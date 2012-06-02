#region Namespace Imports

using System;
using System.Collections.Generic;

using VirtualFileSystem.Annotations;

#endregion


namespace VirtualFileSystem.Interfaces
{
    /// <summary>
    /// Defines an abstract file system interface.
    /// </summary>
    public interface IFileSystem : IDisposable
    {
        /// <summary>
        /// Copies the directory.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <param name="newPath">The new path.</param>
        /// <param name="destinationSystem">The destination system.</param>
        void CopyDirectory([NotNull] string path, [NotNull] string newPath, [CanBeNull] IFileSystem destinationSystem = null);
        
        /// <summary>
        /// Copies the file.
        /// </summary>
        /// <param name="fileName">Name of the file.</param>
        /// <param name="newFileName">New name of the file.</param>
        /// <param name="destinationSystem">The destination system.</param>
        void CopyFile([NotNull] string fileName, [NotNull] string newFileName, [CanBeNull] IFileSystem destinationSystem = null);

        /// <summary>
        /// Moves the directory.
        /// </summary>
        /// <param name="sourceDirectory">The source directory.</param>
        /// <param name="destinationDirectory">The destination directory.</param>
        /// <param name="destinationSystem">The destination system.</param>
        void MoveDirectory([NotNull] string sourceDirectory, [NotNull] string destinationDirectory, [CanBeNull] IFileSystem destinationSystem = null);

        /// <summary>
        /// Moves the file.
        /// </summary>
        /// <param name="sourceFileName">Name of the source file.</param>
        /// <param name="destFileName">Name of the destination file.</param>
        /// <param name="destinationSystem">The destination system.</param>
        void MoveFile([NotNull] string sourceFileName, [NotNull] string destFileName, [CanBeNull] IFileSystem destinationSystem = null);

        /// <summary>
        /// Creates the directory.
        /// </summary>
        /// <param name="path">The path.</param>
        void CreateDirectory([NotNull] string path);

        /// <summary>
        /// Creates the file.
        /// </summary>
        /// <param name="fileName">Name of the file.</param>
        [NotNull]
        IFile CreateFile([NotNull] string fileName);


        /// <summary>
        /// Deletes the directory.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <param name="recursive">If set to <c>true</c>, recursive delete is performed.</param>
        void DeleteDirectory([NotNull] string path, bool recursive);
        
        /// <summary>
        /// Deletes the file.
        /// </summary>
        /// <param name="fileName">Name of the file to delete.</param>
        void DeleteFile([NotNull] string fileName);

        /// <summary>
        /// Finds the file.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <param name="pattern">The pattern.</param>
        /// <param name="recursive">If set to <c>true</c> [recursive].</param>
        [NotNull]
        IEnumerable<string> FindFile([NotNull] string path, [NotNull] string pattern, bool recursive);


        /// <summary>
        /// Gets the drives supported by this file system.
        /// </summary>
        [NotNull]
        IEnumerable<string> GetDrives();


        /// <summary>
        /// Gets the file or directory info.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <returns>The file / directory info.</returns>
        Info GetFileInfo([NotNull] string path);

        /// <summary>
        /// Opens the file.
        /// </summary>
        /// <param name="fileName">Name of the file.</param>
        /// <param name="write">If set to <c>true</c>, file is opened for writing.</param>
        [NotNull]
        IFile OpenFile([NotNull] string fileName, bool write);
    }
}