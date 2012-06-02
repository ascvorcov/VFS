#region Namespace Imports

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;

using VirtualFileSystem.Annotations;
using VirtualFileSystem.Interfaces;
using VirtualFileSystem.Utilities;

#endregion


namespace VirtualFileSystem.EXT2
{
    /// <summary>
    /// Represents the master record, main record in storage header,
    /// which holds system information and block group descriptors.
    /// Serves as a main point of allocation and synchronized node storage.
    /// </summary>
    public sealed class MasterRecord : IBlockAllocator, IDisposable
    {
        #region Constants and Fields

        private readonly object _sync = new object();
        private readonly IDirectDiskAccess _diskAccess;
        private readonly ConcurrentDictionary<ulong, Node> _nodeMap = new ConcurrentDictionary<ulong, Node>();
        
        private ulong _freeSpaceBlocks;
        private BlockGroup[] _groupsReserved;
        private bool _disposed;

        #endregion


        #region Constructors and Destructors

        /// <summary>
        /// Initializes a new instance of the <see cref="MasterRecord"/> class.
        /// </summary>
        /// <param name="diskAccess">The disk access interface.</param>
        public MasterRecord([NotNull] IDirectDiskAccess diskAccess)
        {
            Validate.ArgumentNotNull(diskAccess, "diskAccess");

            _diskAccess = diskAccess;
        }

        #endregion


        #region Properties

        /// <summary>
        /// Gets the global address of blocks start.
        /// </summary>
        [NotNull]
        public Address GlobalBlocksStartAddress
        {
            get
            {
                return _groupsReserved[0].Descriptor.BitmapsAddress;
            }
        }

        /// <summary>
        /// Gets the root directory node of file system.
        /// </summary>
        [NotNull]
        public DirectoryNode RootNode
        {
            get;
            private set;
        }


        /// <summary>
        /// Gets the raw size of the volume in bytes.
        /// </summary>
        public ulong VolumeSize
        {
            get;
            private set;
        }

        #endregion


        #region Public Methods

        /// <summary>
        /// Creates the new master record, formatting it to address storage of specified size.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException"><c>Volume size should be on block size boundary.</c> is out of range.</exception>
        [NotNull]
        public static MasterRecord CreateNewVolume([NotNull] IDirectDiskAccess access, ulong size)
        {
            Validate.ArgumentNotNull(access, "access");

            if (size % Constants.BlockSizeBytes != 0)
            {
                throw new ArgumentOutOfRangeException("Volume size should be on block size boundary.");
            }

            if (size <= BlockGroup.ReservedBlocks * Constants.BlockSizeBytes)
            {
                throw new ArgumentOutOfRangeException(string.Format("Min volume size is {0}", Constants.BlockSizeBytes * (BlockGroup.ReservedBlocks + 1)));
            }

            var totalBlocks = (uint)(size / Constants.BlockSizeBytes);
            uint numberOfFullGroups = totalBlocks / Constants.BlocksPerGroup;
            uint remainingBlocks = totalBlocks % Constants.BlocksPerGroup;
            if (remainingBlocks > BlockGroup.ReservedBlocks)
            {
                numberOfFullGroups++;
            }

            var rec = new MasterRecord(access);
            rec._groupsReserved = new BlockGroup[numberOfFullGroups];
            var offset = new Address(32 + (BlockGroupDescriptor.RawSizeBytes * numberOfFullGroups));
            offset = offset.AlignOnBlockBoundary;

            uint totalFreeBlocks = 0;
            for (uint i = 0; i < numberOfFullGroups; ++i)
            {
                bool last = (i == numberOfFullGroups - 1 && remainingBlocks > 0);
                var groupSize = last ? remainingBlocks : Constants.BlocksPerGroup;
                var newGroup = new BlockGroup(offset, groupSize);
                offset = offset.AddBlocks(groupSize);
                totalFreeBlocks += newGroup.Descriptor.NumFreeBlocksInGroup;
                rec._groupsReserved[i] = newGroup;
            }

            rec._freeSpaceBlocks = totalFreeBlocks;
            rec.VolumeSize = offset.Value;
            rec.RootNode = rec.CreateDirectoryNode("\\", null);
            return rec;
        }


