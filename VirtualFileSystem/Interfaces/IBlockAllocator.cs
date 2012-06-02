using VirtualFileSystem.Annotations;
using VirtualFileSystem.Utilities;


namespace VirtualFileSystem.Interfaces
{
    /// <summary>
    /// Defines an abstract block allocator.
    /// That is, implementation must allocate/free blocks on demand,
    /// throwing if there is not enough space.
    /// </summary>
    public interface IBlockAllocator
    {
        /// <summary>
        /// Allocates the specified number of blocks in file system.
        /// Throw exception if specified number cannot be allocated.
        /// </summary>
        [NotNull]
        Address[] AllocateBlocks(uint count);

        /// <summary>
        /// Frees the specified blocks.
        /// </summary>
        void FreeBlocks([NotNull] params Address[] blocks);
    }
}