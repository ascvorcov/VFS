using System;

using VirtualFileSystem.Annotations;
using VirtualFileSystem.EXT2;


namespace VirtualFileSystem.Utilities
{
    /// <summary>
    /// Utility class to lock / unlock node using <see cref="IDisposable"/> syntax.
    /// </summary>
    internal sealed class NodeLocker : IDisposable
    {
        private readonly Node _node;
        private readonly bool _forWriting;
        private bool _disposed;


        /// <summary>
        /// Initializes a new instance of the <see cref="NodeLocker"/> class.
        /// </summary>
        public NodeLocker([CanBeNull] Node node, bool forWriting)
        {
            _node = node;
            _forWriting = forWriting;
        }

        /// <summary>
        /// Tries to cast underlying node to directory and return.
        /// </summary>
        [CanBeNull]
        public DirectoryNode Directory
        {
            get
            {
                return _node as DirectoryNode;
            }
        }

        /// <summary>
        /// Tries to cast underlying node to file and return.
        /// </summary>
        [CanBeNull]
        public FileNode File
        {
            get
            {
                return _node as FileNode;
            }
        }


        /// <summary>
        /// Gets the node.
        /// </summary>
        [CanBeNull]
        public Node Node
        {
            get
            {
                return _node;
            }
        }

        /// <summary>
        /// Gets a value indicating whether some underlying value exists.
        /// Will return <see langword="false"/> if current locker is based on <see langword="null"/> node.
        /// </summary>
        public bool Exists
        {
            get
            {
                return _node != null;
            }
        }

        /// <summary>
        /// Locks the specified node for reading or writing, depends on parameters.
        /// </summary>
        /// <param name="node">The node.</param>
        /// <param name="forWriting">If set to <c>true</c> node is locked for writing.</param>
        [NotNull]
        public static IDisposable Lock([NotNull] Node node, bool forWriting)
        {
            Validate.ArgumentNotNull(node, "node");

            if (forWriting)
            {
                node.LockWrite();
            }
            else
            {
                node.LockRead();
            }

            return new NodeLocker(node, forWriting);
        }

        /// <summary>
        /// Unlock underlying node. Several calls to dispose can be safely made.
        /// </summary>
        public void Dispose()
        {
            if (_disposed || _node == null)
            {
                return;
            }

            if (_forWriting)
            {
                _node.UnlockWrite();
            }
            else
            {
                _node.UnlockRead();
            }

            _disposed = true;
        }
    }
}