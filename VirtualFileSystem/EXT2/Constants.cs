namespace VirtualFileSystem.EXT2
{
    /// <summary>
    /// Holds constants used in file system implementation.
    /// </summary>
    public static class Constants
    {
        #region fixed constants, hardcoded, should never change

        /// <summary>
        /// Size of single file/directory node header.
        /// </summary>
        public const ushort NodeSize = 128;

        #endregion

        #region constants for fine-tuning

        /// <summary>
        /// Size of single block in file system. That is, minimal allocation size.
        /// </summary>
        public const ushort BlockSizeBytes = 4096;

        /// <summary>
        /// Controls how many nodes are created per single block group.
        /// This value is not actual number of nodes, just ratio.
        /// </summary>
        public const ushort NodeRatio = 8192;

        /// <summary>
        /// Size of buffer in bytes to use when internally copying files.
        /// </summary>
        public const uint   ReadingBufferSizeBytes = BlockSizeBytes * 10;

        #endregion

        #region computed constants

        /// <summary>
        /// Maximum number of blocks which can be addressed by single block group.
        /// </summary>
        public const ushort BlocksPerGroup = BlockSizeBytes * 8; // 8 bits

        /// <summary>
        /// Maximum number of nodes which single block group can address.
        /// </summary>
        public const ushort NodesPerGroup = (BlocksPerGroup * BlockSizeBytes) / NodeRatio;

        /// <summary>
        /// Number of node description headers which fit into one block.
        /// </summary>
        public const ushort NodesPerBlock = BlockSizeBytes / NodeSize;

        /// <summary>
        /// Number of blocks reserved to hold node table.
        /// </summary>
        public const ushort BlocksForNodeTable = NodesPerGroup / NodesPerBlock;

        /// <summary>
        /// Size in bytes of bitmap which holds node allocation map.
        /// </summary>
        public const ushort NodeBitmapSizeBytes = NodesPerGroup / 8;

        /// <summary>
        /// Size of node bitmap in blocks, rounded.
        /// </summary>
        public const ushort NodeBitmapSizeBlocks = (NodeBitmapSizeBytes / BlockSizeBytes) + (NodeBitmapSizeBytes % BlockSizeBytes == 0 ? 0 : 1);

        /// <summary>
        /// Size of single block group in bytes.
        /// </summary>
        public const uint   GroupSizeBytes = BlocksPerGroup * BlockSizeBytes;

        #endregion
    }
}