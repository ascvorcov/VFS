#region Namespace Imports

using System;
using System.Collections.Generic;

using VirtualFileSystem.Annotations;
using VirtualFileSystem.Interfaces;
using VirtualFileSystem.Utilities;

#endregion


namespace VirtualFileSystem.EXT2
{
    /// <summary>
    /// Represents file node, which holds actual file data.
    /// </summary>
    public sealed class FileNode : Node
    {
        #region Constructors and Destructors

        /// <summary>
        /// Initializes a new instance of the <see cref="FileNode"/> class.
        /// </summary>
        /// <param name="allocator">The allocator.</param>
        /// <param name="diskAccess">The disk access.</param>
        /// <param name="address">The address.</param>
        /// <param name="globalBlockStartAddress">The global block start address.</param>
        private FileNode(
            [NotNull] IBlockAllocator allocator,
            [NotNull] IDirectDiskAccess diskAccess,
            [NotNull] Address address,
            [NotNull] Address globalBlockStartAddress)
            : base(allocator, diskAccess, address, globalBlockStartAddress)
        {
            Validate.ArgumentNotNull(allocator, "allocator");
            Validate.ArgumentNotNull(diskAccess, "diskAccess");
            Validate.ArgumentNotNull(address, "address");
            Validate.ArgumentNotNull(globalBlockStartAddress, "globalBlockStartAddress");
        }

        #endregion


        #region Properties

        /// <summary>
        /// Gets the size of the file.
        /// </summary>
        public ulong FileSize
        {
            get
            {
                return Size;
            }
        }

        #endregion


        #region Public Methods

        /// <summary>
        /// Creates new file node at specified address, saving it in storage.
        /// </summary>
        [NotNull]
        public static FileNode Create([NotNull] IBlockAllocator allocator, [NotNull] IDirectDiskAccess diskAccess, [NotNull] Address address, [NotNull] Address globalBlockStartAddress)
        {
            Validate.ArgumentNotNull(allocator, "allocator");
            Validate.ArgumentNotNull(diskAccess, "diskAccess");
            Validate.ArgumentNotNull(address, "address");
            Validate.ArgumentNotNull(globalBlockStartAddress, "globalBlockStartAddress");

            var node = new FileNode(allocator, diskAccess, address, globalBlockStartAddress);
            using (NodeLocker.Lock(node, true))
            {
                node.Save();
                return node;
            }
        }


        /// <summary>
        /// Loads existing file node from disk.
        /// </summary>
        [NotNull]
        public static FileNode Load([NotNull] IBlockAllocator allocator, [NotNull] IDirectDiskAccess diskAccess, [NotNull] Address address, [NotNull] Address globalBlockStartAddress)
        {
            Validate.ArgumentNotNull(allocator, "allocator");
            Validate.ArgumentNotNull(diskAccess, "diskAccess");
            Validate.ArgumentNotNull(address, "address");
            Validate.ArgumentNotNull(globalBlockStartAddress, "globalBlockStartAddress");

            var node = new FileNode(allocator, diskAccess, address, globalBlockStartAddress);
            node.Load();
            return node;
        }


        /// <summary>
        /// Reads the file data.
        /// </summary>
        [NotNull]
        public byte[] ReadData(ulong position, uint count)
        {
            if (position >= FileSize)
            {
                return new byte[0];
            }

            if (position + count > FileSize)
            {
                count = (uint)(FileSize - position);
            }

            if (count == 0)
            {
                return new byte[0];
            }

            var blockIndex = (uint)(position / Constants.BlockSizeBytes);
            var offset = (uint)(position % Constants.BlockSizeBytes);
            Address address = Storage.GetBlockStartAddress(blockIndex);

            var chunkSize = Constants.BlockSizeBytes - offset;
            var toRead = Math.Min(chunkSize, count);

            var buffer = new byte[count];
            var dataAddress = address.Value + offset;
            var bytesRead = DiskAccess.ReadBytes(ref dataAddress, buffer, 0, toRead);
            uint buffferOffset = bytesRead;
            count -= bytesRead;

            while (count > 0)
            {
                address = Storage.GetBlockStartAddress(++blockIndex);
                var pos = address.Value;
                toRead = Math.Min(count, Constants.BlockSizeBytes);
                bytesRead = DiskAccess.ReadBytes(ref pos, buffer, buffferOffset, toRead);
                count -= bytesRead;
                buffferOffset += bytesRead;
            }

            return buffer;
        }


        /// <summary>
        /// Sets the size of the file, doing allocation if necessary.
        /// </summary>
        public void SetFileSize(ulong newSize)
        {
            var requiredNumberOfBlocks =
                (uint)(newSize / Constants.BlockSizeBytes + (newSize % Constants.BlockSizeBytes == 0 ? 0u : 1u));
            bool growing = newSize > FileSize;
            if (growing)
            {
                uint numberOfBlocksToAllocate = requiredNumberOfBlocks - Storage.NumBlocksAllocated;
                if (numberOfBlocksToAllocate > 0)
                {
                    Storage.AddBlocks(numberOfBlocksToAllocate);
                }
            }
            else // shrinking
            {
                // check if we're passing block boundary
                var numberOfBlocksToDeallocate = Storage.NumBlocksAllocated - requiredNumberOfBlocks;
                if (numberOfBlocksToDeallocate > 0)
                {
                    Storage.FreeLastBlocks(numberOfBlocksToDeallocate);
                }
            }

            UpdateSize(newSize);
            UpdateModifiedDate();
        }


        /// <summary>
        /// Writes the data into file, resizing as needed.
        /// </summary>
        public ulong WriteData(ulong position, [NotNull] byte[] data)
        {
            Validate.ArgumentNotNull(data, "data");

            ulong newSize = position + (uint)data.Length;
            if (newSize > FileSize)
            {
                SetFileSize(newSize); // force allocation of new space. will throw if not enough memory.
            }

            var startBlockIndex = (uint)(position / Constants.BlockSizeBytes);
            var offset = (uint)(position % Constants.BlockSizeBytes);

            var sparseWriter = new SparseWriter(DiskAccess);
            var requiredNumberOfBlocks = SparseWriter.GetNumberOfBlocksRequired((uint)data.Length, offset);

            var blocks = new List<Address>();
            for (uint i = 0; i < requiredNumberOfBlocks; ++i)
            {
                blocks.Add(Storage.GetBlockStartAddress(startBlockIndex + i));
            }

            sparseWriter.WriteData(data, blocks.ToArray(), offset);

            UpdateModifiedDate();
            return newSize;
        }

        #endregion
    }
}