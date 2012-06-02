using System;


namespace VirtualFileSystem.Interfaces
{
    /// <summary>
    /// Defines a stateless direct disk access interface.
    /// Provides abstraction for file system implementation.
    /// </summary>
    public interface IDirectDiskAccess : IDisposable
    {
        /// <summary>
        /// Reads single byte at specified address.
        /// Address is updated to reflect new position.
        /// </summary>
        byte ReadByte(ref ulong address);

        /// <summary>
        /// Reads the unsigned integer at specified address.
        /// Address is updated to reflect new position.
        /// </summary>
        uint ReadUInt(ref ulong address);

        /// <summary>
        /// Reads the unsigned long at specified address.
        /// Address is updated to reflect new position.
        /// </summary>
        ulong ReadULong(ref ulong address);

        /// <summary>
        /// Writes the unsigned integer at specified address.
        /// Address is updated to reflect new position.
        /// </summary>
        void WriteUInt(ref ulong address, uint value);

        /// <summary>
        /// Writes the unsigned long at specified address.
        /// Address is updated to reflect new position.
        /// </summary>
        void WriteULong(ref ulong address, ulong value);

        /// <summary>
        /// Writes the single byte at specified address.
        /// Address is updated to reflect new position.
        /// </summary>
        void WriteByte(ref ulong address, byte data);

        /// <summary>
        /// Reads the bytes array starting from specified address.
        /// Address is updated to reflect new position.
        /// Returns actual number of bytes read.
        /// </summary>
        uint ReadBytes(ref ulong address, byte[] data, uint offset, uint count);

        /// <summary>
        /// Writes the array of bytes at specified position.
        /// Address is updated to reflect new position.
        /// </summary>
        void WriteBytes(ref ulong address, byte[] data, uint offset, uint count);
    }
}