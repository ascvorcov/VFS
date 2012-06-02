#region Namespace Imports

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using VirtualFileSystem.Annotations;
using VirtualFileSystem.Interfaces;
using VirtualFileSystem.Utilities;

#endregion


namespace VirtualFileSystem.EXT2
{
    /// <summary>
    /// Represents directory node, node which holds list of directory entries.
    /// </summary>
    public sealed class DirectoryNode : Node
    {
        #region Constants and Fields

        /// <summary>
        /// Special name of current folder.
        /// </summary>
        public const string SpecialNameCurrentDir = ".";

        /// <summary>
        /// Special name of navigate up folder.
        /// </summary>
        public const string SpecialNameNavigateUp = "..";

        private static readonly char[] _invalidFileNameChars = Path.GetInvalidFileNameChars();

        private readonly List<DirectoryEntry> _list = new List<DirectoryEntry>();
        private readonly MasterRecord _record; // todo: decouple as INodeStorage

        #endregion


        #region Constructors and Destructors

        /// <summary>
        /// Initializes a new instance of the <see cref="DirectoryNode"/> class.
        /// </summary>
        /// <param name="record">The master record.</param>
        /// <param name="address">The directory node address.</param>
        /// <param name="diskAccess">The disk access interface.</param>
        private DirectoryNode([NotNull] MasterRecord record, [NotNull] Address address, [NotNull] IDirectDiskAccess diskAccess)
            : base(record, diskAccess, address, record.GlobalBlocksStartAddress)
        {
            Validate.ArgumentNotNull(record, "record");
            Validate.ArgumentNotNull(address, "address");
            Validate.ArgumentNotNull(diskAccess, "diskAccess");

            _record = record;
        }

        #endregion


        #region Properties

        /// <summary>
        /// Gets all child nodes, except special folders and deleted entries.
        /// </summary>
        public IEnumerable<Node> AllChildEntries
        {
            get
            {
                return DirectoryList
                    .Where(entry => entry.Name != SpecialNameCurrentDir && entry.Name != SpecialNameNavigateUp)
                    .Select(DirectoryEntryToNode);
            }
        }

        /// <summary>
        /// Gets a value indicating whether this <see cref="DirectoryNode"/> is directory.
        /// Always returns true.
        /// </summary>
        public override bool IsDirectory
        {
            get
            {
                return true;
            }
        }

        /// <summary>
        /// Gets the directory entry list, excluding deleted entries.
        /// </summary>
        [NotNull]
        private IList<DirectoryEntry> DirectoryList
        {
            get
            {
                return _list.Where(e => !e.IsDeleted).ToList();
            }
        }

        /// <summary>
        /// Gets the number of entries in directory list, including deleted ones.
        /// Updated separately, so may be not synchronized with internal list.
        /// </summary>
        private uint NumberOfEntries
        {
            get
            {
                return (uint)Size;
            }
        }

        #endregion


        #region Public Methods

        /// <summary>
        /// Creates new directory node for specified parent.
        /// </summary>
        [NotNull]
        public static DirectoryNode Create([CanBeNull] DirectoryNode parent, [NotNull] Address address, [NotNull] MasterRecord record, [NotNull] IDirectDiskAccess diskAccess)
        {
            Validate.ArgumentNotNull(address, "address");
            Validate.ArgumentNotNull(record, "record");
            Validate.ArgumentNotNull(diskAccess, "diskAccess");

            var node = new DirectoryNode(record, address, diskAccess);
            using (NodeLocker.Lock(node, true))
            {
                node.AddChildEntry(SpecialNameCurrentDir, true, node.Address); // self
                if (parent != null)
                {
                    node.AddChildEntry(SpecialNameNavigateUp, true, parent.Address); // parent
                }

                node.Save();
                return node;
            }
        }


        /// <summary>
        /// Loads the directory node from specified address.
        /// </summary>
        [NotNull]
        public static DirectoryNode Load([NotNull] Address address, [NotNull] MasterRecord record, [NotNull] IDirectDiskAccess diskAccess)
        {
            Validate.ArgumentNotNull(address, "address");
            Validate.ArgumentNotNull(record, "record");
            Validate.ArgumentNotNull(diskAccess, "diskAccess");

            var node = new DirectoryNode(record, address, diskAccess);
            node.Load();
            return node;
        }


