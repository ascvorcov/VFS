using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using NUnit.Framework;


namespace VirtualFileSystem.UnitTests
{
    [TestFixture]
    public class IntegrationTests
    {
        [Test]
        public void TestFileOperations()
        {
            var real = FileSystems.Real;
            using (var vfs = FileSystems.CreateNewVolume("D:\\storage.vfs", 4096 * 2000, real))
            {
                var file = vfs.CreateFile("\\file1.txt");
                file.WriteData(new byte[] { 1, 2, 3, 4, 5 });
                file.SetPosition(0, SeekOrigin.Begin);
                file.ReadData(5);
                file.SetFileSize(100);
                file.SetFileSize(4);
                file.ReadData(1);
                file.SetFileSize(0);

                byte[] data = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
                for (int i = 0; i < 4000; ++i )
                {
                    file.WriteData(data);
                }

                file.SetFileSize(1);
                file.SetPosition(0, SeekOrigin.Begin);
                Assert.IsTrue(file.ReadData(2)[0] == 1);

                file.Close();
            }

            real.Dispose();
        }

        [Test]
        public void TestMultithreadedAccessToFile()
        {
            using (var vfs = FileSystems.CreateNewVolume("D:\\storage.vfs", 4096 * 600, FileSystems.Real))
            {
                var file = vfs.CreateFile("\\file1.txt");
                file.SetFileSize(10000);
                file.Close();

                //check that reading is not allowed if file is locked for writing.
                var forWriting = vfs.OpenFile("\\file1.txt", true);
                var t0 = Task.Factory.StartNew(() => vfs.OpenFile("\\file1.txt", false));
                Assert.Throws<AggregateException>(t0.Wait);
                forWriting.Close();

                Action worker = () =>
                {
                    var f1 = vfs.OpenFile("\\file1.txt", false);

                    for (int i = 0; i < 100; ++i)
                    {
                        var data = f1.ReadData(100);
                        Assert.IsNotNull(data);
                        Assert.AreEqual(100, data.Length);
                        Thread.Sleep(10);
                    }

                    f1.Close();
                };

                var t1 = Task.Factory.StartNew(worker);
                var t2 = Task.Factory.StartNew(worker);

                t1.Wait();
                t2.Wait();
            }

        }

        [Test]
        public void TestCreateALotOfFiles()
        {
            using (var vfs = FileSystems.CreateNewVolume("D:\\storage.vfs", 4096 * 600, FileSystems.Real))
            {
                var data = new byte[] { 1, 2, 3, 4, 5 };
                bool exception = false;
                int i = 1;
                try
                {
                    string dir = "\\";
                    while (i < 100000)
                    {
                        dir += "dir\\";
                        vfs.CreateDirectory(dir);
                        for (int j = 1; j < 50; ++j)
                        {
                            using (var file = vfs.CreateFile(dir + "file" + j + ".bin"))
                            {
                                file.WriteData(data);
                            }
                        }
                        i++;
                    }
                }
                catch (IOException)
                {
                    exception = true;
                }

                Assert.IsTrue(exception);
            }
        }

        [Test]
        public void TestEmbeddedFileSystem()
        {
            var data = new byte[] { 5, 4, 3, 2, 1 };
            using (var vfs = FileSystems.CreateNewVolume("D:\\storage.vfs", 4096 * 2000, FileSystems.Real))
            {
                using (var embeddedVfs = FileSystems.CreateNewVolume("\\test.vfs", 4096 * 1000, vfs))
                {
                    embeddedVfs.CreateDirectory("\\test");
                    var file = embeddedVfs.CreateFile("\\test\\file.dat");
                    file.WriteData(data);
                    file.SetFileSize(100);
                    file.Close();
                    
                    embeddedVfs.CreateDirectory("\\тест2");
                    embeddedVfs.DeleteDirectory("\\тест2", false);
                }
            }

            using (var vfs = FileSystems.MountVirtual("D:\\storage.vfs", FileSystems.Real))
            {
                using (var embeddedVfs = FileSystems.MountVirtual("\\test.vfs", vfs))
                {
                    var file = embeddedVfs.OpenFile("\\test\\file.dat", false);
                    Assert.AreEqual(100, file.GetFileSize());
                    CollectionAssert.AreEqual(data, file.ReadData(5));
                    file.Close();
                }
            }
        }

