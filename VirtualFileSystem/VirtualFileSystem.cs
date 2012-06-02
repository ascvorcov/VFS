#region Namespace Imports

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using VirtualFileSystem.Annotations;
using VirtualFileSystem.EXT2;
using VirtualFileSystem.Interfaces;
using VirtualFileSystem.Utilities;

#endregion


namespace VirtualFileSystem
{
    /// <summary>
    /// Represents virtual file system facade, implementation of <see cref="IFileSystem"/> interface.
    /// </summary>
    internal sealed class VirtualFileSystem : IFileSystem
    {
        #region Constants and Fields

        private readonly IDirectDiskAccess _diskAccess;
        private readonly MasterRecord _record;
        private bool _disposed;

        #endregion


        #region Constructors and Destructors

        /// <summary>
        /// Initializes a new instance of the <see cref="VirtualFileSystem"/> class.
        /// Load existing file system.
        /// </summary>
        internal VirtualFileSystem([NotNull] IDirectDiskAccess diskAccess)
        {
            Validate.ArgumentNotNull(diskAccess, "diskAccess");

            _diskAccess = diskAccess;
            _record = new MasterRecord(diskAccess);
            _record.Load();
        }


        /// <summary>
        /// Initializes a new instance of the <see cref="VirtualFileSystem"/> class.
        /// Create new file system.
        /// </summary>
        internal VirtualFileSystem([NotNull] IDirectDiskAccess diskAccess, ulong newVolumeSize)
        {
            Validate.ArgumentNotNull(diskAccess, "diskAccess");

            _diskAccess = diskAccess;
            _record = MasterRecord.CreateNewVolume(diskAccess, newVolumeSize);
            _record.Save();
        }

        #endregion


        #region Properties

        public ulong RealVolumeSize
        {
            get
            {
                CheckDisposed();

                return _record.VolumeSize;
            }
        }

        #endregion


        #region Public Methods

        public NodeStorage MountDisk(string fileName)
        {
            var vfn = new VirtualFileName(fileName);
            var locker = FindNode(vfn, false, true);
            try
            {
                var file = locker.File;
                if (file == null)
                    return null;

                return new NodeStorage(file);
            }
            finally
            {
                locker.Dispose();
            }

        }

        /// <summary>
        /// Copies the directory to new destination.
        /// </summary>
        /// <param name="sourceDirectory">The source directory name. Must reference existing directory.</param>
        /// <param name="destination">The destination directory name. That is the name of directory which will be created.</param>
        /// <param name="destinationSystem">The destination system. Optional.</param>
        public void CopyDirectory([NotNull] string sourceDirectory, [NotNull] string destination, [CanBeNull] IFileSystem destinationSystem = null)
        {
            Validate.ArgumentNotNull(sourceDirectory, "sourceDirectory");
            Validate.ArgumentNotNull(destination, "destination");

            CheckDisposed();

            // implemented as a set of simple operations.
            var operations = new VirtualFileOperations(this, destinationSystem ?? this);
            operations.CopyDirectory(sourceDirectory, destination);
        }


