#region Namespace Imports

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

using VirtualFileSystem.Annotations;
using VirtualFileSystem.Interfaces;
using VirtualFileSystem.Utilities;

#endregion


namespace VirtualFileSystem.EXT2
{
    /// <summary>
    /// Represents base class for node. Contains synchronization logic, address storage and field common for files and folders.
    /// </summary>
    public abstract class Node : IDisposable
    {
        #region Constants and Fields

        /// <summary>
        /// Number of milliseconds to wait for lock to be released.
        /// </summary>
        public const int LockWaitThreshold = 1000;

        /// <summary>
        /// Disk access interface, exposed to derived classes.
        /// </summary>
        protected readonly IDirectDiskAccess DiskAccess;

        /// <summary>
        /// Block address storage, exposed to derived classes.
        /// </summary>
        protected readonly BlockAddressStorage Storage;

        private readonly Address _address;
        private readonly ReaderWriterLockSlim _nodeLock = new ReaderWriterLockSlim();

        private DateTime _createdDate = DateTime.Now;
        private DateTime _modifiedDate = DateTime.Now;
        private ulong _size; // will mean file size, bytes for file node, and number of entries for directory node.

        #endregion


        #region Constructors and Destructors

        /// <summary>
        /// Initializes a new instance of the <see cref="Node"/> class.
        /// </summary>
        /// <param name="blockAllocator">The block allocator.</param>
        /// <param name="diskAccess">The disk access.</param>
        /// <param name="address">The address.</param>
        /// <param name="globalBlockStartAddress">The global block start address.</param>
        public Node(
            [NotNull] IBlockAllocator blockAllocator,
            [NotNull] IDirectDiskAccess diskAccess,
            [NotNull] Address address,
            [NotNull] Address globalBlockStartAddress)
        {
            Validate.ArgumentNotNull(blockAllocator, "blockAllocator");
            Validate.ArgumentNotNull(diskAccess, "diskAccess");
            Validate.ArgumentNotNull(address, "address");
            Validate.ArgumentNotNull(globalBlockStartAddress, "globalBlockStartAddress");

            _address = address;
            DiskAccess = diskAccess;
            Storage = new BlockAddressStorage(
                diskAccess, blockAllocator, new Address(_address.Value + 25), globalBlockStartAddress);
        }

        #endregion


        #region Properties

        /// <summary>
        /// Gets the start address of current node.
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
        /// Gets the creation date of this node.
        /// </summary>
        public DateTime CreatedDate
        {
            get
            {
                return _createdDate;
            }
        }

        /// <summary>
        /// Gets the last modification date of this node.
        /// </summary>
        public DateTime ModifiedDate
        {
            get
            {
                return _modifiedDate;
            }
        }

        /// <summary>
        /// Gets a value indicating whether this <see cref="Node"/> is directory.
        /// Overloaded in derived classes.
        /// </summary>
        public virtual bool IsDirectory
        {
            get
            {
                return false;
            }
        }

        /// <summary>
        /// Gets all blocks allocated by this node.
        /// </summary>
        [NotNull]
        public IEnumerable<Address> NodeAllocatedBlocks
        {
            get
            {
                for (uint i = 0; i < Storage.NumBlocksAllocated; ++i)
                {
                    yield return Storage.GetBlockStartAddress(i);
                }
            }
        }

        /// <summary>
        /// Gets the size of this node.
        /// Depending on node implementation, may mean different things.
        /// </summary>
        protected ulong Size
        {
            get
            {
                return _size;
            }
        }

        #endregion


        #region Public Methods

        /// <summary>
        /// Dispose current node, releases node lock.
        /// </summary>
        public void Dispose()
        {
            _nodeLock.Dispose();
        }


