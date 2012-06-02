using System;

using VirtualFileSystem.Annotations;
using VirtualFileSystem.Interfaces;
using VirtualFileSystem.Utilities;


namespace VirtualFileSystem.EXT2
{
    /// <summary>
    /// Represents single block group in file system.
    /// That is, a fixed length set of blocks which has header with allocation info.
    /// </summary>
    public sealed class BlockGroup
    {
        private readonly object _allocationSync = new object();
        private readonly DataBitmap _blockBitmap;
        private readonly DataBitmap _nodeBitmap;
        private readonly BlockGroupDescriptor _descriptor;

        /// <summary>
        /// Number of blocks in group reserved to hold allocation info.
        /// </summary>
        public const ushort ReservedBlocks = 1 + Constants.NodeBitmapSizeBlocks + Constants.BlocksForNodeTable;

        /// <summary>
        /// Initializes a new instance of the <see cref="BlockGroup"/> class, with specified size.
        /// </summary>
        /// <param name="bitmapsStartAddress">The block group start address.</param>
        /// <param name="groupSizeInBlocks">The group size in blocks.</param>
        /// <exception cref="InvalidOperationException">This group size is not permitted.</exception>
        public BlockGroup([NotNull] Address bitmapsStartAddress, uint groupSizeInBlocks)
        {
            Validate.ArgumentNotNull(bitmapsStartAddress, "bitmapsStartAddress");

            if (groupSizeInBlocks <= ReservedBlocks)
            {
                throw new InvalidOperationException("This group size is not permitted.");
            }

            _blockBitmap = new DataBitmap(bitmapsStartAddress, Constants.BlocksPerGroup);
            _nodeBitmap = new DataBitmap(new Address(bitmapsStartAddress.Value + _blockBitmap.SizeBytes), Constants.BlocksForNodeTable * Constants.NodesPerBlock);

            _blockBitmap.ReserveBeginning(ReservedBlocks); // block bitmap include allocation for self and node table.

            _descriptor = new BlockGroupDescriptor
            {
                BitmapsAddress = bitmapsStartAddress,
                NumFreeBlocksInGroup = groupSizeInBlocks - ReservedBlocks,
                NumFreeNodesInGroup = Constants.NodesPerGroup
            };
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="BlockGroup"/> class,
        /// using passed parameters. Used when group is loaded from storage.
        /// </summary>
        /// <param name="descriptor">The descriptor.</param>
        internal BlockGroup([NotNull] BlockGroupDescriptor descriptor)
        {
            Validate.ArgumentNotNull(descriptor, "descriptor");

            _descriptor = descriptor;
            _blockBitmap = new DataBitmap(_descriptor.BitmapsAddress, Constants.BlocksPerGroup);
            _nodeBitmap = new DataBitmap(new Address(_descriptor.BitmapsAddress.Value + _blockBitmap.SizeBytes), Constants.BlocksForNodeTable * Constants.NodesPerBlock);
        }

        /// <summary>
        /// Address of start of node table. That is, table of nodes after two bitmaps.
        /// </summary>
        private ulong NodeTableStartAddress
        {
            get
            {
                return _blockBitmap.Address.Value + ((1 + Constants.NodeBitmapSizeBlocks) * Constants.BlockSizeBytes);
            }
        }

        /// <summary>
        /// Gets the group start address.
        /// </summary>
        private ulong GroupStartAddress
        {
            get
            {
                return _blockBitmap.Address.Value;
            }
        }

        /// <summary>
        /// Gets the block group descriptor, which is stored in master record.
        /// </summary>
        [NotNull]
        public BlockGroupDescriptor Descriptor
        {
            get
            {
                return new BlockGroupDescriptor
                {
                    BitmapsAddress = new Address(_descriptor.BitmapsAddress.Value),
                    NumFreeBlocksInGroup = _descriptor.NumFreeBlocksInGroup,
                    NumFreeNodesInGroup = _descriptor.NumFreeNodesInGroup
                };
            }
        }

        /// <summary>
        /// Allocates the new block if possible.
        /// If block cannot be allocated, <see langword="null"/> is returned.
        /// This operation is synchronized, two threads cannot do allocation at the same time.
        /// </summary>
        [CanBeNull]
        public Address AllocateNewBlock()
        {
            lock (_allocationSync)
            {
                if (_descriptor.NumFreeBlocksInGroup == 0)
                    return null;

                var idx = _blockBitmap.AllocateFirstFree();
                if (idx == -1)
                {
                    return null;
                }
                _descriptor.NumFreeBlocksInGroup--;
                return new Address(GroupStartAddress + (uint)idx * Constants.BlockSizeBytes);
            }
        }

        /// <summary>
        /// Allocates the new node if possible.
        /// If node cannot be allocated, <see langword="null"/> is returned.
        /// This operation is synchronized, two threads cannot do allocation at the same time.
        /// </summary>
        [CanBeNull]
        public Address AllocateNewNode()
        {
            lock (_allocationSync)
            {
                if (_descriptor.NumFreeNodesInGroup == 0)
                    return null;

                var idx = _nodeBitmap.AllocateFirstFree();
                if (idx == -1)
                {
                    return null;
                }

                _descriptor.NumFreeNodesInGroup--;
                var address = new Address(NodeTableStartAddress + (uint)idx * Constants.NodeSize);
                return address;
            }
        }


        /// <summary>
        /// Frees the specified block.
        /// Will throw if address does not belong to current group, or not aligned on block boundary.
        /// </summary>
        /// <param name="address">The address to deallocate.</param>
        /// <exception cref="InvalidOperationException">Address belong to another group.</exception>
        /// <exception cref="InvalidOperationException">Block address must be aligned on block boundary.</exception>
        /// <exception cref="InvalidOperationException">Cannot deallocate reserved bitmap blocks - this is done by group itself.</exception>
        /// <exception cref="InvalidOperationException">Cannot deallocate same block twice.</exception>
        public void FreeBlock([NotNull] Address address)
        {
            Validate.ArgumentNotNull(address, "address");

            lock (_allocationSync)
            {
                if (!address.InRange(_descriptor.BitmapsAddress, Constants.GroupSizeBytes))
                {
                    throw new InvalidOperationException("Address belong to another group.");
                }

                var offset = (uint)(address.Value - GroupStartAddress);
                if (offset % Constants.BlockSizeBytes != 0)
                {
                    throw new InvalidOperationException("Block address must be aligned on block boundary.");
                }

                uint blockIndex = offset / Constants.BlockSizeBytes;

                if (blockIndex < ReservedBlocks)
                {
                    throw new InvalidOperationException("Cannot deallocate reserved bitmap blocks - this is done by group itself.");
                }

                if (!_blockBitmap.Deallocate((int)blockIndex))
                {
                    throw new InvalidOperationException("Cannot deallocate same block twice.");
                }

                _descriptor.NumFreeBlocksInGroup++;
            }
        }


        /// <summary>
        /// Frees the specified node.
        /// Will throw if address does not belong to current node table, or not aligned on node boundary.
        /// </summary>
        /// <param name="address">The node address.</param>
        /// <exception cref="InvalidOperationException">Address belong to another group.</exception>
        /// <exception cref="InvalidOperationException">Node address must be aligned on node boundary.</exception>
        /// <exception cref="InvalidOperationException">Cannot deallocate same node twice.</exception>
        public void FreeNode([NotNull] Address address)
        {
            Validate.ArgumentNotNull(address, "address");

            lock (_allocationSync)
            {
                if (!address.InRange(_descriptor.BitmapsAddress, Constants.GroupSizeBytes))
                {
                    throw new InvalidOperationException("Address belong to another group.");
                }

                var offset = (uint)(address.Value - NodeTableStartAddress);
                if (offset % Constants.NodeSize != 0)
                {
                    throw new InvalidOperationException("Node address must be aligned on node boundary.");
                }

                uint element = offset / Constants.NodeSize;

                if (!_nodeBitmap.Deallocate((int)element))
                {
                    throw new InvalidOperationException("Cannot deallocate same node twice.");
                }

                _descriptor.NumFreeNodesInGroup++;
            }
        }


        /// <summary>
        /// Loads group bitmaps from the specified offset.
        /// </summary>
        public void Load(ulong offset, [NotNull] IDirectDiskAccess reader)
        {
            Validate.ArgumentNotNull(reader, "reader");

            _blockBitmap.Load(ref offset, reader);
            _nodeBitmap.Load(ref offset, reader);
        }


        /// <summary>
        /// Persists the group bitmaps at the specified offset.
        /// </summary>
        public void Save(ulong offset, [NotNull] IDirectDiskAccess writer)
        {
            Validate.ArgumentNotNull(writer, "writer");

            _blockBitmap.Save(ref offset, writer);
            _nodeBitmap.Save(ref offset, writer);
        }
    }
}