        /// <summary>
        /// Adds the child entry to directory. Name must be unique, not contain invalid chars, and be less than 255 symbols.
        /// </summary>
        /// <exception cref="ArgumentException">File name of this size is not supported.</exception>
        /// <exception cref="ArgumentException">File name contains invalid characters.</exception>
        /// <exception cref="IOException"><c>IOException</c>.</exception>
        public void AddChildEntry([NotNull] string name, bool directory, [NotNull] Address address)
        {
            Validate.ArgumentNotNull(name, "name");
            Validate.ArgumentNotNull(address, "address");

            if (name.Length > 255 || name.Length <= 0)
            {
                throw new ArgumentException("File name of this size is not supported.");
            }

            if (name.IndexOfAny(_invalidFileNameChars) >= 0)
            {
                throw new ArgumentException("File name contains invalid characters.");
            }

            if (DirectoryList.Any(e => string.Equals(e.Name, name, StringComparison.InvariantCultureIgnoreCase)))
            {
                throw new IOException(directory ? "Directory already exists." : "File already exists.");
            }

            InsertEntryIntoList(name, directory, address);
        }


        /// <summary>
        /// Finds the and removes child entry from internal list.
        /// Can return null if entry not found, or entry found but it is not directory.
        /// </summary>
        [CanBeNull]
        public Node FindAndRemoveChildEntry([NotNull] string segment, bool directory)
        {
            Validate.ArgumentNotNull(segment, "segment");

            foreach (var entry in DirectoryList)
            {
                if (!string.Equals(entry.Name, segment, StringComparison.InvariantCultureIgnoreCase))
                {
                    continue;
                }

                if (entry.IsDirectory != directory)
                {
                    // make sure only correct type of child can be removed.
                    return null;
                }

                entry.IsDeleted = true;
                entry.Save(DiskAccess);
                return DirectoryEntryToNode(entry);
            }

            return null;
        }


        /// <summary>
        /// Finds the child entry with specified name.
        /// </summary>
        [CanBeNull]
        public Node FindChildEntry([NotNull] string segment)
        {
            Validate.ArgumentNotNull(segment, "segment");

            foreach (var entry in DirectoryList)
            {
                if (string.Equals(entry.Name, segment, StringComparison.InvariantCultureIgnoreCase))
                {
                    return DirectoryEntryToNode(entry);
                }
            }

            return null;
        }


        /// <summary>
        /// Finds the entries in current node matching specified pattern.
        /// </summary>
        [NotNull]
        public IEnumerable<string> FindMatchingEntries([NotNull] string pattern)
        {
            Validate.ArgumentNotNull(pattern, "pattern");

            var sp = new SearchPattern(pattern);
            foreach (var entry in DirectoryList)
            {
                if (entry.Name == SpecialNameCurrentDir || entry.Name == SpecialNameNavigateUp)
                {
                    // skip special names, as framework does
                    continue;
                }

                if (sp.Match(entry.Name))
                {
                    yield return entry.Name;
                }
            }
        }


        /// <summary>
        /// Gets the all child directory names.
        /// </summary>
        [NotNull]
        public IEnumerable<string> GetAllChildDirectories()
        {
            return DirectoryList
                .Where(e => e.IsDirectory && e.Name != SpecialNameCurrentDir && e.Name != SpecialNameNavigateUp)
                .Select(e => e.Name);
        }


        /// <summary>
        /// Overload of base load, used to load directory entries from storage.
        /// </summary>
        /// <exception cref="Exception">Directory entry list corrupted.</exception>
        public override void Load()
        {
            base.Load();

            // now load directory list, after loading base node info.
            _list.Clear();
            var address = Storage.GetBlockStartAddress(0);
            var dataOffset = address.Value;
            for (int i = 0; i < NumberOfEntries; ++i)
            {
                var entry = new DirectoryEntry();
                entry.Load(dataOffset, DiskAccess);
                _list.Add(entry);
                dataOffset = entry.Next == null ? 0 : entry.Next.Value;
            }

            if (NumberOfEntries > 0 && dataOffset != 0)
            {
                throw new Exception("Directory entry list corrupted.");
            }
        }


