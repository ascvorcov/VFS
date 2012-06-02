using VirtualFileSystem.Interfaces;


namespace FileClient
{
    /// <summary>
    /// Disk drive model, physical or virtual.
    /// </summary>
    public class Drive
    {
        /// <summary>
        /// Gets or sets the order of disk drive creation.
        /// Required to deallocate in correct order.
        /// </summary>
        public int CreationOrder
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the public visible name of drive.
        /// </summary>
        public string Name
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the drive root folder name. Can be different from Name.
        /// </summary>
        public string Root
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the underlying file system interface, associated with this drive.
        /// </summary>
        public IFileSystem AssociatedFileSystem
        {
            get;
            set;
        }
    }
}