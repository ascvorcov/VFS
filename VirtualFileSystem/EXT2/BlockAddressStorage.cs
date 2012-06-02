#region Namespace Imports

using System;
using System.IO;

using VirtualFileSystem.Annotations;
using VirtualFileSystem.Interfaces;
using VirtualFileSystem.Utilities;

#endregion


namespace VirtualFileSystem.EXT2
{
    /// <summary>
    /// Implementation of direct / indirect / double-indirect pointers logic for Node.
    /// Flattens the model so that addresses can be viewed as an array of continuous blocks.
    /// </summary>
    public sealed class BlockAddressStorage
    {
        #region Constants and Fields

        private const uint _numberOfDirectBlocks = 12;
        private const uint _pointersInIndirectBlock = Constants.BlockSizeBytes / sizeof(uint);
        private const uint _blocksAddressedByOneNode = _numberOfDirectBlocks + _pointersInIndirectBlock + (_pointersInIndirectBlock * _pointersInIndirectBlock);

        private readonly Address _address;
        private readonly Address _globalBlocksStartAddress;
        private readonly IBlockAllocator _allocator;
        private readonly IDirectDiskAccess _diskAccess;

        private readonly uint[] _directBlocks = new uint[_numberOfDirectBlocks];
        private uint _doubleIndirectBlock;
        private uint _indirectBlock;
        private uint _numBlocksAllocated;

        #endregion


        #region Constructors and Destructors

        /// <summary>
        /// Initializes a new instance of the <see cref="BlockAddressStorage"/> class.
        /// </summary>
        /// <param name="diskAccess">The disk access interface.</param>
        /// <param name="allocator">The allocator interface.</param>
        /// <param name="storageAddress">The current instance start address in disk storage.</param>
        /// <param name="globalBlocksStartAddress">The global blocks start address, used to calculate offsets and block indexes.</param>
        public BlockAddressStorage(
            [NotNull] IDirectDiskAccess diskAccess,
            [NotNull] IBlockAllocator allocator,
            [NotNull] Address storageAddress,
            [NotNull] Address globalBlocksStartAddress)
        {
            Validate.ArgumentNotNull(diskAccess, "diskAccess");
            Validate.ArgumentNotNull(allocator, "allocator");
            Validate.ArgumentNotNull(storageAddress, "storageAddress");
            Validate.ArgumentNotNull(globalBlocksStartAddress, "globalBlocksStartAddress");

            _address = storageAddress;
            _diskAccess = diskAccess;
            _allocator = allocator;
            _globalBlocksStartAddress = globalBlocksStartAddress;
        }

        #endregion


        #region Properties

        /// <summary>
        /// Gets or sets the number of blocks allocated.
        /// Setter also reflects changed value in underlying disk storage.
        /// </summary>
        public uint NumBlocksAllocated
        {
            get
            {
                return _numBlocksAllocated;
            }

            private set
            {
                // immediately mirror value in storage.
                _numBlocksAllocated = value;
                ulong offset = _address.Value;
                _diskAccess.WriteUInt(ref offset, _numBlocksAllocated);
            }
        }

        #endregion


        #region Public Methods

        /// <summary>
        /// Allocate and add specified number of blocks to storage.
        /// </summary>
        /// <param name="blocksToAllocate">The number of blocks to allocate.</param>
        /// <exception cref="Exception">Maximum file size reached.</exception>
        /// <exception cref="IOException">Disk full.</exception>
        public void AddBlocks(uint blocksToAllocate)
        {
            if (blocksToAllocate == 0)
            {
                return;
            }

            if (blocksToAllocate + NumBlocksAllocated > _blocksAddressedByOneNode)
            {
                throw new Exception("Maximum file size reached.");
            }

            var newBlocks = _allocator.AllocateBlocks(blocksToAllocate);
            if (newBlocks == null || newBlocks.Length == 0 || newBlocks.Length != blocksToAllocate)
            {
                throw new IOException("Disk full.");
            }

            foreach (var block in newBlocks)
            {
                AddBlock(block);
            }
        }


