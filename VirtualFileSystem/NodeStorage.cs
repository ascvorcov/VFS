using System;

using VirtualFileSystem.EXT2;
using VirtualFileSystem.Interfaces;
using VirtualFileSystem.Utilities;


namespace VirtualFileSystem
{
    internal sealed class NodeStorage : IDirectDiskAccess
    {
        private readonly FileNode _node;

        public NodeStorage(FileNode node)
        {
            _node = node;
        }

        public void Dispose()
        {
        }


        public byte ReadByte(ref ulong address)
        {
            using (NodeLocker.Lock(_node, true))
            {
                var data = _node.ReadData(address, 1);
                address++;
                return data[0];
            }
        }


        public uint ReadUInt(ref ulong address)
        {
            using (NodeLocker.Lock(_node, true))
            {
                var ret = BitConverter.ToUInt32(_node.ReadData(address, 4), 0);
                address += 4;
                return ret;
            }
        }


        public ulong ReadULong(ref ulong address)
        {
            using (NodeLocker.Lock(_node, true))
            {
                var ret = BitConverter.ToUInt64(_node.ReadData(address, 8), 0);
                address += 8;
                return ret;
            }
        }


        public void WriteUInt(ref ulong address, uint value)
        {
            using (NodeLocker.Lock(_node, true))
            {
                _node.WriteData(address, BitConverter.GetBytes(value));
                address += 4;
            }
        }


        public void WriteULong(ref ulong address, ulong value)
        {
            using (NodeLocker.Lock(_node, true))
            {
                _node.WriteData(address, BitConverter.GetBytes(value));
                address += 8;
            }
        }


        public void WriteByte(ref ulong address, byte data)
        {
            using (NodeLocker.Lock(_node, true))
            {
                _node.WriteData(address, new[] { data });
                address++;
            }
        }


        public uint ReadBytes(ref ulong address, byte[] data, uint offset, uint count)
        {
            using (NodeLocker.Lock(_node, true))
            {
                var bytes = _node.ReadData(address, count);
                Array.Copy(bytes, 0, data, offset, bytes.Length);
                address += (uint)bytes.Length;
                return (uint)bytes.Length;
            }
        }


        public void WriteBytes(ref ulong address, byte[] data, uint offset, uint count)
        {
            using (NodeLocker.Lock(_node, true))
            {
                var buffer = new byte[count];
                Array.Copy(data, offset, buffer, 0, count);
                _node.WriteData(address, buffer);
                address += count;
            }
        }


        public void SetFileSize(ulong realVolumeSize)
        {
            using (NodeLocker.Lock(_node, true))
            {
                _node.SetFileSize(realVolumeSize);
            }
        }
    }
}