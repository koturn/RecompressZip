using RecompressZip.Zip;


namespace RecompressZip
{
    /// <summary>
    /// Comression result.
    /// </summary>
    /// <remarks>
    /// Primary ctor: Initialize all properties.
    /// </remarks>
    /// <param name="header">Local file header.</param>
    /// <param name="offset">Relative offset of local file header.</param>
    public struct CompressionResult(LocalFileHeader header, uint offset)
    {
        /// <summary>
        /// Local file header.
        /// </summary>
        public LocalFileHeader Header { get; } = header;
        /// <summary>
        /// Relative offset of local file header.
        /// </summary>
        public uint Offset { get; set; } = offset;
    }
}
