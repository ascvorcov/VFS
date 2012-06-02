#region Namespace Imports

using System.Collections;

using VirtualFileSystem.Annotations;
using VirtualFileSystem.Interfaces;
using VirtualFileSystem.Utilities;

#endregion


namespace VirtualFileSystem.EXT2
{
    /// <summary>
    /// Wrapper for bit array, which holds allocation information for some address space.
    /// Can persist and load its state. Not thread-safe.
    /// </summary>
    public sealed class DataBitmap
    {
        #region Constants and Fields

        private readonly Address _address;
        private BitArray _bitmap;

        #endregion


        #region Constructors and Destructors

        /// <summary>
        /// Initializes a new instance of the <see cref="DataBitmap"/> class.
        /// </summary>
        /// <param name="address">The bitmap start address.</param>
        /// <param name="length">The length of required bitmap, in items.</param>
        public DataBitmap([NotNull] Address address, int length)
        {
            Validate.ArgumentNotNull(address, "address");

            _address = address;
            _bitmap = new BitArray(length);
        }

        #endregion


        #region Properties

        /// <summary>
        /// Gets the start address of current bitmap.
        /// </summary>
        [NotNull]
        public Address Address
        {
            get
            {
                return _address;
            }
        }

        /// <summary>
        /// Gets the size of bitmap, in bytes.
        /// </summary>
        public uint SizeBytes
        {
            get
            {
                return (uint)_bitmap.Length / 8;
            }
        }

        #endregion


        #region Public Methods

        /// <summary>
        /// Allocates the first free item, and returns its index.
        /// Returns -1 if no more space.
        /// </summary>
        public int AllocateFirstFree()
        {
            for (int i = 0; i < _bitmap.Length; ++i)
            {
                if (_bitmap[i])
                {
                    continue;
                }

                _bitmap[i] = true;
                return i;
            }

            return -1;
        }


        /// <summary>
        /// Deallocates the element with specified index.
        /// If element is already deallocated, returns <see langword="false"/>.
        /// </summary>
        public bool Deallocate(int element)
        {
            var isAllocated = _bitmap[element];
            if (isAllocated)
            {
                _bitmap[element] = false;
                return true;
            }

            return false;
        }


        /// <summary>
        /// Loads the bitmap from specified offset.
        /// </summary>
        public void Load(ref ulong offset, [NotNull] IDirectDiskAccess reader)
        {
            Validate.ArgumentNotNull(reader, "reader");

            var bitData = new byte[_bitmap.Length / 8];
            reader.ReadBytes(ref offset, bitData, 0, (uint)bitData.Length);
            _bitmap = new BitArray(bitData);
        }


        /// <summary>
        /// Bulk reserve of specified number of elements at the beginning of bitmap.
        /// </summary>
        public void ReserveBeginning(int numberOfElements)
        {
            for (int i = 0; i < numberOfElements; ++i)
            {
                _bitmap[i] = true;
            }
        }


        /// <summary>
        /// Saves the bitmap at the specified offset.
        /// </summary>
        public void Save(ref ulong offset, [NotNull] IDirectDiskAccess writer)
        {
            Validate.ArgumentNotNull(writer, "writer");

            var bitData = new byte[_bitmap.Length / 8];
            _bitmap.CopyTo(bitData, 0);
            writer.WriteBytes(ref offset, bitData, 0, (uint)bitData.Length);
        }

        #endregion
    }
}