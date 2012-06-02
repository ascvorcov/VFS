#region Namespace Imports

using System;
using System.Collections.Generic;

using VirtualFileSystem.Annotations;

#endregion


namespace VirtualFileSystem
{
    /// <summary>
    /// Represents helper class, wrapper around raw path/name string.
    /// Contains functionality to break paths, similar to Path framework class.
    /// </summary>
    internal sealed class VirtualFileName
    {
        #region Constants and Fields

        public const string Separator = @"\";

        private readonly string[] _segments;

        #endregion


        #region Constructors and Destructors

        /// <summary>
        /// Initializes a new instance of the <see cref="VirtualFileName"/> class.
        /// </summary>
        /// <param name="fileName">Name of the file.</param>
        /// <exception cref="ArgumentException">Malformed path.</exception>
        public VirtualFileName([NotNull] string fileName)
        {
            // todo: canonicalize path, removing '.' and '..'
            Validate.ArgumentNotNull(fileName, "fileName");

            if (!fileName.StartsWith(Separator, StringComparison.InvariantCultureIgnoreCase))
            {
                throw new ArgumentException("Malformed path.");
            }

            _segments = fileName.Split(new[] { Separator }, StringSplitOptions.RemoveEmptyEntries);
        }

        #endregion


        #region Properties

        /// <summary>
        /// Gets all path segments.
        /// </summary>
        [NotNull]
        public IEnumerable<string> AllSegments
        {
            get
            {
                return _segments;
            }
        }

        /// <summary>
        /// Gets all segments except last. Usually represents parent directory.
        /// </summary>
        [NotNull]
        public IEnumerable<string> AllSegmentsExceptLast
        {
            get
            {
                for (int i = 0; i < _segments.Length - 1; ++i)
                {
                    yield return _segments[i];
                }
            }
        }


        /// <summary>
        /// Gets the full path with name in string form.
        /// Always begins from \ symbol.
        /// </summary>
        [NotNull]
        public string FullName
        {
            get
            {
                return Separator + string.Join(Separator, _segments);
            }
        }

        /// <summary>
        /// Gets only file name (or directory name), last segment in path.
        /// </summary>
        [NotNull]
        public string Name
        {
            get
            {
                return _segments[_segments.Length - 1];
            }
        }

        /// <summary>
        /// Gets the full path without name (last segment).
        /// </summary>
        [NotNull]
        public string Path
        {
            get
            {
                return Separator + string.Join(Separator, AllSegmentsExceptLast);
            }
        }

        #endregion


        #region Public Methods

        /// <summary>
        /// Combines the specified path with specified segment, appending separator if needed.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <param name="segment">The segment to append.</param>
        /// <returns>
        /// Returns new combined path.
        /// </returns>
        [NotNull]
        public static string Combine([NotNull] string path, [NotNull] string segment)
        {
            Validate.ArgumentNotNull(path, "path");
            Validate.ArgumentNotNull(segment, "segment");

            if (path.EndsWith(Separator))
            {
                return path + segment;
            }

            return path + Separator + segment;
        }

        #endregion
    }
}