        /// <summary>
        /// Creates the new directory node with specified name and parent.
        /// Parent can be <see langword="null"/> for root node.
        /// </summary>
        /// <exception cref="IOException">Disk full. Cannot allocate node.</exception>
        /// <exception cref="InvalidOperationException">Node already exists.</exception>
        [NotNull]
        public DirectoryNode CreateDirectoryNode([NotNull] string name, [CanBeNull] DirectoryNode parent)
        {
            Validate.ArgumentNotNull(name, "name");

            var addr = AllocateNode();
            if (addr == null)
            {
                throw new IOException("Disk full. Cannot allocate node.");
            }

            var node = DirectoryNode.Create(parent, addr, this, _diskAccess);
            if (parent != null)
            {
                parent.AddChildEntry(name, true, node.Address);
            }

            if (!_nodeMap.TryAdd(addr.Value, node))
            {
                throw new InvalidOperationException("Node already exists.");
            }

            return node;
        }


        /// <summary>
        /// Creates the new file node, adding it to parent.
        /// </summary>
        /// <param name="fileName">Name of the file.</param>
        /// <param name="parent">The parent directory node.</param>
        /// <returns>Created file node.</returns>
        /// <exception cref="IOException">Disk full. Cannot allocate node.</exception>
        /// <exception cref="InvalidOperationException">Node already exists.</exception>
        [NotNull]
        public FileNode CreateFileNode([NotNull] string fileName, [NotNull] DirectoryNode parent)
        {
            Validate.ArgumentNotNull(fileName, "fileName");
            Validate.ArgumentNotNull(parent, "parent");

            var newFileNode = AllocateNode();
            if (newFileNode == null)
            {
                throw new IOException("Disk full. Cannot allocate node.");
            }

            var fileNode = FileNode.Create(this, _diskAccess, newFileNode, GlobalBlocksStartAddress);
            parent.AddChildEntry(fileName, false, newFileNode);

            if (!_nodeMap.TryAdd(newFileNode.Value, fileNode))
            {
                throw new InvalidOperationException("Node already exists.");
            }

            return fileNode;
        }


        /// <summary>
        /// Dispose current master record, saving state into storage.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            // todo: it is still possible for another thread to hold master record,
            // and do creation of nodes.
            var nodeList = _nodeMap.Values.ToList();
            _nodeMap.Clear();
            foreach (var node in nodeList)
            {
                // save all accessed nodes, optimizing used space, deallocating if necessary.
                using (NodeLocker.Lock(node, true))
                {
                    node.Save();
                }

                node.Dispose();
            }

            Save();
        }


        /// <summary>
        /// Frees the node and all its allocated blocks.
        /// Not a thread-safe method, we don't control how many times same node
        /// can be passed for deallocation.
        /// </summary>
        /// <param name="node">The node to deallocate.</param>
        /// <exception cref="InvalidOperationException">Attempting to deallocate unknown node.</exception>
        public void FreeNodeAndAllAllocatedBlocks([NotNull] Node node)
        {
            Validate.ArgumentNotNull(node, "node");

            IBlockAllocator allocator = this;
            allocator.FreeBlocks(node.NodeAllocatedBlocks.ToArray());

            var group = GetGroupByAddress(node.Address);
            group.FreeNode(node.Address);

            Node value;
            if (!_nodeMap.TryRemove(node.Address.Value, out value))
            {
                throw new InvalidOperationException("Attempting to deallocate unknown node.");
            }
        }


        /// <summary>
        /// Gets the directory node, loading it from storage if not exists in cache.
        /// </summary>
        /// <param name="addr">The node address.</param>
        /// <returns>The directory node.</returns>
        public DirectoryNode GetDirectoryNode([NotNull] Address addr)
        {
            // should throw if attemt to retrieve file is made.
            Validate.ArgumentNotNull(addr, "addr");

            return (DirectoryNode)_nodeMap.GetOrAdd(addr.Value, f => DirectoryNode.Load(addr, this, _diskAccess));
        }


        /// <summary>
        /// Gets the file node, loading it from storage if not exists in cache.
        /// </summary>
        /// <param name="addr">The node address.</param>
        /// <returns>The file node.</returns>
        public FileNode GetFileNode([NotNull] Address addr)
        {
            // should throw if attemt to retrieve directory is made.
            Validate.ArgumentNotNull(addr, "addr");

            return (FileNode)_nodeMap.GetOrAdd(addr.Value, f => FileNode.Load(this, _diskAccess, addr, GlobalBlocksStartAddress));
        }


