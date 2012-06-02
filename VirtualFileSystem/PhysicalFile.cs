using System;
using System.IO;

using VirtualFileSystem.Annotations;
using VirtualFileSystem.Interfaces;


namespace VirtualFileSystem
{
    /// <summary>
    /// Implementation of <see cref="IFile"/> interface for <see cref="FileStream"/> object.
    /// </summary>
    internal sealed class PhysicalFile : IFile
    {
        private bool _disposed;
        private readonly FileStream _stream;
        private readonly FileInfo _info;


        /// <summary>
        /// Initializes a new instance of the <see cref="PhysicalFile"/> class.
        /// </summary>
        public PhysicalFile([NotNull] FileStream stream, [NotNull] FileInfo info)
        {
            Validate.ArgumentNotNull(stream, "stream");

            _stream = stream;
            _info = info;
        }


        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            Close();
        }


        /// <summary>
        /// Gets the file creation time.
        /// </summary>
        public DateTime CreationTime
        {
            get
            {
                return _info.CreationTime;
            }
        }

        /// <summary>
        /// Gets the last file modification time.
        /// </summary>
        public DateTime LastModificationTime
        {
            get
            {
                return _info.LastWriteTime;
            }
        }

        /// <summary>
        /// Sets the new size of the file. Can shrink and grow file.
        /// If file size was reduced, and current position is beyond file size,
        /// current position is relocated to end of file.
        /// </summary>
        public void SetFileSize(ulong size)
        {
            _stream.SetLength((long)size);
        }


        /// <summary>
        /// Gets the size of the file.
        /// </summary>
        public ulong GetFileSize()
        {
            return (ulong)_stream.Length;
        }


        /// <summary>
        /// Writes the data to file. File is automatically grown,
        /// if write beyond current size is made.
        /// </summary>
        public void WriteData([NotNull] byte[] data)
        {
            Validate.ArgumentNotNull(data, "data");

            _stream.Write(data, 0, data.Length);
        }


        /// <summary>
        /// Reads the portion of file data, advancing read pointer.
        /// Will return empty array if at end of file.
        /// </summary>
        [NotNull]
        public byte[] ReadData(uint length)
        {
            var buffer = new byte[length];
            var read = _stream.Read(buffer, 0, (int)length);
            if (read < length)
            {
                var newBuffer = new byte[read];
                Array.Copy(buffer, newBuffer, read);
                buffer = newBuffer;
            }
            return buffer;
        }


        /// <summary>
        /// Sets the current read or write position.
        /// Can be set from beginning or end of file.
        /// Offset can be negative if seeking from current position.
        /// </summary>
        public ulong SetPosition(long offset, SeekOrigin origin)
        {
            return (ulong)_stream.Seek(offset, origin);
        }


        /// <summary>
        /// Gets a value indicating whether this <see cref="IFile"/> can written to.
        /// </summary>
        public bool CanWrite
        {
            get
            {
                return _stream.CanWrite;
            }
        }

        /// <summary>
        /// Closes the current file, releasing resources.
        /// </summary>
        public void Close()
        {
            if (_disposed)
                return;
            
            _disposed = true;
            _stream.Close();
        }
    }
}