        /// <summary>
        /// Free specified number of blocks at the end of sequence.
        /// </summary>
        /// <param name="number">The number of blocks to free.</param>
        /// <exception cref="ArgumentOutOfRangeException"><c>number</c> is out of range.</exception>
        public void FreeLastBlocks(uint number)
        {
            if (number > NumBlocksAllocated)
            {
                throw new ArgumentOutOfRangeException("number", "Cannot remove more blocks than allocated.");
            }

            uint blockIndex = NumBlocksAllocated;
            while (blockIndex -- > NumBlocksAllocated - number)
            {
                var addr = GetBlockStartAddress(blockIndex);
                _allocator.FreeBlocks(addr);

                if (blockIndex < _numberOfDirectBlocks)
                {
                    SetDirectBlock(blockIndex, 0);
                }
                else
                {
                    var indirectIndex = blockIndex - _numberOfDirectBlocks;
                    if (indirectIndex < _pointersInIndirectBlock)
                    {
                        SetIndexToIndirectBlock(_indirectBlock, indirectIndex * sizeof(uint), 0);
                        if (indirectIndex == 0) // last indirect block deallocated, free indirect block space
                        {
                            _allocator.FreeBlocks(new Address(GetBlockAddress(_indirectBlock)));
                            _indirectBlock = 0;
                        }
                    }
                    else
                    {
                        var doubleIndirectIndex = indirectIndex - _pointersInIndirectBlock;
                        var offsetIndirect = doubleIndirectIndex / _pointersInIndirectBlock;
                        var offsetDoubleIndirect = doubleIndirectIndex % _pointersInIndirectBlock;
                        var idx = GetIndexFromIndirectBlock(_doubleIndirectBlock, offsetIndirect * sizeof(uint));
                        SetIndexToIndirectBlock(idx, offsetDoubleIndirect * sizeof(uint), 0);

                        if (offsetDoubleIndirect == 0)
                        {
                            // deallocate indirect block
                            _allocator.FreeBlocks(new Address(GetBlockAddress(idx)));
                        }

                        if (doubleIndirectIndex == 0)
                        {
                            // deallocate main double indirect pointer
                            _allocator.FreeBlocks(new Address(GetBlockAddress(_doubleIndirectBlock)));
                            _doubleIndirectBlock = 0;
                        }
                    }
                }
            }

            NumBlocksAllocated -= number;
        }


        /// <summary>
        /// Gets the start address of block with specified index.
        /// Index is zero-based.
        /// </summary>
        /// <exception cref="IndexOutOfRangeException">This block is not allocated.</exception>
        [NotNull]
        public Address GetBlockStartAddress(uint index)
        {
            if (index >= NumBlocksAllocated)
            {
                throw new IndexOutOfRangeException("This block is not allocated.");
            }

            uint resultIndex;
            if (index < _numberOfDirectBlocks)
            {
                resultIndex = _directBlocks[index];
            }
            else if (index - _numberOfDirectBlocks < _pointersInIndirectBlock)
            {
                index -= _numberOfDirectBlocks;
                resultIndex = GetIndexFromIndirectBlock(_indirectBlock, index * sizeof(uint));
            }
            else
            {
                index -= _numberOfDirectBlocks;
                index -= _pointersInIndirectBlock;

                resultIndex = _doubleIndirectBlock;
                resultIndex = GetIndexFromIndirectBlock(resultIndex, (index / _pointersInIndirectBlock) * sizeof(uint));
                resultIndex = GetIndexFromIndirectBlock(resultIndex, (index % _pointersInIndirectBlock) * sizeof(uint));
            }

            return new Address(GetBlockAddress(resultIndex));
        }


        /// <summary>
        /// Loads current instance from disk.
        /// </summary>
        public void Load()
        {
            ulong offset = _address.Value;
            _numBlocksAllocated = _diskAccess.ReadUInt(ref offset);
            for (int i = 0; i < _directBlocks.Length; ++i)
            {
                _directBlocks[i] = _diskAccess.ReadUInt(ref offset);
            }

            _indirectBlock = _diskAccess.ReadUInt(ref offset);
            _doubleIndirectBlock = _diskAccess.ReadUInt(ref offset);
        }


