using System;
using System.IO;

using VirtualFileSystem.Annotations;
using VirtualFileSystem.EXT2;
using VirtualFileSystem.Interfaces;


namespace VirtualFileSystem
{
    /// <summary>
    /// Represents virtual file, wrapper around <see cref="FileNode"/>, implementation of <see cref="IFile"/> interface.
    /// </summary>
    internal sealed class VirtualFile : IFile
    {
        private ulong _position;
        private FileNode _node;
        private readonly bool _canWrite;


        /// <summary>
        /// Initializes a new instance of the <see cref="VirtualFile"/> class.
        /// </summary>
        /// <param name="node">The node.</param>
        /// <param name="canWrite">If set to <c>true</c>, file is opened for writing.</param>
        /// <exception cref="ArgumentException">Cannot open directory as file.</exception>
        public VirtualFile([NotNull] FileNode node, bool canWrite)
        {
            Validate.ArgumentNotNull(node, "node");

            if (node.IsDirectory)
            {
               throw new ArgumentException("Cannot open directory as file."); 
            }

            _node = node;
            _canWrite = canWrite;
        }

        /// <summary>
        /// Dispose current virtual file, removing node reference.
        /// Can be called several times, doesn't throw.
        /// </summary>
        public void Dispose()
        {
            if (_node == null)
            {
                return;
            }

            Close();
        }


        /// <summary>
        /// Gets the file creation time.
        /// </summary>
        public DateTime CreationTime
        {
            get
            {
                CheckDisposed();
                return _node.CreatedDate;
            }
        }

        /// <summary>
        /// Gets the file last modification time.
        /// </summary>
        public DateTime LastModificationTime
        {
            get
            {
                CheckDisposed();
                return _node.ModifiedDate;
            }
        }

        /// <summary>
        /// Sets the new size of the file. Can shrink and grow file.
        /// If file size was reduced, and current position is beyond file size,
        /// current position is relocated to end of file.
        /// </summary>
        /// <exception cref="InvalidOperationException">File was opened for reading only.</exception>
        public void SetFileSize(ulong size)
        {
            CheckDisposed();
            if (!_canWrite)
            {
                throw new InvalidOperationException("File was opened for reading only.");
            }

            _node.SetFileSize(size);
            if (_position > size)
            {
                //relocate pointer to end of file
                _position = size;
            }
        }


        /// <summary>
        /// Gets the size of the file.
        /// </summary>
        public ulong GetFileSize()
        {
            CheckDisposed();
            return _node.FileSize;
        }


        /// <summary>
        /// Writes the data to file. File is automatically grown,
        /// if write beyond current size is made.
        /// </summary>
        /// <param name="data">The data to write.</param>
        /// <exception cref="InvalidOperationException">File was opened for reading only.</exception>
        public void WriteData([NotNull] byte[] data)
        {
            Validate.ArgumentNotNull(data, "data");

            CheckDisposed();
            if (!_canWrite)
            {
                throw new InvalidOperationException("File was opened for reading only.");
            }

            _position = _node.WriteData(_position, data);
        }


        /// <summary>
        /// Reads the portion of file data, advancing read pointer.
        /// Will return empty array if at end of file.
        /// </summary>
        [NotNull]
        public byte[] ReadData(uint length)
        {
            CheckDisposed();
            var res = _node.ReadData(_position, length);
            _position += (ulong)res.Length;
            return res;
        }


        /// <summary>
        /// Sets the current read or write position.
        /// Can be set from beginning or end of file.
        /// Offset can be negative if seeking from current position.
        /// </summary>
        /// <param name="offset">Offset to set.</param>
        /// <param name="origin">Where to start from.</param>
        /// <returns>New position.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><c>offset</c> is out of range.</exception>
        /// <exception cref="IOException">Seek outside file boundaries.</exception>
        public ulong SetPosition(long offset, SeekOrigin origin)
        {
            CheckDisposed();
            if (offset < 0 && origin != SeekOrigin.Current)
            {
                throw new ArgumentOutOfRangeException("offset", "Offset can be negative only when seeking from current position.");
            }

            var ex = new IOException("Seek outside file boundaries.");
            if (origin == SeekOrigin.Begin)
            {
                if ((ulong)offset > _node.FileSize)
                {
                    throw ex;
                }

                _position = (ulong)offset;
            }
            else if (origin == SeekOrigin.Current)
            {
                if (offset < 0)
                {
                    var absoluteOffset = (ulong)Math.Abs(offset);
                    if (absoluteOffset > _position)
                    {
                        throw ex;
                    }

                    _position -= absoluteOffset;
                }
                else
                {
                    if (_position + (ulong)offset > _node.FileSize)
                    {
                        throw ex;
                    }

                    _position += (ulong)offset;
                }
            }
            else if (origin == SeekOrigin.End)
            {
                if ((ulong)offset > _node.FileSize)
                {
                    throw ex;
                }

                _position = _node.FileSize - (ulong)offset - 1;
            }

            return _position;
        }


        /// <summary>
        /// Gets a value indicating whether this <see cref="IFile"/> can written to.
        /// </summary>
        public bool CanWrite
        {
            get
            {
                CheckDisposed();
                return _canWrite;
            }
        }

        /// <summary>
        /// Closes the current file, releasing resources.
        /// </summary>
        public void Close()
        {
            CheckDisposed();
            if (_canWrite)
            {
                _node.UnlockWrite();
            }
            else
            {
                _node.UnlockRead();
            }

            // node should not be disposed. It is located in master cache.
            _node = null;
        }

        /// <summary>
        /// Checks if current file is disposed.
        /// </summary>
        /// <exception cref="InvalidOperationException">File is closed.</exception>
        private void CheckDisposed()
        {
            if (_node == null)
            {
                throw new InvalidOperationException("File is closed.");
            }
        }
    }
}