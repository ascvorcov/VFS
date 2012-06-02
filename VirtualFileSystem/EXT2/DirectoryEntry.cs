#region Namespace Imports

using System;
using System.Text;

using VirtualFileSystem.Annotations;
using VirtualFileSystem.Interfaces;
using VirtualFileSystem.Utilities;

#endregion


namespace VirtualFileSystem.EXT2
{
    /// <summary>
    /// Represents single entry in directory. 
    /// That is, item used to locate real Node by its name.
    /// </summary>
    public sealed class DirectoryEntry
    {
        #region Constants and Fields

        private uint _sizeBytes;

        #endregion


        #region Properties

        /// <summary>
        /// Gets or sets the entry self address.
        /// </summary>
        [NotNull]
        public Address EntrySelfAddress
        {
            get;
            set;
        }

        /// <summary>
        /// Gets the entry size in bytes.
        /// </summary>
        public uint EntrySizeBytes
        {
            get
            {
                // entry size on disk. Computed once, does not change if entry is resurrected,
                // and name is changed to shorter one.
                if (_sizeBytes == 0)
                {
                    _sizeBytes = CalculateEntrySize();
                }

                return _sizeBytes;
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether this <see cref="DirectoryEntry"/> is deleted.
        /// </summary>
        public bool IsDeleted
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets a value indicating whether this <see cref="DirectoryEntry"/> references a directory.
        /// </summary>
        public bool IsDirectory
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the name of entry.
        /// </summary>
        public string Name
        {
            get;
            set;
        }

        /// <summary>
        /// Address of next entry in list.
        /// </summary>
        [CanBeNull]
        public Address Next
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the node address this entry points to.
        /// </summary>
        public Address NodeAddress
        {
            get;
            set;
        }

        #endregion


        #region Public Methods

        /// <summary>
        /// Calculates the size of the entry, without updating <see cref="EntrySizeBytes"/> property.
        /// </summary>
        public uint CalculateEntrySize()
        {
            var baseLength = 1 + 8 + 8 + (uint)Name.Length * 2;
            return (uint)Align(baseLength);
        }


        /// <summary>
        /// Loads entry from the specified offset.
        /// </summary>
        public void Load(ulong offset, [NotNull] IDirectDiskAccess reader)
        {
            Validate.ArgumentNotNull(reader, "reader");

            EntrySelfAddress = new Address(offset);

            var b = reader.ReadByte(ref offset);
            IsDeleted = (b & 0x01) == 0x01;
            IsDirectory = (b & 0x02) == 0x02;

            NodeAddress = new Address(reader.ReadULong(ref offset));
            var nextAddr = reader.ReadULong(ref offset);
            Next = nextAddr != 0 ? new Address(nextAddr) : null;

            var nameLength = reader.ReadByte(ref offset);

            var buffer = new byte[nameLength * 2];
            reader.ReadBytes(ref offset, buffer, 0, (uint)buffer.Length);
            Name = Encoding.Unicode.GetString(buffer);
        }


        /// <summary>
        /// Resurrects current entry from deleted state, assigning it new name, address and type.
        /// </summary>
        /// <param name="name">The new name to assign to entry. Length must be less or equal to current name.</param>
        /// <param name="isDirectory">If set to <c>true</c> entry is marked as directory.</param>
        /// <param name="address">The new address of node.</param>
        /// <exception cref="InvalidOperationException">Cannot resurrect alive directory entry.</exception>
        /// <exception cref="InvalidOperationException">Resurrected entry is too small to hold new entry.</exception>
        public void Resurrect([NotNull] string name, bool isDirectory, [NotNull] Address address)
        {
            Validate.ArgumentNotNull(name, "name");
            Validate.ArgumentNotNull(address, "address");

            if (!IsDeleted)
            {
                throw new InvalidOperationException("Cannot resurrect alive directory entry.");
            }

            if (name.Length > Name.Length)
            {
                throw new InvalidOperationException("Resurrected entry is too small to hold new entry.");
            }

            IsDeleted = false;
            IsDirectory = isDirectory;
            NodeAddress = address;
            Name = name;
        }


        /// <summary>
        /// Saves current entry at its internal address.
        /// </summary>
        public void Save([NotNull] IDirectDiskAccess writer)
        {
            Validate.ArgumentNotNull(writer, "writer");

            var offset = EntrySelfAddress.Value;

            byte flags = 0;
            if (IsDeleted)
            {
                flags |= 0x01;
            }

            if (IsDirectory)
            {
                flags |= 0x02;
            }

            writer.WriteByte(ref offset, flags);
            writer.WriteULong(ref offset, NodeAddress.Value);
            writer.WriteULong(ref offset, Next == null ? 0 : Next.Value);
            writer.WriteByte(ref offset, (byte)Name.Length);
            var nameData = Encoding.Unicode.GetBytes(Name);
            writer.WriteBytes(ref offset, nameData, 0, (uint)nameData.Length);
        }

        #endregion


        #region Methods

        /// <summary>
        /// Aligns specified value on 4 bytes boundary.
        /// </summary>
        private ulong Align(ulong value)
        {
            var tail = value % 4;
            if (tail == 0) // already aligned
            {
                return value;
            }

            return value + (4 - tail);
        }

        #endregion
    }
}