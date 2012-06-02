using System;
using System.IO;

using VirtualFileSystem.Annotations;
using VirtualFileSystem.Interfaces;


namespace VirtualFileSystem
{
    /// <summary>
    /// Implementation of Stream class using <see cref="IFile"/> interface as a target.
    /// </summary>
    internal sealed class VirtualStream : Stream
    {
        private readonly IFile _file;


        /// <summary>
        /// Initializes a new instance of the <see cref="VirtualStream"/> class.
        /// </summary>
        /// <param name="file">The file.</param>
        public VirtualStream([NotNull] IFile file)
        {
            Validate.ArgumentNotNull(file, "file");

            _file = file;
        }


        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
            {
                _file.Close();
            }
        }

        public override void Flush()
        {
        }


        public override long Seek(long offset, SeekOrigin origin)
        {
            return (long)_file.SetPosition(offset, origin);
        }


        public override void SetLength(long value)
        {
            _file.SetFileSize((ulong)value);
        }


        public override int Read(byte[] buffer, int offset, int count)
        {
            var data = _file.ReadData((uint)count);

            Array.ConstrainedCopy(data, 0, buffer, offset, data.Length);
            return data.Length;
        }


        public override void Write(byte[] buffer, int offset, int count)
        {
            var newBuffer = new byte[count];
            Array.ConstrainedCopy(buffer, offset, newBuffer, 0, count);
            _file.WriteData(newBuffer);
        }


        public override bool CanRead
        {
            get
            {
                return true;
            }
        }

        public override bool CanSeek
        {
            get
            {
                return true;
            }
        }

        public override bool CanWrite
        {
            get
            {
                return _file.CanWrite;
            }
        }

        public override long Length
        {
            get
            {
                return (long)_file.GetFileSize();
            }
        }

        public override long Position
        {
            get
            {
                return (long)_file.SetPosition(0, SeekOrigin.Current);
            }
            set
            {
                _file.SetPosition(value, SeekOrigin.Begin);
            }
        }
    }
}
