using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using NUnit.Framework;

using Rhino.Mocks;

using VirtualFileSystem.EXT2;
using VirtualFileSystem.Interfaces;
using VirtualFileSystem.Utilities;


namespace VirtualFileSystem.UnitTests
{
    [TestFixture]
    public class BlockAddressStorageTests
    {
        [Test]
        public void TestIndirectBlocks()
        {
            var repository = new MockRepository();
            var mockDiskAccess = repository.DynamicMock<IDirectDiskAccess>();
            var mockAllocator = repository.StrictMock<IBlockAllocator>();

            const int blocksToAllocate = 2000;
            var list = new List<Address>();
            for (int i = 0; i < blocksToAllocate; ++i)
            {
                list.Add(new Address((ulong)i * Constants.BlockSizeBytes));
            }

            Expect.Call(mockAllocator.AllocateBlocks(blocksToAllocate)).IgnoreArguments().Return(list.ToArray());
            Expect.Call(mockAllocator.AllocateBlocks(1)).IgnoreArguments().Return(new[] { new Address(0) }).Repeat.Times(3); // indirect block allocation
            Expect.Call(() => mockAllocator.FreeBlocks(null)).IgnoreArguments().Repeat.Any();

            var storage = new BlockAddressStorage(mockDiskAccess, mockAllocator, new Address(0), new Address(0));

            repository.ReplayAll();

            storage.AddBlocks(2000);
            storage.GetBlockStartAddress(12);
            storage.GetBlockStartAddress(1024);
            storage.GetBlockStartAddress(1024 + 12);
            storage.GetBlockStartAddress(1999);
            Assert.Throws<IndexOutOfRangeException>(() => storage.GetBlockStartAddress(2000));

            repository.VerifyAll();
        }

        [Test]
        public void TestBlockAllocation()
        {
            var repository = new MockRepository();
            var mockDiskAccess = repository.DynamicMock<IDirectDiskAccess>();
            var mockAllocator = repository.StrictMock<IBlockAllocator>();

            int count = 0;

            Expect.Call(mockAllocator.AllocateBlocks(1)).Return(new []{new Address(0)}).Repeat.Any().WhenCalled(f => count++);
            Expect.Call(() => mockAllocator.FreeBlocks(null)).IgnoreArguments().Repeat.Any().WhenCalled(f => count--);

            var storage = new BlockAddressStorage(mockDiskAccess, mockAllocator, new Address(0), new Address(0));

            repository.ReplayAll();


            for(int i = 0; i < 10000; ++i)
            {
                storage.AddBlocks(1);
            }

            Assert.AreEqual(10000, storage.NumBlocksAllocated);
            Assert.IsNotNull(storage.GetBlockStartAddress(0));
            Assert.IsNotNull(storage.GetBlockStartAddress(9999));

            storage.FreeLastBlocks(5000);
            Assert.AreEqual(5000, storage.NumBlocksAllocated);
            Assert.IsNotNull(storage.GetBlockStartAddress(4999));
            Assert.Throws<IndexOutOfRangeException>(() => storage.GetBlockStartAddress(5000));

            storage.FreeLastBlocks(5000);

            Assert.AreEqual(0, storage.NumBlocksAllocated);
            Assert.Throws<IndexOutOfRangeException>(() => storage.GetBlockStartAddress(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => storage.FreeLastBlocks(1));
            Assert.AreEqual(0, count);
            repository.VerifyAll();
        }
    }
}
