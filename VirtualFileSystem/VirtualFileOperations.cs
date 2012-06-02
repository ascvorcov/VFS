#region Namespace Imports

using System;

using VirtualFileSystem.Annotations;
using VirtualFileSystem.EXT2;
using VirtualFileSystem.Interfaces;

#endregion


namespace VirtualFileSystem
{
    /// <summary>
    /// Implements bulk operations over two abstract file systems.
    /// Implemented as a series of non-locking, non-transactional operations, which may leave system in unstable state.
    /// </summary>
    internal sealed class VirtualFileOperations
    {
        #region Constants and Fields

        private readonly IFileSystem _destination;
        private readonly IFileSystem _source;

        #endregion


        #region Constructors and Destructors

        /// <summary>
        /// Initializes a new instance of the <see cref="VirtualFileOperations"/> class.
        /// </summary>
        /// <param name="source">The source file system.</param>
        /// <param name="destination">The destination file system.</param>
        public VirtualFileOperations([NotNull] IFileSystem source, [NotNull] IFileSystem destination)
        {
            _source = source;
            _destination = destination;
        }

        #endregion


        #region Public Methods

        /// <summary>
        /// Copies the directory.
        /// </summary>
        /// <param name="sourceDirectory">The source directory.</param>
        /// <param name="destDirectory">The destination directory.</param>
        /// <exception cref="InvalidOperationException">File name is invalid.</exception>
        public void CopyDirectory([NotNull] string sourceDirectory, [NotNull] string destDirectory)
        {
            Validate.ArgumentNotNull(sourceDirectory, "sourceDirectory");
            Validate.ArgumentNotNull(destDirectory, "destDirectory");

            var sourceDirLen = sourceDirectory.Length;
            _destination.CreateDirectory(destDirectory);
            foreach (var sourceFileName in _source.FindFile(sourceDirectory, "*", true))
            {
                if (!sourceFileName.StartsWith(sourceDirectory, StringComparison.InvariantCultureIgnoreCase))
                {
                    throw new InvalidOperationException("File name is invalid.");
                }

                var rawName = sourceFileName.Substring(sourceDirLen);
                var destFileName = destDirectory + rawName;

                var info = _source.GetFileInfo(sourceFileName);
                if (info.IsDirectory)
                {
                    _destination.CreateDirectory(destFileName);
                }
                else
                {
                    CopyFile(sourceFileName, destFileName);
                }
            }
        }


        /// <summary>
        /// Copies the file.
        /// </summary>
        /// <param name="sourceFileName">Name of the source file.</param>
        /// <param name="destFileName">Name of the destination file.</param>
        public void CopyFile([NotNull] string sourceFileName, [NotNull] string destFileName)
        {
            Validate.ArgumentNotNull(sourceFileName, "sourceFileName");
            Validate.ArgumentNotNull(destFileName, "destFileName");

            using (var srcFile = _source.OpenFile(sourceFileName, false))
            {
                using (var destFile = _destination.CreateFile(destFileName))
                {
                    byte[] buffer;
                    do
                    {
                        buffer = srcFile.ReadData(Constants.ReadingBufferSizeBytes);
                        if (buffer.Length == 0)
                        {
                            break;
                        }

                        destFile.WriteData(buffer);
                    }
                    while (true);
                }
            }
        }


        /// <summary>
        /// Moves the directory.
        /// </summary>
        /// <param name="sourceDirectory">The source directory.</param>
        /// <param name="destDirectory">The destination directory.</param>
        public void MoveDirectory([NotNull] string sourceDirectory, [NotNull] string destDirectory)
        {
            Validate.ArgumentNotNull(sourceDirectory, "sourceDirectory");
            Validate.ArgumentNotNull(destDirectory, "destDirectory");

            CopyDirectory(sourceDirectory, destDirectory);
            _source.DeleteDirectory(sourceDirectory, true);
        }


        /// <summary>
        /// Moves the file.
        /// </summary>
        /// <param name="sourceFileName">Name of the source file.</param>
        /// <param name="destFileName">Name of the destination file.</param>
        public void MoveFile([NotNull] string sourceFileName, [NotNull] string destFileName)
        {
            Validate.ArgumentNotNull(sourceFileName, "sourceFileName");
            Validate.ArgumentNotNull(destFileName, "destFileName");

            CopyFile(sourceFileName, destFileName);
            _source.DeleteFile(sourceFileName);
        }

        #endregion
    }
}