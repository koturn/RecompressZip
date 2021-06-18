namespace RecompressZip.Zip
{
    /// <summary>
    /// Central directory file header.
    /// </summary>
    public class CentralDirectoryFileHeader : ZipHeader
    {
        /// <summary>
        /// Version made by.
        /// </summary>
        public ushort VerMadeBy { get; set; }
        /// <summary>
        /// Version needed to extract (minimum).
        /// </summary>
        public ushort VerExtract { get; set; }
        /// <summary>
        /// General purpose bit flag.
        /// </summary>
        public ushort BitFlag { get; set; }
        /// <summary>
        /// Compression method.
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
        /// File comment length.
        /// </summary>
        public ushort CommentLength { get; set; }
        /// <summary>
        /// Disk number where file starts.
        /// </summary>
        public ushort DiskNumber { get; set; }
        /// <summary>
        /// Internal file attributes.
        /// </summary>
        public ushort InternalFileAttribute { get; set; }
        /// <summary>
        /// External file attributes.
        /// </summary>
        public uint ExternalFileAttribute { get; set; }
        /// <summary>
        /// <para>Relative offset of local file header.</para>
        /// <para>This is the number of bytes between the start of the first disk on which the file occurs,
        /// and the start of the local file header.
        /// This allows software reading the central directory to locate the position of the file inside the ZIP file.</para>
        /// </summary>
        public uint Offset { get; set; }
        /// <summary>
        /// File name.
        /// </summary>
        public byte[] FileName { get; set; }
        /// <summary>
        /// Extra field.
        /// </summary>
        public byte[] ExtraField { get; set; }
        /// <summary>
        /// Comment.
        /// </summary>
        public byte[] Comment { get; set; }
    };
}