        [Test]
        public void TestMoveCopyFile()
        {
            using (var vfs = FileSystems.CreateNewVolume("D:\\storage.vfs", 4096 * 2000, FileSystems.Real))
            {
                vfs.CreateDirectory("\\dir1");
                vfs.CreateDirectory("\\dir1\\dir2");
                vfs.CreateDirectory("\\dir1\\dir3");
                var file = vfs.CreateFile("\\dir1\\file.txt");
                file.Close();

                vfs.MoveFile("\\dir1\\file.txt", "\\dir1\\dir2\\file.txt");
                vfs.MoveFile("\\dir1\\dir2\\file.txt", "\\dir1\\dir3\\file.txt");
                vfs.MoveFile("\\dir1\\dir3\\file.txt", "\\dir1\\dir2\\file.txt");
                vfs.MoveFile("\\dir1\\dir2\\file.txt", "\\dir1\\file.txt");

                vfs.MoveFile("\\dir1\\file.txt", "\\Dir1\\file1.txt");
                vfs.MoveFile("\\dir1\\file1.txt", "\\Dir1\\file.txt");

                vfs.CopyFile("\\dir1\\file.txt", "\\dir1\\dir2\\file2.txt");
                vfs.CopyFile("\\dir1\\dir2\\file2.txt", "\\dir1\\file2.txt");
                vfs.CopyFile("\\dir1\\dir2\\file2.txt", "\\file2.txt");
            }
        }

        [Test]
        public void TestRecursiveDirectoryRemoval()
        {
            var data = new byte[] { 5, 4, 3, 2, 1 };
            using (var vfs = FileSystems.CreateNewVolume("D:\\storage.vfs", 4096 * 2000, FileSystems.Real))
            {
                vfs.CreateDirectory("\\dir1");
                vfs.CreateDirectory("\\dir1\\dir2");
                vfs.CreateDirectory("\\dir1\\dir3");
                var file = vfs.CreateFile("\\dir1\\file1.txt");
                file.WriteData(data);
                file.Close();

                file = vfs.CreateFile("\\dir1\\dir3\\file2.txt");
                file.WriteData(data);
                file.Close();

                vfs.DeleteDirectory("\\dir1\\dir2", false);
                vfs.DeleteFile("\\dir1\\file1.txt");
                vfs.DeleteDirectory("\\dir1", true);
            }
        }


        [Test]
        public void TestWriteLargeFileUntilOutOfSpace()
        {
            using (var vfs = FileSystems.CreateNewVolume("D:\\storage.vfs", 4096 * 2000, FileSystems.Real))
            {
                var rnd = new Random();
                var buffer = new byte[10000];
                for (int i = 0; i < buffer.Length; ++i)
                {
                    buffer[i] = (byte)rnd.Next(255);
                }

                vfs.CreateDirectory("\\dir1");
                var file = vfs.CreateFile("\\dir1\\file.dat");

                bool exception = false;
                int j = 0;
                try
                {
                    while (j++ < 100000)
                    {
                        file.WriteData(buffer);
                    }
                }
                catch (IOException)
                {
                    exception = true;
                }

                file.Close();
                Assert.IsTrue(exception);
            }
        }

        [Test]
        public void TestWriteAndReadData()
        {
            byte[] buffer = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15 };
            var real = FileSystems.Real;
            using (var vfs = FileSystems.CreateNewVolume("D:\\storage.vfs", 4096 * 2000, real))
            {
                using (var file1 = vfs.CreateFile("\\file1.txt"))
                {
                    for (int i = 0; i < 2740; ++i)
                    {
                        file1.WriteData(buffer);
                    }
                }
            }