        /// <summary>
        /// Saves the directory node.
        /// </summary>
        public override void Save()
        {
            // save also does space optimization - if some entries were deleted,
            // or resurrected and now take less space. 
            // So save will also change addresses, and do deallocation.

            uint currentBlockIndex = 0;
            uint remainingBlockSize = Constants.BlockSizeBytes;
            ulong currentAddress = Storage.GetBlockStartAddress(0).Value;
            var newEntryList = new List<DirectoryEntry>();
            DirectoryEntry previousEntry = null;
            for (int i = 0; i < _list.Count; ++i)
            {
                var entry = _list[i];
                if (entry.IsDeleted)
                {
                    continue;
                }

                newEntryList.Add(entry);
                var sz = entry.CalculateEntrySize();
                if (remainingBlockSize < sz)
                {
                    currentAddress = Storage.GetBlockStartAddress(++currentBlockIndex).Value;
                    remainingBlockSize = Constants.BlockSizeBytes;
                }

                entry.Next = null;
                entry.EntrySelfAddress = new Address(currentAddress);
                if (previousEntry != null)
                {
                    previousEntry.Next = entry.EntrySelfAddress;
                }

                remainingBlockSize -= sz;
                currentAddress += sz;
                previousEntry = entry;
            }

            if (Storage.NumBlocksAllocated > currentBlockIndex + 1)
            {
                var unusedBlocks = Storage.NumBlocksAllocated - currentBlockIndex - 1;
                if (unusedBlocks > 0)
                {
                    // some blocks can be freed.
                    Storage.FreeLastBlocks(unusedBlocks);
                }
            }

            _list.Clear();
            _list.AddRange(newEntryList);
            UpdateSize((ulong)_list.Count);

            base.Save();
            foreach (var entry in _list)
            {
                entry.Save(DiskAccess);
            }
        }

        #endregion


        #region Methods

        /// <summary>
        /// Inserts the entry into list.
        /// Tries to resurrect deleted entry if possible.
        /// Adds new entry if not possible. Updates data in storage.
        /// Tries to do optimization on every 100 items added.
        /// </summary>
        private void InsertEntryIntoList([NotNull] string name, bool directory, [NotNull] Address nodeAddress)
        {
            uint currentBlockIndex = 0;
            uint currentBlockSpace = Constants.BlockSizeBytes;
            foreach (var currentEntry in _list)
            {
                if (currentEntry.IsDeleted && currentEntry.Name.Length >= name.Length)
                {
                    currentEntry.Resurrect(name, directory, nodeAddress);
                    currentEntry.Save(DiskAccess);
                    return;
                }
                if (currentEntry.EntrySizeBytes > currentBlockSpace)
                {
                    currentBlockIndex++;
                    currentBlockSpace = Constants.BlockSizeBytes;
                }

                currentBlockSpace -= currentEntry.EntrySizeBytes;
            }

            var newEntry = new DirectoryEntry { Name = name, IsDirectory = directory, NodeAddress = nodeAddress };
            if (currentBlockSpace < newEntry.EntrySizeBytes)
            {
                currentBlockIndex++;
                currentBlockSpace = Constants.BlockSizeBytes;
            }

            if (currentBlockIndex >= Storage.NumBlocksAllocated)
            {
                // should never need to allocate more than 1 block
                Storage.AddBlocks(1);
            }

            var offset = Constants.BlockSizeBytes - currentBlockSpace;
            var addr = Storage.GetBlockStartAddress(currentBlockIndex);
            newEntry.EntrySelfAddress = new Address(addr.Value + offset);
            newEntry.Save(DiskAccess);
            var lastItem = _list.LastOrDefault();
            if (lastItem != null)
            {
                lastItem.Next = newEntry.EntrySelfAddress;
                lastItem.Save(DiskAccess);
            }

            _list.Add(newEntry);
            UpdateSize((ulong)_list.Count); // size holds total number of entries, including deleted ones.

            if (_list.Count % 100 == 0)
            {
                // do space optimization on every 100 files,
                // to prevent attack like adding files with increasing name length.
                Save();
            }
        }


        /// <summary>
        /// Converts directory entry to a node.
        /// </summary>
        [NotNull]
        private Node DirectoryEntryToNode([NotNull] DirectoryEntry entry)
        {
            Validate.ArgumentNotNull(entry, "entry");

            if (entry.IsDirectory)
            {
                return _record.GetDirectoryNode(entry.NodeAddress);
            }

            return _record.GetFileNode(entry.NodeAddress);
        }

        #endregion
    }
}