        /// <summary>
        /// Copies the file.
        /// </summary>
        /// <param name="sourceFileName">Name of the source file.</param>
        /// <param name="destFileName">Name of the destination file.</param>
        /// <param name="destinationSystem">The destination system. Optional.</param>
        /// <exception cref="FileNotFoundException">Cannot find source file to copy.</exception>
        /// <exception cref="DirectoryNotFoundException">Specified destination path does not exist.</exception>
        public void CopyFile([NotNull] string sourceFileName, [NotNull] string destFileName, [CanBeNull] IFileSystem destinationSystem = null)
        {
            Validate.ArgumentNotNull(sourceFileName, "sourceFileName");
            Validate.ArgumentNotNull(destFileName, "destFileName");

            CheckDisposed();

            // destination and source must be file names. directory names are not supported (same as native io).
            bool sameSystem = destinationSystem == null || destinationSystem == this;

            // move inside same volume
            if (sameSystem && sourceFileName == destFileName)
            {
                return;
            }

            if (sameSystem)
            {
                var sourceVfn = new VirtualFileName(sourceFileName);
                using (var srcResult = FindNode(sourceVfn, false)) // lock file for reading, we're copying
                {
                    var sourceFile = srcResult.File;
                    if (sourceFile == null)
                    {
                        throw new FileNotFoundException("Cannot find source file to copy.", sourceFileName);
                    }

                    var vfn = new VirtualFileName(destFileName);

                    // lock destination dir for writing - new file will be created there.
                    using (var destResult = FindNode(vfn, true, true))
                    {
                        var parentDir = destResult.Directory;
                        if (parentDir == null)
                        {
                            throw new DirectoryNotFoundException("Specified destination path does not exist.");
                        }

                        var destFile = _record.CreateFileNode(vfn.Name, parentDir);
                        using (NodeLocker.Lock(destFile, true))
                        {
                            CopyNodeData(sourceFile, destFile);
                        }
                    }
                }
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
        /// <param name="path">The directory path to create.</param>
        /// <exception cref="DirectoryNotFoundException">Specified path does not reference a directory.</exception>
        public void CreateDirectory([NotNull] string path)
        {
            Validate.ArgumentNotNull(path, "path");

            CheckDisposed();

            var vfn = new VirtualFileName(path);
            using (var result = FindNode(vfn, true, true))
            {
                var parent = result.Directory;
                if (parent == null)
                {
                    throw new DirectoryNotFoundException("Specified path does not reference a directory.");
                }

                _record.CreateDirectoryNode(vfn.Name, parent);
            }
        }


        /// <summary>
        /// Creates the new file and locks it for writing.
        /// </summary>
        /// <param name="fileName">Name of the file.</param>
        /// <exception cref="DirectoryNotFoundException">Specified path does not reference a directory.</exception>
        [NotNull]
        public IFile CreateFile([NotNull] string fileName)
        {
            Validate.ArgumentNotNull(fileName, "fileName");

            CheckDisposed();

            var vfn = new VirtualFileName(fileName);

            VirtualFile ret;
            using (var result = FindNode(vfn, true, true))
            {
                var parentNode = result.Directory;
                if (parentNode == null)
                {
                    throw new DirectoryNotFoundException("Specified path does not reference a directory.");
                }

                var node = _record.CreateFileNode(vfn.Name, parentNode);
                node.LockWrite();
                ret = new VirtualFile(node, true);
            }
            return ret;
        }


        /// <summary>
        /// Deletes the directory.
        /// </summary>
        /// <param name="path">The path to directory to delete.</param>
        /// <param name="recursive">If set to <c>true</c>, recursive directory removal is performed.</param>
        /// <exception cref="DirectoryNotFoundException">Specified path does not reference a directory.</exception>
        public void DeleteDirectory([NotNull] string path, bool recursive)
        {
            Validate.ArgumentNotNull(path, "path");

            CheckDisposed();

            // todo: recursive delete should not lock entire tree. parallel read should be possible.
            var vfn = new VirtualFileName(path);
            using (var result = FindNode(vfn, true, true))
            {
                var parentDirectory = result.Directory;
                if (parentDirectory == null)
                {
                    throw new DirectoryNotFoundException("Specified path does not reference a directory.");
                }

                var node = parentDirectory.FindAndRemoveChildEntry(vfn.Name, true);
                if (node == null)
                {
                    throw new DirectoryNotFoundException("Specified path does not reference a directory.");
                }

                DeallocateRecursive(node);
            }
        }


        /// <summary>
        /// Deletes the file.
        /// </summary>
        /// <param name="fileName">Name of the file.</param>
        /// <exception cref="FileNotFoundException">File not found.</exception>
        public void DeleteFile([NotNull] string fileName)
        {
            Validate.ArgumentNotNull(fileName, "fileName");

            CheckDisposed();

            var vfn = new VirtualFileName(fileName);

            using (var result = FindNode(vfn, true, true))
            {
                var parentNode = result.Directory;
                if (parentNode == null)
                {
                    throw new FileNotFoundException("File not found.", fileName);
                }

                var fileNode = parentNode.FindAndRemoveChildEntry(vfn.Name, false);
                if (fileNode == null)
                {
                    throw new FileNotFoundException("File not found.", fileName);
                }

                _record.FreeNodeAndAllAllocatedBlocks(fileNode);
            }
        }


        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _record.Dispose();
            _diskAccess.Dispose();
            _disposed = true;
        }


        /// <summary>
        /// Finds the file at specified path, using specified pattern. Can do
        /// recursive search. Tries to do minimal locking, when control is
        /// returned to user no locks are kept.
        /// </summary>
        /// <param name="path">The path to search.</param>
        /// <param name="pattern">The pattern to find. Can include ? and * symbols.</param>
        /// <param name="recursive">If set to <c>true</c>,  recursive search is performed, in all child directories.</param>
        /// <returns>
        /// <c>IEnumerable</c> with full paths of files and directories which were found.
        /// </returns>
        [NotNull]
        public IEnumerable<string> FindFile([NotNull] string path, [NotNull] string pattern, bool recursive)
        {
            Validate.ArgumentNotNull(path, "path");
            Validate.ArgumentNotNull(pattern, "pattern");

            CheckDisposed();
            var vfn = new VirtualFileName(path);
            List<string> matches;
            List<string> dirsToSearch;
            using (var result = FindNode(vfn, false))
            {
                var parentDirectory = result.Directory;
                if (parentDirectory == null)
                {
                    // this situation is possible when directory is being changed,
                    // and target was removed. Don't fail, just skip.
                    yield break;
                }

                dirsToSearch = parentDirectory.GetAllChildDirectories().ToList();
                matches = parentDirectory.FindMatchingEntries(pattern).ToList();
            }

            // unlock directory here.
            foreach (var file in matches)
            {
                yield return VirtualFileName.Combine(vfn.FullName, file);
            }

            if (!recursive || !dirsToSearch.Any())
            {
                yield break;
            }

            foreach (var dir in dirsToSearch)
            {
                foreach (var fileName in FindFile(VirtualFileName.Combine(path, dir), pattern, true))
                {
                    yield return fileName;
                }
            }
        }


        /// <summary>
        /// Gets the drives supported by this file system. 
        /// Always returns root directory symbol for virtual file system.
        /// </summary>
        [NotNull]
        public IEnumerable<string> GetDrives()
        {
            CheckDisposed();

            yield return VirtualFileName.Separator; // always single root.
        }


        /// <summary>
        /// Gets the information about file or directory at specified path.
        /// Does not fail if specified path does not exists.
        /// </summary>
        public Info GetFileInfo([NotNull] string path)
        {
            CheckDisposed();
            var vfn = new VirtualFileName(path);
            using (var result = FindNode(vfn, false))
            {
                return new Info
                {
                    Exists = result.Exists,
                    FileSize = result.File == null ? 0 : result.File.FileSize,
                    IsDirectory = result.Directory != null,
                    CreatedDate = result.Node == null ? DateTime.MinValue : result.Node.CreatedDate,
                    ModifiedDate = result.Node == null ? DateTime.MinValue : result.Node.ModifiedDate
                };
            }
        }


        /// <summary>
        /// Moves or renames the directory.
        /// </summary>
        /// <param name="sourceDirectory">The source directory.</param>
        /// <param name="destination">The destination.</param>
        /// <param name="destinationSystem">The destination system. Optional. </param>
        public void MoveDirectory([NotNull] string sourceDirectory, [NotNull] string destination, [CanBeNull] IFileSystem destinationSystem = null)
        {
            Validate.ArgumentNotNull(sourceDirectory, "sourceDirectory");
            Validate.ArgumentNotNull(destination, "destination");

            CheckDisposed();

            // implemented as a set of simple operations
            var operations = new VirtualFileOperations(this, destinationSystem ?? this);
            operations.MoveDirectory(sourceDirectory, destination);
        }


        /// <summary>
        /// Moves or renames the file.
        /// </summary>
        /// <param name="sourceFileName">Name of the source file.</param>
        /// <param name="destFileName">New name and path of moved file.</param>
        /// <param name="destinationSystem">The destination system. Optional.</param>
        /// <exception cref="FileNotFoundException">Cannot find source file to move.</exception>
        /// <exception cref="DirectoryNotFoundException">Cannot find destination directory for move operation.</exception>
        /// <exception cref="IOException">Destination file already exists.</exception>
        public void MoveFile([NotNull] string sourceFileName, [NotNull] string destFileName, [CanBeNull] IFileSystem destinationSystem = null)
        {
            Validate.ArgumentNotNull(sourceFileName, "sourceFileName");
            Validate.ArgumentNotNull(destFileName, "destFileName");

            CheckDisposed();

            // destination and source must be file names. directory names are not supported (same as native io).
            bool sameSystem = destinationSystem == null || destinationSystem == this;

            // move inside same volume
            if (sameSystem && sourceFileName == destFileName)
            {
                return;
            }

            if (sameSystem)
            {
                var sourceVfn = new VirtualFileName(sourceFileName);
                var destVfn = new VirtualFileName(destFileName);

                NodeLocker srcResult;
                NodeLocker destResult;


                if (string.Equals(sourceVfn.Path, destVfn.Path, StringComparison.InvariantCultureIgnoreCase))
                {
                    // same folder, rename file.
                    srcResult = destResult = FindNode(sourceVfn, true, true);
                }
                else if (sourceVfn.AllSegments.Count() > destVfn.AllSegments.Count())
                {
                    // lock longer path first
                    srcResult = FindNode(sourceVfn, true, true);
                    destResult = FindNode(destVfn, true, true);
                }
                else
                {
                    destResult = FindNode(destVfn, true, true);
                    srcResult = FindNode(sourceVfn, true, true);
                }

                using (srcResult)
                {
                    var sourceDir = srcResult.Directory;
                    if (sourceDir == null)
                    {
                        throw new FileNotFoundException("Cannot find source file to move.", sourceFileName);
                    }

                    using (destResult)
                    {
                        var destDir = destResult.Directory;
                        if (destDir == null)
                        {
                            throw new DirectoryNotFoundException("Cannot find destination directory for move operation.");
                        }

                        if (destDir.FindChildEntry(destVfn.Name) != null)
                        {
                            throw new IOException("Destination file already exists.");
                        }

                        var sourceFile = sourceDir.FindChildEntry(sourceVfn.Name);
                        if (sourceFile == null || sourceFile is DirectoryNode)
                        {
                            throw new FileNotFoundException("Cannot find source file to move.", sourceFileName);
                        }

                        using (NodeLocker.Lock(sourceFile, true))
                        {
                            sourceDir.FindAndRemoveChildEntry(sourceVfn.Name, false);
                            destDir.AddChildEntry(destVfn.Name, false, sourceFile.Address);
                        }
                    }
                }
            }
            else
            {
                var operations = new VirtualFileOperations(this, destinationSystem);
                operations.MoveFile(sourceFileName, destFileName);
            }
        }


        /// <summary>
        /// Opens the file for reading or writing.
        /// </summary>
        /// <param name="fileName">Name of the file.</param>
        /// <param name="canWrite">If set to <c>true</c>, file is open for writing.</param>
        /// <exception cref="Exception"><c>IO Exception</c>.</exception>
        /// <exception cref="FileNotFoundException">File not found.</exception>
        [NotNull]
        public IFile OpenFile([NotNull] string fileName, bool canWrite)
        {
            Validate.ArgumentNotNull(fileName, "fileName");

            CheckDisposed();

            var vfn = new VirtualFileName(fileName);

            VirtualFile ret;
            // no using clause, since file must remain locked.
            var result = FindNode(vfn, false, canWrite);
            try
            {
                var currentNode = result.File;
                if (currentNode == null)
                {
                    throw new FileNotFoundException("File not found.", fileName);
                }

                ret = new VirtualFile(currentNode, canWrite);
            }
            catch
            {
                result.Dispose();
                throw;
            }

            return ret;
        }

        #endregion


        #region Methods

        /// <summary>
        /// Checks if current file system was disposed.
        /// </summary>
        /// <exception cref="ObjectDisposedException">File system was disposed.</exception>
        private void CheckDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException("File system was disposed.");
            }
        }


