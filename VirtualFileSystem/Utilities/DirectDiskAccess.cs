using System.IO;

using VirtualFileSystem.Annotations;
using VirtualFileSystem.Interfaces;


namespace VirtualFileSystem.Utilities
{
    /// <summary>
    /// Implementation of direct disk access using Stream as input/output.
    /// All operations in this class are thread safe, state is not persisted, file is repositioned on every call.
    /// </summary>
    public sealed class DirectDiskAccess : IDirectDiskAccess
    {
        private readonly object _sync = new object();
        private readonly Stream _stream;
        private readonly BinaryReader _reader;
        private readonly BinaryWriter _writer;

        /// <summary>
        /// Initializes a new instance of the <see cref="DirectDiskAccess"/> class.
        /// </summary>
        /// <param name="stream">The stream.</param>
        public DirectDiskAccess([NotNull] Stream stream)
        {
            Validate.ArgumentNotNull(stream, "stream");

            _stream = stream;
            _reader = new BinaryReader(stream);
            _writer = new BinaryWriter(stream);
        }

        /// <summary>
        /// Dispose current instance and underlying stream.
        /// </summary>
        public void Dispose()
        {
            _stream.Dispose();
        }

        /// <summary>
        /// Reads single byte at specified address.
        /// Address is updated to reflect new position.
        /// </summary>
        public byte ReadByte(ref ulong address)
        {
            lock (_sync)
            {
                Seek(address);
                address += sizeof(byte);
                return _reader.ReadByte();
            }
        }

        /// <summary>
        /// Reads the unsigned integer at specified address.
        /// Address is updated to reflect new position.
        /// </summary>
        public uint ReadUInt(ref ulong address)
        {
            lock (_sync)
            {
                Seek(address);
                address += sizeof(uint);
                return _reader.ReadUInt32();
            }
        }

        /// <summary>
        /// Reads the unsigned long at specified address.
        /// Address is updated to reflect new position.
        /// </summary>
        public ulong ReadULong(ref ulong address)
        {
            lock (_sync)
            {
                Seek(address);
                address += sizeof(ulong);
                return _reader.ReadUInt64();
            }
        }


        /// <summary>
        /// Writes the unsigned integer at specified address.
        /// Address is updated to reflect new position.
        /// </summary>
        public void WriteUInt(ref ulong address, uint value)
        {
            lock (_sync)
            {
                Seek(address);
                address += sizeof(uint);
                _writer.Write(value);
            }
        }


        /// <summary>
        /// Writes the unsigned long at specified address.
        /// Address is updated to reflect new position.
        /// </summary>
        public void WriteULong(ref ulong address, ulong value)
        {
            lock (_sync)
            {
                Seek(address);
                address += sizeof(ulong);
                _writer.Write(value);
            }
        }

        /// <summary>
        /// Writes the single byte at specified address.
        /// Address is updated to reflect new position.
        /// </summary>
        public void WriteByte(ref ulong address, byte data)
        {
            lock (_sync)
            {
                Seek(address);
                address += sizeof(byte);
                _writer.Write(data);
            }
        }


        /// <summary>
        /// Reads the bytes array starting from specified address.
        /// Address is updated to reflect new position.
        /// Returns actual number of bytes read.
        /// </summary>
        public uint ReadBytes(ref ulong address, [NotNull] byte[] data, uint offset, uint count)
        {
            lock (_sync)
            {
                Seek(address);
                var ret = (uint)_stream.Read(data, (int)offset, (int)count);
                address += sizeof(byte) * ret;
                return ret;
            }
        }


        /// <summary>
        /// Writes the array of bytes at specified position.
        /// Address is updated to reflect new position.
        /// </summary>
        public void WriteBytes(ref ulong address, [NotNull] byte[] data, uint offset, uint count)
        {
            lock (_sync)
            {
                Seek(address);
                address += sizeof(byte) * count;
                _stream.Write(data, (int)offset, (int)count);
            }
        }

        /// <summary>
        /// Reposition to the specified address.
        /// </summary>
        private void Seek(ulong address)
        {
            _stream.Seek((long)address, SeekOrigin.Begin);
        }
    }
}