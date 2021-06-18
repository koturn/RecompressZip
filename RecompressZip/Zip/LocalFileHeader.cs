namespace RecompressZip.Zip
{
    /// <summary>
    /// <para>Header for files.</para>
    /// <para>All multi-byte values in the header are stored in little-endian byte order.</para>
    /// <para>All length fields count the length in bytes.</para>
    /// </summary>
    public class LocalFileHeader : ZipHeader
    {
        /// <summary>
        /// Version needed to extract (minimum).
        /// </summary>
        public ushort VerExtract { get; set; }
        /// <summary>
        /// General purpose bit flag.
        /// </summary>
        public ushort BitFlag { get; set; }
        /// <summary>
        /// Compression method; e.g. none = 0, DEFLATE = 8 (or "\0x08\0x00")
        /// </summary>
        public ushort Method { get; set; }
        /// <summary>
        /// File last modification time.
        /// </summary>
        public ushort LastModificationTime { get; set; }
        /// <summary>
        /// File last modification date.
        /// </summary>
        public ushort LastModificationDate { get; set; }
        /// <summary>
        /// CRC-32 of uncompressed data.
        /// </summary>
        public uint Crc32 { get; set; }
        /// <summary>
        /// Compressed size (or 0xffffffff for ZIP64).
        /// </summary>
        public uint CompressedLength { get; set; }
        /// <summary>
        /// Uncompressed size (or 0xffffffff for ZIP64).
        /// </summary>
        public uint Length { get; set; }
        /// <summary>
        /// File name length.
        /// </summary>
        public ushort FileNameLength { get; set; }
        /// <summary>
        /// Extra field length.
        /// </summary>
        public ushort ExtraLength { get; set; }
        /// <summary>
        /// File name.
        /// </summary>
        public byte[] FileName { get; set; }
        /// <summary>
        /// Extra field.
        /// </summary>
        public byte[] ExtraField { get; set; }
    };
}
