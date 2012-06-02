using NUnit.Framework;

using Rhino.Mocks;

using VirtualFileSystem.EXT2;
using VirtualFileSystem.Interfaces;
using VirtualFileSystem.Utilities;


namespace VirtualFileSystem.UnitTests
{
    [TestFixture]
    public class SparseWriterTests
    {
        [Test]
        public void TestHeadAndTailNoBody()
        {
            // test case when there is no body, just head and tail in two blocks.
            var repo = new MockRepository();
            var diskAccessMock = repo.StrictMock<IDirectDiskAccess>();
            const uint size = 15;
            var data = new byte[size];

            ulong addr1 = 4095;
            ulong addr2 = 4096;
            diskAccessMock.WriteBytes(ref addr1, data, 0, 1);
            diskAccessMock.WriteBytes(ref addr2, data, 1, size - 1);
            repo.ReplayAll();

            var writer = new SparseWriter(diskAccessMock);

            Assert.AreEqual(2, SparseWriter.GetNumberOfBlocksRequired(size, 4095));

            var dest = new[] { new Address(0), new Address(4096) };
            writer.WriteData(data, dest, 4095);

            repo.VerifyAll();
        }

        [Test]
        public void TestTailFitsIntoBody()
        {
            // test case when tail completely fits into last block
            var repo = new MockRepository();
            var diskAccessMock = repo.DynamicMock<IDirectDiskAccess>();
            var data = new byte[10000];

            uint delta = 4096 - 2288;
            ulong addr1 = 2288;
            ulong addr2 = 4096;
            ulong addr3 = 8192;
            diskAccessMock.WriteBytes(ref addr1, data, 0, delta);
            diskAccessMock.WriteBytes(ref addr2, data, delta, 4096);
            diskAccessMock.WriteBytes(ref addr3, data, delta + 4096, 4096);
            repo.ReplayAll();

            var writer = new SparseWriter(diskAccessMock);
            Assert.AreEqual(3, SparseWriter.GetNumberOfBlocksRequired(10000, 2288));
            var dest = new [] { new Address(0), new Address(4096), new Address(8192) };
            writer.WriteData(data, dest, 2288);

            repo.VerifyAll();
        }

        [Test]
        public void TestHeadAndBodyFitInBlockMargin()
        {
            var repo = new MockRepository();
            var diskAccessMock = repo.DynamicMock<IDirectDiskAccess>();

            var data = new byte[8192];
            ulong addr1 = 0;
            ulong addr2 = 4096;
            diskAccessMock.WriteBytes(ref addr1, data, 0, 4096);
            diskAccessMock.WriteBytes(ref addr2, data, 4096, 4096);
            repo.ReplayAll();


            var writer = new SparseWriter(diskAccessMock);
            Assert.AreEqual(2, SparseWriter.GetNumberOfBlocksRequired(8192, 0));
            var dest = new[] { new Address(0), new Address(4096) };
            writer.WriteData(data, dest, 0);
            repo.VerifyAll();
        }

        [Test]
        public void TestHeadBodyAndTailTakeOneBlockEach()
        {
            var repo = new MockRepository();
            var diskAccessMock = repo.DynamicMock<IDirectDiskAccess>();
            var data = new byte[4096 * 3];

            ulong addr1 = 0;
            ulong addr2 = 4096;
            ulong addr3 = 8192;
            diskAccessMock.WriteBytes(ref addr1, data, 0,    4096);
            diskAccessMock.WriteBytes(ref addr2, data, 4096, 4096);
            diskAccessMock.WriteBytes(ref addr3, data, 8192, 4096);
            repo.ReplayAll();


            var writer = new SparseWriter(diskAccessMock);

            Assert.AreEqual(3, SparseWriter.GetNumberOfBlocksRequired(4096 * 3, 0));

            var dest = new[] { new Address(0), new Address(4096), new Address(8192) };
            writer.WriteData(data, dest, 0);

            repo.VerifyAll();
        }
    }
}