        /// <summary>
        /// Copies the node data.
        /// </summary>
        private static void CopyNodeData([NotNull] FileNode sourceFile, [NotNull] FileNode destFile)
        {
            Validate.ArgumentNotNull(sourceFile, "sourceFile");
            Validate.ArgumentNotNull(destFile, "destFile");

            ulong position = 0;
            byte[] buffer;
            destFile.SetFileSize(sourceFile.FileSize);
            while ((buffer = sourceFile.ReadData(position, Constants.ReadingBufferSizeBytes)).Length > 0)
            {
                destFile.WriteData(position, buffer);
                position += (uint)buffer.Length;
            }
        }


        /// <summary>
        /// Recursively lock and deallocate tree, starting from specified node.
        /// </summary>
        private void DeallocateRecursive([NotNull] Node root)
        {
            Validate.ArgumentNotNull(root, "root");

            using (NodeLocker.Lock(root, true))
            {
                if (root.IsDirectory)
                {
                    var dir = (DirectoryNode)root;
                    foreach (var child in dir.AllChildEntries)
                    {
                        DeallocateRecursive(child);
                    }
                }

                _record.FreeNodeAndAllAllocatedBlocks(root);
            }

            // dispose only when unlocked.
            root.Dispose();
        }


        /// <summary>
        /// Finds the node by specified path.
        /// Locks path for reading while looking for target node.
        /// Found node is returned as locked.
        /// </summary>
        /// <param name="vfn">The virtual file name object.</param>
        /// <param name="excludeLast">If set to <c>true</c>, last component of path is excluded (directory retrieved instead of file).</param>
        /// <param name="lockTargetForWriting">If set to <c>true</c>, locks target for writing.</param>
        [NotNull]
        private NodeLocker FindNode([NotNull] VirtualFileName vfn, bool excludeLast, bool lockTargetForWriting = false)
        {
            Validate.ArgumentNotNull(vfn, "vfn");

            NodeLocker ret;
            var lockedNodes = new Stack<Node>();
            try
            {
                _record.RootNode.LockRead();
                lockedNodes.Push(_record.RootNode);
                Node currentNode = _record.RootNode;
                foreach (var segment in excludeLast ? vfn.AllSegmentsExceptLast : vfn.AllSegments)
                {
                    var dirNode = currentNode as DirectoryNode;
                    if (dirNode == null)
                    {
                        currentNode = null;
                        break;
                    }

                    currentNode = dirNode.FindChildEntry(segment);
                    if (currentNode == null)
                    {
                        continue;
                    }

                    if (!currentNode.TryLockRead())
                    {
                        // failed to obtain read lock, probably file node locked by someone for writing.
                        return new NodeLocker(null, false);
                    }

                    lockedNodes.Push(currentNode);
                }

                // current node can be null.
                ret = new NodeLocker(currentNode, lockTargetForWriting);
                if (currentNode != null)
                {
                    // do not unlock last, if it was found
                    lockedNodes.Pop();
                    if (lockTargetForWriting)
                    {
                        currentNode.UnlockRead();
                        currentNode.LockWrite();
                    }
                }
            }
            finally
            {
                // unlock also if exception happens.
                while (lockedNodes.Count > 0)
                {
                    lockedNodes.Pop().UnlockRead();
                }
            }

            return ret;
        }

        #endregion
    }
}