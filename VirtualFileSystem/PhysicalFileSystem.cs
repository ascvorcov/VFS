#region Namespace Imports

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

using VirtualFileSystem.Annotations;
using VirtualFileSystem.Interfaces;

#endregion


namespace VirtualFileSystem
{
    /// <summary>
    /// Wrapper implementation of <see cref="IFileSystem"/> above real file system.
    /// </summary>
    internal sealed class PhysicalFileSystem : IFileSystem
    {
        #region Public Methods

        /// <summary>
        /// Copies the directory.
        /// </summary>
        public void CopyDirectory([NotNull] string path, [NotNull] string newPath, [CanBeNull] IFileSystem destinationSystem = null)
        {
            Validate.ArgumentNotNull(path, "path");
            Validate.ArgumentNotNull(newPath, "newPath");

            var op = new VirtualFileOperations(this, destinationSystem ?? this);
            op.CopyDirectory(path, newPath);
        }


        /// <summary>
        /// Copies the file.
        /// </summary>
        public void CopyFile([NotNull] string sourceFileName, [NotNull] string destFileName, [CanBeNull] IFileSystem destinationSystem = null)
        {
            Validate.ArgumentNotNull(sourceFileName, "sourceFileName");
            Validate.ArgumentNotNull(destFileName, "destFileName");

            if (destinationSystem == null || destinationSystem == this)
            {
                File.Copy(sourceFileName, destFileName);
            }
            else
            {
                var operations = new VirtualFileOperations(this, destinationSystem);
                operations.CopyFile(sourceFileName, destFileName);
            }
        }


        /// <summary>
        /// Creates the directory.
        /// </summary>
        public void CreateDirectory([NotNull] string path)
        {
            Validate.ArgumentNotNull(path, "path");

            Directory.CreateDirectory(path);
        }


        /// <summary>
        /// Creates the file.
        /// </summary>
        [NotNull]
        public IFile CreateFile([NotNull] string fileName)
        {
            Validate.ArgumentNotNull(fileName, "fileName");

            return new PhysicalFile(File.Create(fileName), new FileInfo(fileName));
        }


        /// <summary>
        /// Deletes the directory.
        /// </summary>
        public void DeleteDirectory([NotNull] string path, bool recursive)
        {
            Validate.ArgumentNotNull(path, "path");

            Directory.Delete(path, recursive);
        }


        /// <summary>
        /// Deletes the file.
        /// </summary>
        public void DeleteFile([NotNull] string fileName)
        {
            Validate.ArgumentNotNull(fileName, "fileName");

            File.Delete(fileName);
        }


        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
        }


        /// <summary>
        /// Finds the file.
        /// </summary>
        [NotNull]
        public IEnumerable<string> FindFile([NotNull] string path, [NotNull] string pattern, bool recursive)
        {
            Validate.ArgumentNotNull(path, "path");
            Validate.ArgumentNotNull(pattern, "pattern");

            var info = new DirectoryInfo(path);
            return WalkDirectoryTree(info, pattern, recursive);
        }


        /// <summary>
        /// Gets the drives supported by this file system.
        /// </summary>
        [NotNull]
        public IEnumerable<string> GetDrives()
        {
            return DriveInfo.GetDrives().Select(d => d.Name);
        }


        /// <summary>
        /// Gets the file or directory info.
        /// </summary>
        public Info GetFileInfo([NotNull] string path)
        {
            Validate.ArgumentNotNull(path, "path");

            var info = new FileInfo(path);
            ulong size = info.Exists ? (ulong)info.Length : 0;
            bool isDirectory = info.Exists ? false : Directory.Exists(path);
            bool exists = info.Exists || isDirectory;

            return new Info { FileSize = size, Exists = exists, IsDirectory = isDirectory, CreatedDate = info.CreationTime, ModifiedDate = info.LastWriteTime };
        }


        /// <summary>
        /// Moves the directory.
        /// </summary>
        public void MoveDirectory([NotNull] string sourceDirectory, [NotNull] string destination, [CanBeNull] IFileSystem destinationSystem = null)
        {
            Validate.ArgumentNotNull(sourceDirectory, "sourceDirectory");
            Validate.ArgumentNotNull(destination, "destination");

            if (destinationSystem == null || destinationSystem == this)
            {
                Directory.Move(sourceDirectory, destination);
            }
            else
            {
                var op = new VirtualFileOperations(this, destinationSystem);
                op.MoveDirectory(sourceDirectory, destination);
            }
        }


        /// <summary>
        /// Moves the file.
        /// </summary>
        public void MoveFile([NotNull] string sourceFileName, [NotNull] string destFileName, [CanBeNull] IFileSystem destinationSystem = null)
        {
            Validate.ArgumentNotNull(sourceFileName, "sourceFileName");
            Validate.ArgumentNotNull(destFileName, "destFileName");

            if (destinationSystem == null || destinationSystem == this)
            {
                File.Move(sourceFileName, destFileName);
            }
            else
            {
                var operations = new VirtualFileOperations(this, destinationSystem);
                operations.MoveFile(sourceFileName, destFileName);
            }
        }


        /// <summary>
        /// Opens the file.
        /// </summary>
        [NotNull]
        public IFile OpenFile([NotNull] string fileName, bool write)
        {
            Validate.ArgumentNotNull(fileName, "fileName");
            var info = new FileInfo(fileName);
            return new PhysicalFile(write ? File.Open(fileName, FileMode.Open) : File.OpenRead(fileName), info);
        }

        #endregion


        #region Methods

        /// <summary>
        /// Walks the directory tree.
        /// </summary>
        [NotNull]
        private static IEnumerable<string> WalkDirectoryTree([NotNull] DirectoryInfo root, [NotNull] string mask, bool recursive)
        {
            Validate.ArgumentNotNull(root, "root");
            Validate.ArgumentNotNull(mask, "mask");

            FileInfo[] files;
            try
            {
                files = root.GetFiles(mask);
            }
            catch (UnauthorizedAccessException e)
            {
                Trace.Write(e);
                yield break;
            }
            catch (DirectoryNotFoundException e)
            {
                Trace.Write(e);
                yield break;
            }

            if (files != null)
            {
                foreach (FileInfo fi in files)
                {
                    yield return fi.FullName;
                }
            }

            var directories = root.GetDirectories(mask);
            if (directories != null)
            {
                foreach (var dir in directories)
                {
                    yield return dir.FullName;
                }
            }

            if (!recursive)
            {
                yield break;
            }

            foreach (DirectoryInfo dirInfo in root.GetDirectories())
            {
                foreach (var fileName in WalkDirectoryTree(dirInfo, mask, true))
                {
                    yield return fileName;
                }
            }
        }

        #endregion
    }
}