        /// <summary>
        /// Saves the current instance on disk.
        /// </summary>
        public void Save()
        {
            ulong offset = _address.Value;
            _diskAccess.WriteUInt(ref offset, _numBlocksAllocated);
            for (int i = 0; i < _directBlocks.Length; ++i)
            {
                _diskAccess.WriteUInt(ref offset, _directBlocks[i]);
            }

            _diskAccess.WriteUInt(ref offset, _indirectBlock);
            _diskAccess.WriteUInt(ref offset, _doubleIndirectBlock);
        }

        #endregion


        #region Methods

        /// <summary>
        /// Adds the specified block to current storage.
        /// Updates number of allocated blocks, does indirect block allocation if necessary.
        /// </summary>
        private void AddBlock([NotNull] Address newBlock)
        {
            Validate.ArgumentNotNull(newBlock, "newBlock");

            if (NumBlocksAllocated < _numberOfDirectBlocks)
            {
                SetDirectBlock(NumBlocksAllocated++, GetBlockIndex(newBlock));
                return;
            }

            if (NumBlocksAllocated - _numberOfDirectBlocks < _pointersInIndirectBlock)
            {
                if (NumBlocksAllocated == _numberOfDirectBlocks)
                {
                    // allocate indirect block
                    _indirectBlock = AllocateIndirectBlock();
                    Save();
                }

                var indirectIndex = NumBlocksAllocated - _numberOfDirectBlocks;
                SetIndexToIndirectBlock(_indirectBlock, indirectIndex * sizeof(uint), GetBlockIndex(newBlock));
                NumBlocksAllocated++;
                return;
            }

            var index = NumBlocksAllocated - _numberOfDirectBlocks - _pointersInIndirectBlock;
            if (index == 0)
            {
                // allocate double indirect block
                _doubleIndirectBlock = AllocateIndirectBlock();
                Save();
            }

            var indirectPage = index / _pointersInIndirectBlock;
            var doubleIndirectPage = index % _pointersInIndirectBlock;
            if (doubleIndirectPage == 0)
            {
                var newDoubleIndirectBlock = AllocateIndirectBlock();
                SetIndexToIndirectBlock(_doubleIndirectBlock, indirectPage * sizeof(uint), newDoubleIndirectBlock);
            }

            var page = GetIndexFromIndirectBlock(_doubleIndirectBlock, indirectPage * sizeof(uint));
            SetIndexToIndirectBlock(page, doubleIndirectPage * sizeof(uint), GetBlockIndex(newBlock));
            NumBlocksAllocated++;
        }


        /// <summary>
        /// Allocates the new indirect block.
        /// </summary>
        /// <exception cref="IOException">Disk full.</exception>
        private uint AllocateIndirectBlock()
        {
            var indirectBlock = _allocator.AllocateBlocks(1);
            if (indirectBlock == null || indirectBlock.Length == 0)
            {
                throw new IOException("Disk full.");
            }

            return GetBlockIndex(indirectBlock[0]);
        }


        /// <summary>
        /// Gets the address of block with specified index.
        /// </summary>
        private ulong GetBlockAddress(uint blockIndex)
        {
            return _globalBlocksStartAddress.Value + (blockIndex * Constants.BlockSizeBytes);
        }


        /// <summary>
        /// Gets the index of the block. Calculated using global blocks start address.
        /// </summary>
        private uint GetBlockIndex([NotNull] Address address)
        {
            return (uint)((address.Value - _globalBlocksStartAddress.Value) / Constants.BlockSizeBytes);
        }


        /// <summary>
        /// Gets the value from indirect block on disk.
        /// </summary>
        private uint GetIndexFromIndirectBlock(uint indirectBlock, uint offset)
        {
            var addr = GetBlockAddress(indirectBlock) + offset;
            return _diskAccess.ReadUInt(ref addr);
        }


        /// <summary>
        /// Sets one of the direct blocks, reflecting value in disk storage.
        /// </summary>
        private void SetDirectBlock(uint index, uint value)
        {
            _directBlocks[index] = value;
            var address = _address.Value + (index + 1) * sizeof(uint);
            _diskAccess.WriteUInt(ref address, value);
        }


        /// <summary>
        /// Persist specified value on disk, in specified indirect block.
        /// </summary>
        private void SetIndexToIndirectBlock(uint indirectBlock, uint offset, uint value)
        {
            var addr = GetBlockAddress(indirectBlock) + offset;
            _diskAccess.WriteUInt(ref addr, value);
        }

        #endregion
    }
}