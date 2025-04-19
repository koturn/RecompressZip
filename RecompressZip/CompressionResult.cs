using RecompressZip.Zip;


namespace RecompressZip
{
    /// <summary>
    /// Comression result.
    /// </summary>
    public struct CompressionResult
    {
        /// <summary>
        /// Local file header.
        /// </summary>
        public LocalFileHeader Header { get; }
        /// <summary>
        /// Relative offset of local file header.
        /// </summary>
        public uint Offset { get; set; }

        /// <summary>
        /// Initialize all properties.
        /// </summary>
        /// <param name="header">Local file header.</param>
        /// <param name="offset">Relative offset of local file header.</param>
        public CompressionResult(LocalFileHeader header, uint offset)
        {
            Header = header;
            Offset = offset;
        }
    }
}
