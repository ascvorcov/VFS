using System;


namespace VirtualFileSystem.Interfaces
{
    /// <summary>
    /// Represents information about file/directory.
    /// </summary>
    public struct Info
    {
        /// <summary>
        /// True, if file/directory exists.
        /// </summary>
        public bool Exists;

        /// <summary>
        /// True, if entry references directory, otherwise False.
        /// </summary>
        public bool IsDirectory;

        /// <summary>
        /// Size of file. Zero if entry is directory.
        /// </summary>
        public ulong FileSize;

        /// <summary>
        /// Date when file/directory was last modified.
        /// </summary>
        public DateTime ModifiedDate;

        /// <summary>
        /// Date when file/directory was last created.
        /// </summary>
        public DateTime CreatedDate;
    }
}