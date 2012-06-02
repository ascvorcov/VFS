using System;

using NUnit.Framework;

using Rhino.Mocks;

using VirtualFileSystem.EXT2;
using VirtualFileSystem.Interfaces;
using VirtualFileSystem.Utilities;


namespace VirtualFileSystem.UnitTests
{
    [TestFixture]
    public class FileNodeTests
    {
        [Test]
        public void BasicTest()
        {
            var repo = new MockRepository();
            var diskAccessMock = repo.DynamicMock<IDirectDiskAccess>();
            var allocatorMock = repo.DynamicMock<IBlockAllocator>();

            ulong addr = 0;
            Expect.Call(diskAccessMock.ReadByte(ref addr)).IgnoreArguments().Return(0).Repeat.Any();
            Expect.Call(diskAccessMock.ReadBytes(ref addr, null, 0, 0)).IgnoreArguments().Return(0).Repeat.Any();
            Expect.Call(diskAccessMock.ReadUInt(ref addr)).IgnoreArguments().Return(0).Repeat.Any();
            Expect.Call(diskAccessMock.ReadULong(ref addr)).IgnoreArguments().Return(0).Repeat.Any();


            Expect.Call(allocatorMock.AllocateBlocks(1)).Return(new[] { new Address(0) }).Repeat.Any();
            repo.ReplayAll();

            var node = FileNode.Create(allocatorMock, diskAccessMock, new Address(0), new Address(0));
            node.ReadData(0, 100);
            node.LockWrite();
            node.WriteData(0, new byte[100]);
        }
    }
}