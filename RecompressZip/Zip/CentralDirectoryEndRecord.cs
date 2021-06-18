namespace RecompressZip.Zip
{
    /// <summary>
    /// <para>End of central directory record (EOCD)</para>
    /// <para>After all the central directory entries comes the end of central directory (EOCD) record,
    /// which marks the end of the ZIP file.</para>
    /// </summary>
    public class CentralDirectoryEndRecord : ZipHeader
    {
        /// <summary>
        /// Number of this disk (or 0xffff for ZIP64).
        /// </summary>
        public ushort NumDisks { get; set; }
        /// <summary>
        /// Disk where central directory starts (or 0xffff for ZIP64).
        /// </summary>
        public ushort Disk { get; set; }
        /// <summary>
        /// Number of central directory records on this disk (or 0xffff for ZIP64).
        /// </summary>
        public ushort NumRecords { get; set; }
        /// <summary>
        /// Number of central directory records on this disk (or 0xffff for ZIP64).
        /// </summary>
        public ushort TotalRecords { get; set; }
        /// <summary>
        /// Size of central directory (bytes) (or 0xffffffff for ZIP64).
        /// </summary>
        public uint CentralDirectorySize { get; set; }
        /// <summary>
        /// Offset of start of central directory, relative to start of archive (or 0xffffffff for ZIP64).
        /// </summary>
        public uint Offset { get; set; }
        /// <summary>
        /// Comment length.
        /// </summary>
        public ushort CommentLength { get; set; }
        /// <summary>
        /// Comment.
        /// </summary>
        public byte[] Comment { get; set; }
    }
}