            using (var vfs = FileSystems.MountVirtual("D:\\storage.vfs", real))
            {
                vfs.CopyFile("\\file1.txt", "D:\\file1.txt", FileSystems.Real);
                using (var file1 = FileSystems.Real.OpenFile("D:\\file1.txt", false))
                {
                    for (int i = 0; i < 2740; ++i)
                    {
                        var readData = file1.ReadData((uint)buffer.Length);
                        CollectionAssert.AreEqual(buffer, readData);
                    }
                }
            }
        }

        [Test]
        public void TestDirectoryEntryPopulation()
        {
            var real = FileSystems.Real;
            using (var vfs = FileSystems.CreateNewVolume("D:\\storage.vfs", 4096 * 2000, real))
            {
                for (int j = 0; j < 50; ++j)
                {
                    for (int i = 1; i < 255; ++i)
                    {
                        var name = "\\" + new string('a', i);
                        vfs.CreateDirectory(name);
                        vfs.DeleteDirectory(name, true);
                    }
                }
            }

        }

        [Test]
        public void TestDirectoryEntriesSpanMultipleBlocks()
        {
            var real = FileSystems.Real;
            using (var vfs = FileSystems.CreateNewVolume("D:\\storage.vfs", 4096 * 2000, real))
            {
                for (int i = 0; i < 50; ++i)
                {
                    var fname = "\\" + new string('a', 100) + i;
                    var file = vfs.CreateFile(fname);
                    file.Close();
                }
            }

            using (var vfs = FileSystems.MountVirtual("D:\\storage.vfs", real))
            {
                var files = vfs.FindFile("\\", "*", true).ToList();
                Assert.AreEqual(50, files.Count);
                var firstName = "\\" + new string('a', 100) + 0;
                Assert.AreEqual(firstName, files[0]);
            }

        }

        [Test]
        public void TestFileAndFolderStructure()
        {
            var real = FileSystems.Real;
            using (var vfs = FileSystems.CreateNewVolume("D:\\storage.vfs", 4096 * 2000, real))
            {
                var file1 = vfs.CreateFile("\\file1.txt");
                var file2 = vfs.CreateFile("\\имя файла на русском языке.gif");
                var file3 = vfs.CreateFile("\\long file name with whitespaces and no extension");

                file1.Close();
                file2.Close();
                file3.Close();

                vfs.CreateDirectory("\\folder1\\"); // with slash
                vfs.CreateDirectory("\\folder1\\folder2"); // no slash
                vfs.CreateDirectory("\\folder2"); // duplicate name 

                var file4 = vfs.CreateFile("\\folder2\\файл"); // mixed name
                file4.SetFileSize(5000);
                file4.WriteData(new byte[] { 1,2,3,4,5 });
                file4.Close();

                vfs.DeleteDirectory("\\folder1\\", true);
            }

            using (var vfs = FileSystems.MountVirtual("D:\\storage.vfs", real))
            {
                var file = vfs.OpenFile("\\file1.txt", true);
                file.WriteData(new byte[] { 1, 2, 3, 4, 5, 6 });
                file.Close();

                file = vfs.OpenFile("\\folder2\\файл", false); // open file deep in directory
                Assert.AreEqual(5000ul, file.GetFileSize());
                Assert.AreEqual(1, file.ReadData(5)[0]);
                file.Close();

                file = vfs.OpenFile("\\имя файла на русском языке.gif", false);
                Assert.AreEqual(0, file.GetFileSize());
                file.Close();

                var files = vfs.FindFile("\\", "*1*", true).ToList();
                Assert.AreEqual(1, files.Count); // should be only 1, folder1 is deleted
                Assert.AreEqual("\\file1.txt", files[0]);
            }
        }
    }
}