        /// <summary>
        /// Loads current node information from storage.
        /// </summary>
        /// <exception cref="IOException">Attempting to load directory info into non-directory node.</exception>
        public virtual void Load()
        {
            ulong offset = _address.Value;
            bool isDirectory = DiskAccess.ReadByte(ref offset) == 1;
            if (isDirectory != IsDirectory)
            {
                throw new IOException("Attempting to load directory info into non-directory node.");
            }

            _size = DiskAccess.ReadULong(ref offset);
            _createdDate = new DateTime((long)DiskAccess.ReadULong(ref offset));
            _modifiedDate = new DateTime((long)DiskAccess.ReadULong(ref offset));
            Storage.Load();
        }


        /// <summary>
        /// Locks this node for reading.
        /// </summary>
        /// <exception cref="InvalidOperationException">Cannot acquire read lock</exception>
        public void LockRead()
        {
            if (!_nodeLock.TryEnterReadLock(LockWaitThreshold))
            {
                throw new InvalidOperationException("Cannot acquire read lock");
            }
        }

        /// <summary>
        /// Tries to lock node for reading.
        /// Returns <see langword="false"/> if failed.
        /// </summary>
        public bool TryLockRead()
        {
            if (_nodeLock.IsWriteLockHeld || _nodeLock.IsReadLockHeld)
            {
                return false;
            }

            return _nodeLock.TryEnterReadLock(LockWaitThreshold);
        }


        /// <summary>
        /// Locks this node for writing.
        /// </summary>
        /// <exception cref="InvalidOperationException">Cannot acquire write lock.</exception>
        public void LockWrite()
        {
            if (!_nodeLock.TryEnterWriteLock(LockWaitThreshold))
            {
                throw new InvalidOperationException("Cannot acquire write lock.");
            }
        }


        /// <summary>
        /// Saves current node information into storage.
        /// </summary>
        public virtual void Save()
        {
            VerifyWriterLock();
            ulong offset = _address.Value;
            DiskAccess.WriteByte(ref offset, (byte)(IsDirectory ? 1 : 0));
            DiskAccess.WriteULong(ref offset, _size);
            DiskAccess.WriteULong(ref offset, (ulong)_createdDate.Ticks);
            DiskAccess.WriteULong(ref offset, (ulong)_modifiedDate.Ticks);
            Storage.Save();
        }


        /// <summary>
        /// Unlocks this node if it was locked for reading.
        /// </summary>
        public void UnlockRead()
        {
            _nodeLock.ExitReadLock();
        }


        /// <summary>
        /// Unlocks this node if it was locked for writing.
        /// </summary>
        public void UnlockWrite()
        {
            _nodeLock.ExitWriteLock();
        }

        #endregion


        #region Methods

        /// <summary>
        /// Updates the modified date field of node.
        /// Changes are reflected in storage.
        /// </summary>
        protected void UpdateModifiedDate()
        {
            VerifyWriterLock();
            _modifiedDate = DateTime.Now;
            var addressOfModifiedDateField = _address.Value + sizeof(byte) + (sizeof(ulong) * 2);
            // skip 1-byte flag and two ulong fields
            DiskAccess.WriteULong(ref addressOfModifiedDateField, (ulong)_modifiedDate.Ticks);
        }


        /// <summary>
        /// Updates the size field of node.
        /// Changes are reflected in storage.
        /// </summary>
        protected void UpdateSize(ulong newSize)
        {
            VerifyWriterLock();
            _size = newSize;
            var addressOfSizeField = _address.Value + sizeof(byte); // skip 1-byte flag field
            DiskAccess.WriteULong(ref addressOfSizeField, _size);
        }


        /// <summary>
        /// Verifies the writer lock, throws if not acquired.
        /// </summary>
        /// <exception cref="InvalidOperationException">Write lock must be acquired first to perform this operation.</exception>
        private void VerifyWriterLock()
        {
            if (!_nodeLock.IsWriteLockHeld)
            {
                throw new InvalidOperationException("Write lock must be acquired first to perform this operation.");
            }
        }

        #endregion
    }
}