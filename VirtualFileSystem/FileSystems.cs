using VirtualFileSystem.Annotations;
using VirtualFileSystem.Interfaces;
using VirtualFileSystem.Utilities;


namespace VirtualFileSystem
{
    /// <summary>
    /// Facade for file systems manipulation.
    /// Hides specifics of internal classes, exposing only interfaces required to mount/unmount volumes.
    /// </summary>
    public static class FileSystems
    {
        /// <summary>
        /// Provides reference to implementation of current system physical FS.
        /// </summary>
        [NotNull]
        public static IFileSystem Real
        {
            get
            {
                return new PhysicalFileSystem();
            }
        }

        /// <summary>
        /// Creates the new virtual volume, with specified size and name.
        /// Destination file system must be specified.
        /// </summary>
        /// <param name="fileName">Name of the file.</param>
        /// <param name="volumeSize">Size of the volume.</param>
        /// <param name="destination">The destination.</param>
        [NotNull]
        public static IFileSystem CreateNewVolume([NotNull] string fileName, ulong volumeSize, [NotNull] IFileSystem destination)
        {
            Validate.ArgumentNotNull(fileName, "fileName");
            Validate.ArgumentNotNull(destination, "destination");

            var file = destination.CreateFile(fileName);
            file.SetFileSize(volumeSize); // preliminary allocation. mounting system will increase this size slightly.

            if (destination is PhysicalFileSystem)
            {
                var stream = new VirtualStream(file);
                var diskAccess = new DirectDiskAccess(stream);
                var vfs = new VirtualFileSystem(diskAccess, volumeSize);
                file.SetFileSize(vfs.RealVolumeSize);
                return vfs;
            }
            else
            {
                // for virtual files mounting file system directly.
                // this allows parallel access from any thread, not restricted by writer lock in creating thread.
                var destVfs = (VirtualFileSystem)destination;
                file.Close();
                var diskAccess = destVfs.MountDisk(fileName);
                var vfs = new VirtualFileSystem(diskAccess, volumeSize);
                diskAccess.SetFileSize(vfs.RealVolumeSize);
                return vfs;
            }

        }

        /// <summary>
        /// Mounts and returns reference to the existing virtual volume in specified file system.
        /// </summary>
        /// <param name="fileName">Name of the file.</param>
        /// <param name="source">The source.</param>
        [NotNull]
        public static IFileSystem MountVirtual([NotNull] string fileName, [NotNull] IFileSystem source)
        {
            Validate.ArgumentNotNull(fileName, "fileName");
            Validate.ArgumentNotNull(source, "source");

            if (source is PhysicalFileSystem)
            {
                var file = source.OpenFile(fileName, true);
                var stream = new VirtualStream(file);
                var diskAccess = new DirectDiskAccess(stream);
                return new VirtualFileSystem(diskAccess);
            }

            var vfs = (VirtualFileSystem)source;
            var access = vfs.MountDisk(fileName);
            return new VirtualFileSystem(access);
        }
    }
}
