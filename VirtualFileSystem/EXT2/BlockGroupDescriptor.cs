using VirtualFileSystem.Annotations;
using VirtualFileSystem.Interfaces;
using VirtualFileSystem.Utilities;


namespace VirtualFileSystem.EXT2
{
    /// <summary>
    /// Block group descriptor information. That is, chunk of block group info,
    /// which is stored in master record, and loaded when system is mounted.
    /// </summary>
    public sealed class BlockGroupDescriptor
    {
        /// <summary>
        /// Gets or sets the bitmaps address.
        /// </summary>
        public Address BitmapsAddress
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the number of free blocks in group.
        /// </summary>
        public uint NumFreeBlocksInGroup
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the number of free nodes in group.
        /// </summary>
        public uint NumFreeNodesInGroup
        {
            get;
            set;
        }

        /// <summary>
        /// Saves the descriptor at specified offset.
        /// </summary>
        public ulong Save(ulong offset, [NotNull] IDirectDiskAccess writer)
        {
            Validate.ArgumentNotNull(writer, "writer");

            writer.WriteULong(ref offset, BitmapsAddress.Value);
            writer.WriteUInt(ref offset, NumFreeBlocksInGroup);
            writer.WriteUInt(ref offset, NumFreeNodesInGroup);
            return offset;
        }

        /// <summary>
        /// Loads the descriptor from specified offset.
        /// </summary>
        public ulong Load(ulong offset, [NotNull] IDirectDiskAccess reader)
        {
            Validate.ArgumentNotNull(reader, "reader");

            BitmapsAddress = new Address(reader.ReadULong(ref offset));
            NumFreeBlocksInGroup = reader.ReadUInt(ref offset);
            NumFreeNodesInGroup = reader.ReadUInt(ref offset);
            return offset;
        }

        /// <summary>
        /// Raw size of descriptor, in bytes.
        /// </summary>
        public const uint RawSizeBytes = 16;
    }
}