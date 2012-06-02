#region Namespace Imports

using VirtualFileSystem.Annotations;
using VirtualFileSystem.EXT2;

#endregion


namespace VirtualFileSystem.Utilities
{
    /// <summary>
    /// Immutable wrapper around absolute address value in virtual file system.
    /// Used to distinguish between offset and real address, also contains some commonly used operations.
    /// </summary>
    public sealed class Address
    {
        #region Constants and Fields

        private readonly ulong _address;

        #endregion


        #region Constructors and Destructors

        /// <summary>
        /// Initializes a new instance of the <see cref="Address"/> class.
        /// </summary>
        /// <param name="address">The address.</param>
        public Address(ulong address)
        {
            _address = address;
        }

        #endregion


        #region Properties

        /// <summary>
        /// Returns the new instance of address, aligned on block boundary.
        /// </summary>
        [NotNull]
        public Address AlignOnBlockBoundary
        {
            get
            {
                var remainder = _address % Constants.BlockSizeBytes;
                if (remainder == 0) //already aligned
                {
                    return this;
                }

                var padding = Constants.BlockSizeBytes - remainder;
                return new Address(_address + padding);
            }
        }


        /// <summary>
        /// Gets the actual value of address.
        /// </summary>
        public ulong Value
        {
            get
            {
                return _address;
            }
        }

        #endregion


        #region Public Methods

        /// <summary>
        /// Offset address by specified number of blocks forward, and return new instance.
        /// </summary>
        [NotNull]
        public Address AddBlocks(uint blockCount)
        {
            return new Address(_address + (blockCount * Constants.BlockSizeBytes));
        }


        /// <summary>
        /// Checks if specified address falls in range starting from current address,
        /// and up to <param name="size"/> bytes forward.
        /// </summary>
        public bool InRange([NotNull] Address start, ulong size)
        {
            Validate.ArgumentNotNull(start, "start");

            return _address >= start.Value && _address < start.Value + size;
        }

        #endregion
    }
}