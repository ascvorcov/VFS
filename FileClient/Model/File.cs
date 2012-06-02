
using System;


namespace FileClient
{
    /// <summary>
    /// File model, single file / directory in file system.
    /// </summary>
    public class File
    {
        private readonly char[] _separators = new[] { '\\', '/' };

        /// <summary>
        /// Gets the visible name of the file, without path.
        /// </summary>
        public string Name
        {
            get
            {
                if (NavUp)
                {
                    return "..";
                }

                var idx = FullName.LastIndexOfAny(_separators);
                return FullName.Substring(idx + 1);
            }
        }

        /// <summary>
        /// Gets the folder name which contains current file.
        /// </summary>
        public string ContainingFolder
        {
            get
            {
                var name = FullName.TrimEnd(_separators);
                var idx = name.LastIndexOfAny(_separators);

                return name.Substring(0, idx + 1);
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether this <see cref="File"/> instance represents directory.
        /// </summary>
        public bool IsDirectory
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the full name of file, including path.
        /// </summary>
        public string FullName
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the size of file. Always 0 for directories.
        /// </summary>
        public string Size
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets a value indicating whether current file instance is special, and represents 'navigate up' item.
        /// </summary>
        public bool NavUp
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the created date.
        /// </summary>
        public string Created
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the modified date.
        /// </summary>
        public string Modified
        {
            get;
            set;
        }
    }
}