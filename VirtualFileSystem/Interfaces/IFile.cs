#region Namespace Imports

using System;
using System.IO;

using VirtualFileSystem.Annotations;

#endregion


namespace VirtualFileSystem.Interfaces
{
    /// <summary>
    /// Defines a file interface.
    /// </summary>
    public interface IFile : IDisposable
    {
        #region Properties

        /// <summary>
        /// Gets a value indicating whether this <see cref="IFile"/> can written to.
        /// </summary>
        bool CanWrite
        {
            get;
        }

        /// <summary>
        /// Gets the file creation time.
        /// </summary>
        DateTime CreationTime
        {
            get;
        }

        /// <summary>
        /// Gets the last file modification time.
        /// </summary>
        DateTime LastModificationTime
        {
            get;
        }

        #endregion


        #region Public Methods

        /// <summary>
        /// Closes the current file, releasing resources.
        /// </summary>
        void Close();

        /// <summary>
        /// Gets the size of the file.
        /// </summary>
        ulong GetFileSize();

        /// <summary>
        /// Reads the portion of file data, advancing read pointer.
        /// Will return empty array if at end of file.
        /// </summary>
        [NotNull]
        byte[] ReadData(uint length);

        /// <summary>
        /// Sets the new size of the file. Can shrink and grow file.
        /// If file size was reduced, and current position is beyond file size,
        /// current position is relocated to end of file.
        /// </summary>
        void SetFileSize(ulong size);

        /// <summary>
        /// Sets the current read or write position.
        /// Can be set from beginning or end of file. 
        /// Offset can be negative if seeking from current position.
        /// </summary>
        ulong SetPosition(long offset, SeekOrigin origin);

        /// <summary>
        /// Writes the data to file. File is automatically grown,
        /// if write beyond current size is made.
        /// </summary>
        /// <param name="data">The data to write.</param>
        void WriteData([NotNull] byte[] data);

        #endregion
    }
}