        /// <summary>
        /// Loads the master record.
        /// </summary>
        /// <exception cref="InvalidOperationException">Only volumes aligned on block size are supported.</exception>
        public void Load()
        {
            ulong offset = 0;
            VolumeSize = _diskAccess.ReadULong(ref offset);
            _freeSpaceBlocks = _diskAccess.ReadULong(ref offset);

            if (VolumeSize % Constants.BlockSizeBytes != 0)
            {
                throw new InvalidOperationException("Only volumes aligned on block size are supported.");
            }

            var rootNodeAddress = new Address(_diskAccess.ReadULong(ref offset));
            var groupCount = _diskAccess.ReadULong(ref offset);

            _groupsReserved = new BlockGroup[groupCount];

            for (uint i = 0; i < groupCount; ++i)
            {
                var descriptor = new BlockGroupDescriptor();
                offset = descriptor.Load(offset, _diskAccess);
                _groupsReserved[i] = new BlockGroup(descriptor); // block group not initialized at this point.
            }

            foreach (var group in _groupsReserved)
            {
                var desc = group.Descriptor;
                group.Load(desc.BitmapsAddress.Value, _diskAccess);
            }

            RootNode = GetDirectoryNode(rootNodeAddress);
        }


        /// <summary>
        /// Saves the master record.
        /// </summary>
        public void Save()
        {
            ulong offset = 0; // master record offset is always 0.
            _diskAccess.WriteULong(ref offset, VolumeSize);
            _diskAccess.WriteULong(ref offset, _freeSpaceBlocks);
            _diskAccess.WriteULong(ref offset, RootNode.Address.Value);
            _diskAccess.WriteULong(ref offset, (ulong)_groupsReserved.Length);
            foreach (var group in _groupsReserved)
            {
                var desc = group.Descriptor;
                offset = desc.Save(offset, _diskAccess);
                group.Save(desc.BitmapsAddress.Value, _diskAccess);
            }
        }

        #endregion


        #region Methods

        /// <summary>
        /// Allocates the specified number of blocks in file system.
        /// Throw exception if specified number cannot be allocated.
        /// </summary>
        /// <exception cref="IOException">Disk full.</exception>
        [NotNull]
        Address[] IBlockAllocator.AllocateBlocks(uint count)
        {
            lock (_sync)
            {
                if (count > _freeSpaceBlocks)
                {
                    throw new IOException("Disk full.");
                }

                _freeSpaceBlocks -= count;
            }

            var ret = new Address[count];
            var curIndex = 0;
            foreach (var group in _groupsReserved)
            {
                while (curIndex < count)
                {
                    var block = group.AllocateNewBlock();
                    if (block == null)
                    {
                        break; // take next group
                    }
                    ret[curIndex++] = block;
                }

                if (curIndex == count)
                {
                    break;
                }
            }

            return ret;
        }


        /// <summary>
        /// Allocates the node.
        /// </summary>
        [CanBeNull]
        private Address AllocateNode()
        {
            foreach (var group in _groupsReserved)
            {
                var addr = group.AllocateNewNode();
                if (addr != null)
                {
                    return addr;
                }
            }

            return null;
        }


        /// <summary>
        /// Frees the specified blocks.
        /// </summary>
        void IBlockAllocator.FreeBlocks([NotNull] params Address[] blocks)
        {
            Validate.ArgumentNotNull(blocks, "blocks");

            foreach (var address in blocks)
            {
                var group = GetGroupByAddress(address);
                group.FreeBlock(address);
            }

            lock (_sync)
            {
                _freeSpaceBlocks += (uint)blocks.Length;
            }
        }


        /// <summary>
        /// Gets the group by its address.
        /// </summary>
        [NotNull]
        private BlockGroup GetGroupByAddress([NotNull] Address address)
        {
            Validate.ArgumentNotNull(address, "address");

            return _groupsReserved[(uint)(address.Value - GlobalBlocksStartAddress.Value) / Constants.GroupSizeBytes];
        }

        #endregion
    }
}