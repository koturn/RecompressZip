namespace RecompressZip
{
    /// <summary>
    /// Comression result.
    /// </summary>
    public struct CompressionResult
    {
        /// <summary>
        /// Compressed size.
        /// </summary>
        public uint CompressedLength { get; set; }
        /// <summary>
        /// Uncompressed size.
        /// </summary>
        public uint Length { get; set; }
        /// <summary>
        /// Relative offset of local file header.
        /// </summary>
        public uint Offset { get; set; }

        /// <summary>
        /// Initialize all properties.
        /// </summary>
        /// <param name="compressedLength">Compressed size.</param>
        /// <param name="length">Uncompressed size.</param>
        /// <param name="offset">Relative offset of local file header.</param>
        public CompressionResult(uint compressedLength, uint length, uint offset)
        {
            CompressedLength = compressedLength;
            Length = length;
            Offset = offset;
        }
    }
}
