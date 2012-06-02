using System;
using System.IO;

using NUnit.Framework;

using VirtualFileSystem.EXT2;
using VirtualFileSystem.Utilities;


namespace VirtualFileSystem.UnitTests
{
    [TestFixture]
    public class BlockGroupTests
    {
        [Test]
        public void BasicTest()
        {
            Assert.Throws<InvalidOperationException>(() => new BlockGroup(new Address(0), BlockGroup.ReservedBlocks));

            var group = new BlockGroup(new Address(4096), 1000);

            Assert.IsNotNull(group.Descriptor);
            Assert.AreEqual(4096, group.Descriptor.BitmapsAddress.Value);
            Assert.AreEqual(1000 - BlockGroup.ReservedBlocks, group.Descriptor.NumFreeBlocksInGroup);
            Assert.AreEqual(Constants.NodesPerGroup, group.Descriptor.NumFreeNodesInGroup);

            var node1 = group.AllocateNewNode();
            var node2 = group.AllocateNewNode();

            Assert.AreEqual(Constants.NodesPerGroup - 2, group.Descriptor.NumFreeNodesInGroup);

            group.FreeNode(node1);
            group.FreeNode(node2);

            Assert.AreEqual(Constants.NodesPerGroup, group.Descriptor.NumFreeNodesInGroup);

            var block1 = group.AllocateNewBlock();
            var block2 = group.AllocateNewBlock();

            Assert.AreEqual(1000 - BlockGroup.ReservedBlocks - 2, group.Descriptor.NumFreeBlocksInGroup);

            group.FreeBlock(block1);
            group.FreeBlock(block2);

            Assert.AreEqual(1000 - BlockGroup.ReservedBlocks, group.Descriptor.NumFreeBlocksInGroup);

            while (group.AllocateNewBlock() != null)
            {
            }

            while (group.AllocateNewNode() != null)
            {
            }

            Assert.IsNull(group.AllocateNewBlock());
            Assert.IsNull(group.AllocateNewNode());
        }
    }
}