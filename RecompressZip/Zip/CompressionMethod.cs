namespace RecompressZip.Zip
{
    /// <summary>
    /// <para>Compression Method Values</para>
    /// <para>Note: Methods 1-6, <see cref="Shrunk"/>, <see cref="ReducedCompressionFactor1"/>, <see cref="ReducedCompressionFactor2"/>,
    /// <see cref="ReducedCompressionFactor3"/>, <see cref="ReducedCompressionFactor4"/> and <see cref="Implode"/>, are
    /// legacy algorithms and are no longer recommended for use when compressing files.</para>
    /// </summary>
    public enum CompressionMethod : ushort
    {
        /// <summary>
        /// The file is stored (no compression).
        /// </summary>
        NoCompression = 0,
        /// <summary>
        /// The file is Shrunk.
        /// (Legacy algorithm and are no longer recommended for use when compressing files.)
        /// </summary>
        Shrunk = 1,
        /// <summary>
        /// The file is Reduced with compression factor 1.
        /// (Legacy algorithm and are no longer recommended for use when compressing files.)
        /// </summary>
        ReducedCompressionFactor1 = 2,
        /// <summary>
        /// The file is Reduced with compression factor 2.
        /// (Legacy algorithm and are no longer recommended for use when compressing files.)
        /// </summary>
        ReducedCompressionFactor2 = 3,
        /// <summary>
        /// The file is Reduced with compression factor 3.
        /// (Legacy algorithm and are no longer recommended for use when compressing files.)
        /// </summary>
        ReducedCompressionFactor3 = 4,
        /// <summary>
        /// The file is Reduced with compression factor 4.
        /// (Legacy algorithm and are no longer recommended for use when compressing files.)
        /// </summary>
        ReducedCompressionFactor4 = 5,
        /// <summary>
        /// The file is Imploded.
        /// (Legacy algorithm and are no longer recommended for use when compressing files.)
        /// </summary>
        Implode = 6,
        /// <summary>
        /// Reserved for Tokenizing compression algorithm.
        /// </summary>
        ReservedForTokenizingCompressionAlgorithm = 7,
        /// <summary>
        /// The file is Deflated.
        /// </summary>
        Deflate = 8,
        /// <summary>
        /// Enhanced Deflating using Deflate64(tm).
        /// </summary>
        Deflate64 = 9,
        /// <summary>
        /// PKWARE Data Compression Library Imploding (old IBM TERSE).
        /// </summary>
        ImplodingPkware = 10,
        /// <summary>
        /// Reserved by PKWARE.
        /// </summary>
        ReservedByPkware1 = 11,
        /// <summary>
        /// The file is compressed using BZIP2 algorithm.
        /// </summary>
        Bzip2 = 12,
        /// <summary>
        /// Reserved by PKWARE.
        /// </summary>
        ReservedByPkware2 = 13,
        /// <summary>
        /// The file is compressed using LZMA algorithm.
        /// </summary>
        Lzma = 14,
        /// <summary>
        /// Reserved by PKWARE.
        /// </summary>
        ReservedByPkware3 = 15,
        /// <summary>
        /// IBM z/OS CMPSC Compression.
        /// </summary>
        CmpscCompression = 16,
        /// <summary>
        /// Reserved by PKWARE.
        /// </summary>
        ReservedByPkware4 = 17,
        /// <summary>
        /// File is compressed using IBM TERSE (new).
        /// </summary>
        IbmTerse = 18,
        /// <summary>
        /// IBM LZ77 z Architecture.
        /// </summary>
        IbmLz77Z = 19,
        /// <summary>
        /// Deprecated (use method <see cref="Zstandard"/> for zstd).
        /// </summary>
        Deprecated = 20,
        /// <summary>
        /// Zstandard (zstd) Compression.
        /// </summary>
        Zstandard = 93,
        /// <summary>
        /// MP3 Compression.
        /// </summary>
        Mp3 = 94,
        /// <summary>
        /// XZ Compression.
        /// </summary>
        Xz = 95,
        /// <summary>
        /// JPEG variant.
        /// </summary>
        JpegVariant = 96,
        /// <summary>
        /// WavPack compressed data.
        /// </summary>
        WavPack = 97,
        /// <summary>
        /// PPMd version I, Rev 1.
        /// </summary>
        PPMd = 98,
        /// <summary>
        /// AE-x encryption marker.
        /// </summary>
        AExEncryptionMarker = 99
    }
}
