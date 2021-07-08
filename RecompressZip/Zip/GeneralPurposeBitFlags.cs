using System;


namespace RecompressZip.Zip
{
    /// <summary>
    /// General Purpose Bit Flags.
    /// </summary>
    [Flags]
    public enum GeneralPurpsoseBitFlags : ushort
    {
        /// <summary>
        /// Indicates that the file is encrypted.
        /// </summary>
        Encrypted = 0x0001,
        /// <summary>
        /// <para>If the compression method used was type 6, Imploding, then this bit, if set, indicates an 8K sliding dictionary was used.
        /// If clear, then a 4K sliding dictionary was used.</para>
        /// <para>If the compression method used was type 8, Deflating, this bit is used in combination with <see cref="CompressFeature2"/>.
        /// <list type="table">
        ///   <listheader>
        ///     <term><see cref="CompressFeature1"/></term>
        ///     <term><see cref="CompressFeature2"/></term>
        ///     <description>Description</description>
        ///   </listheader>
        ///   <item>
        ///     <term>0</term>
        ///     <term>0</term>
        ///     <description>Normal (-en) compression option was used.</description>
        ///   </item>
        ///   <item>
        ///     <term>1</term>
        ///     <term>0</term>
        ///     <description>Maximum (-exx/-ex) compression option was used.</description>
        ///   </item>
        ///   <item>
        ///     <term>0</term>
        ///     <term>1</term>
        ///     <description>Fast (-ef) compression option was used.</description>
        ///   </item>
        ///   <item>
        ///     <term>1</term>
        ///     <term>1</term>
        ///     <description>Super Fast (-es) compression option was used.</description>
        ///   </item>
        /// </list></para>
        /// <para>If the compression method used was type 14, LZMA, then this bit, if set, indicates an end-of-stream (EOS) marker is used to mark the end of the compressed data stream.
        /// If clear, then an EOS marker is not present and the compressed data size must be known to extract.</para>
        /// </summary>
        CompressFeature1 = 0x0002,
        /// <summary>
        /// <para>If the compression method used was type 6, Imploding, then this bit, if set, indicates 3 Shannon-Fano trees were used to encode the sliding dictionary output.
        /// If clear, then 2 Shannon-Fano trees were used.</para>
        /// <para>If the compression method used was type 8, Deflating, this bit is used in combination with <see cref="CompressFeature1"/>.</para>
        /// </summary>
        CompressFeature2 = 0x0004,
        /// <summary>
        /// <para>Indicate that the fields crc-32, compressed size and uncompressed size are set
        /// to zero in the local header.</para>
        /// <para>The correct values are put in the data descriptor immediately following the compressed data.
        /// (Note: PKZIP version 2.04g for DOS only recognizes this bit for method 8 compression,
        /// newer versions of PKZIP recognize this bit for any compression method.)</para>
        /// </summary>
        ZeroLengthAndCrc32InLocalHeader = 0x0008,
        /// <summary>
        /// Reserved for use with method 8, for enhanced deflating.
        /// </summary>
        EnhancedDeflating = 0x0010,
        /// <summary>
        /// Indicates that the file is compressed patched data.
        /// (Note: Requires PKZIP version 2.70 or greater)
        /// </summary>
        CompressedPatchData = 0x0020,
        /// <summary>
        /// Indicates that the file is compressed patched data.
        /// (Note: Requires PKZIP version 2.70 or greater)
        /// </summary>
        StrongEncryption = 0x0040,
        /// <summary>
        /// Currently unused.
        /// </summary>
        Unused1 = 0x0080,
        /// <summary>
        /// Currently unused.
        /// </summary>
        Unused2 = 0x0100,
        /// <summary>
        /// Currently unused.
        /// </summary>
        Unused3 = 0x0200,
        /// <summary>
        /// Currently unused.
        /// </summary>
        Unused4 = 0x0400,
        /// <summary>
        /// Indicates that the filename and comment fields for this file are encoded using UTF-8.
        /// </summary>
        Utf8NameAndComment = 0x0800,
        /// <summary>
        /// Reserved by PKWARE for enhanced compression.
        /// </summary>
        ReservedByPkware1 = 0x1000,
        /// <summary>
        /// Set when encrypting the Central Directory to indicate selected data values in the Local Header are masked to hide their actual values.
        /// </summary>
        MaskedLocalHeader = 0x2000,
        /// <summary>
        /// Reserved by PKWARE for alternate streams.
        /// </summary>
        ReservedByPkware2 = 0x4000,
        /// <summary>
        /// Reserved by PKWARE.
        /// </summary>
        ReservedByPkware3 = 0x8000
    }
}
