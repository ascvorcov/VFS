#region Namespace Imports

using System;
using System.Linq;

using VirtualFileSystem.Annotations;
using VirtualFileSystem.EXT2;
using VirtualFileSystem.Interfaces;

#endregion


namespace VirtualFileSystem.Utilities
{
    /// <summary>
    /// Implementation of writer which can write continuous byte data 
    /// into specified block buckets, which have different addresses.
    /// </summary>
    public sealed class SparseWriter
    {
        #region Constants and Fields

        private readonly IDirectDiskAccess _diskAccess;

        #endregion


        #region Constructors and Destructors

        /// <summary>
        /// Initializes a new instance of the <see cref="SparseWriter"/> class.
        /// </summary>
        /// <param name="diskAccess">The direct disk access interface.</param>
        public SparseWriter([NotNull] IDirectDiskAccess diskAccess)
        {
            Validate.ArgumentNotNull(diskAccess, "diskAccess");

            _diskAccess = diskAccess;
        }

        #endregion


        #region Public Methods

        /// <summary>
        /// Gets the number of blocks required to store data of specified length at specified offset.
        /// </summary>
        public static uint GetNumberOfBlocksRequired(uint dataLength, uint offset)
        {
            var remainingBlockSpace = Constants.BlockSizeBytes - offset;
            if (remainingBlockSpace > dataLength)
            {
                return 1;
            }

            dataLength -= remainingBlockSpace; // remaining data length
            var body = dataLength / Constants.BlockSizeBytes;
            var tail = dataLength % Constants.BlockSizeBytes;

            return 1 + body + (tail > 0 ? 1u : 0u);
        }


        /// <summary>
        /// Writes the specified data into specified address blocks, starting at specified offset in first block.
        /// </summary>
        public void WriteData([NotNull] byte[] data, [NotNull] Address[] blocks, uint offset)
        {
            Validate.ArgumentNotNull(data, "data");
            Validate.ArgumentNotNull(blocks, "blocks");

            if (data.Length == 0)
            {
                return;
            }

            WriteInto(blocks.First(), Head(data, offset), offset);
            WriteInto(blocks, Body(data, offset));
            WriteInto(blocks.Last(), Tail(data, offset));
        }

        #endregion


        #region Methods

        /// <summary>
        /// Extracts the 'body' of data (without head and tail) as a set of ranges.
        /// </summary>
        [NotNull]
        private static Range[] Body([NotNull] byte[] data, uint offset)
        {
            Validate.ArgumentNotNull(data, "data");

            var headSize = Constants.BlockSizeBytes - offset;
            if (headSize > data.Length)
            {
                return new Range[0];
            }

            var remainingLength = data.Length - headSize;
            var bodyPages = remainingLength / Constants.BlockSizeBytes;
            var result = new Range[bodyPages]; // can be 0
            for (int i = 0; i < bodyPages; ++i)
            {
                result[i] = new Range(data, headSize, Constants.BlockSizeBytes);
                headSize += Constants.BlockSizeBytes;
            }

            return result;
        }

        /// <summary>
        /// Extracts the 'head' of data as a Range.
        /// That is, chunk which comes first if byte buffer does not fit into first block.
        /// </summary>
        [NotNull]
        private static Range Head([NotNull] byte[] data, uint offset)
        {
            Validate.ArgumentNotNull(data, "data");

            var remainingBlockSpace = Constants.BlockSizeBytes - offset;
            if (remainingBlockSpace > data.Length)
            {
                return new Range(data, 0, (uint)data.Length);
            }

            return new Range(data, 0, remainingBlockSpace);
        }


        /// <summary>
        /// Extracts the tail of data as Range, the chunk which comes last and does not cover entire block.
        /// </summary>
        [NotNull]
        private static Range Tail([NotNull] byte[] data, uint offset)
        {
            Validate.ArgumentNotNull(data, "data");

            var headSize = Constants.BlockSizeBytes - offset;
            if (headSize >= data.Length)
            {
                return Range.Empty;
            }

            var len = (uint)data.Length;
            uint tailSize = ((len - headSize) % Constants.BlockSizeBytes);
            return new Range(data, len - tailSize, tailSize);
        }


        /// <summary>
        /// Writes specified range at specified address and offset.
        /// </summary>
        private void WriteInto([NotNull] Address address, Range range, uint offset = 0u)
        {
            Validate.ArgumentNotNull(address, "address");

            if (range.IsEmpty)
            {
                return;
            }

            ulong addr = address.Value + offset;
            _diskAccess.WriteBytes(ref addr, range.Data, range.StartIndex, range.Size);
        }


        /// <summary>
        /// Writes specified set of ranges at specified addresses.
        /// </summary>
        /// <exception cref="ArgumentException">number of body blocks must be same as number of ranges</exception>
        private void WriteInto([NotNull] Address[] blocks, [NotNull] Range[] ranges)
        {
            Validate.ArgumentNotNull(blocks, "blocks");
            Validate.ArgumentNotNull(ranges, "ranges");

            if (ranges.Length == 0)
            {
                return;
            }

            // 1 or 2 blocks will be reserved for head/tail
            if (ranges.Length >= blocks.Length || ranges.Length < blocks.Length - 2)
            {
                throw new ArgumentException("number of body blocks must be same as number of ranges");
            }

            for (int i = 0; i < ranges.Length; ++i)
            {
                WriteInto(blocks[i + 1], ranges[i]);
            }
        }

        #endregion


        private struct Range
        {
            #region Constants and Fields

            [CanBeNull]
            public readonly byte[] Data;

            public readonly uint Size;
            public readonly uint StartIndex;

            #endregion


            #region Constructors and Destructors

            public Range([CanBeNull] byte[] data, uint index, uint size)
            {
                Data = data;
                StartIndex = index;
                Size = size;
            }

            #endregion


            #region Properties

            public static Range Empty
            {
                get
                {
                    return new Range(null, 0, 0);
                }
            }

            public bool IsEmpty
            {
                get
                {
                    return Size == 0;
                }
            }

            #endregion
        }
    }
}