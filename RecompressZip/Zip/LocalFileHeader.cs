using System.IO;


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
        public GeneralPurpsoseBitFlags BitFlag { get; set; }
        /// <summary>
        /// Compression method; e.g. none = 0, DEFLATE = 8 (or "\0x08\0x00")
        /// </summary>
        public CompressionMethod Method { get; set; }
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
        /// <summary>
        /// Indicates <see cref="Name"/> and <see cref="Comment"/> is UTF-8 string or not.
        /// </summary>
        public bool IsUtf8NameAndComment
        {
            get => (BitFlag & GeneralPurpsoseBitFlags.Utf8NameAndComment) != 0;
            set => BitFlag = value ? (BitFlag | GeneralPurpsoseBitFlags.Utf8NameAndComment) : (BitFlag & ~GeneralPurpsoseBitFlags.Utf8NameAndComment);
        }

        /// <summary>
        /// Initialize all properties.
        /// </summary>
        /// <param name="verExtract">Version needed to extract (minimum).</param>
        /// <param name="bitFlag">General purpose bit flag.</param>
        /// <param name="method">Compression method; e.g. none = 0, DEFLATE = 8 (or "\0x08\0x00")</param>
        /// <param name="lastModificationTime">File last modification time.</param>
        /// <param name="lastModificationDate">File last modification date.</param>
        /// <param name="crc32">CRC-32 of uncompressed data.</param>
        /// <param name="compressedLength">Compressed size (or 0xffffffff for ZIP64).</param>
        /// <param name="length">Uncompressed size (or 0xffffffff for ZIP64).</param>
        /// <param name="fileNameLength">File name length.</param>
        /// <param name="extraLength">Extra field length.</param>
        public LocalFileHeader(
            ushort verExtract,
            GeneralPurpsoseBitFlags bitFlag,
            CompressionMethod method,
            ushort lastModificationTime,
            ushort lastModificationDate,
            uint crc32,
            uint compressedLength,
            uint length,
            ushort fileNameLength,
            ushort extraLength)
        {
            Signature = ZipSignature.LocalFileHeader;
            VerExtract = verExtract;
            BitFlag = bitFlag;
            Method = method;
            LastModificationTime = lastModificationTime;
            LastModificationDate = lastModificationDate;
            Crc32 = crc32;
            CompressedLength = compressedLength;
            Length = length;
            FileNameLength = fileNameLength;
            ExtraLength = extraLength;
            FileName = new byte[fileNameLength];
            ExtraField = new byte[extraLength];
        }


        /// <summary>
        /// Write to specified <see cref="BinaryWriter"/>.
        /// </summary>
        /// <param name="writer"><see cref="BinaryWriter"/> of destination stream.</param>
        public void WriteTo(BinaryWriter writer)
        {
            writer.Write((uint)Signature);
            writer.Write(VerExtract);
            writer.Write((ushort)BitFlag);
            writer.Write((ushort)Method);
            writer.Write(LastModificationTime);
            writer.Write(LastModificationDate);
            writer.Write(Crc32);
            writer.Write(CompressedLength);
            writer.Write(Length);
            writer.Write(FileNameLength);
            writer.Write(ExtraLength);
            writer.Write(FileName);
            writer.Write(ExtraField);
        }


        /// <summary>
        /// Read local file header from <see cref="BinaryReader"/>.
        /// </summary>
        /// <param name="reader"><see cref="BinaryReader"/> of zip data.</param>
        /// <param name="isIncludeSignature">If <c>true</c>, read signature at first.</param>
        /// <returns>Local file header data.</returns>
        /// <exception cref="InvalidDataException">Throw when Read signature is not <see cref="ZipSignature.LocalFileHeader"/>.</exception>
        public static LocalFileHeader ReadFrom(BinaryReader reader, bool isIncludeSignature = false)
        {
            if (isIncludeSignature)
            {
                var signature = ReadSignature(reader);
                if (signature != ZipSignature.LocalFileHeader)
                {
                    throw new InvalidDataException($"Read signature is invalid: {signature}");
                }
            }

            var header = new LocalFileHeader(
                reader.ReadUInt16(),
                (GeneralPurpsoseBitFlags)reader.ReadUInt16(),
                (CompressionMethod)reader.ReadUInt16(),
                reader.ReadUInt16(),
                reader.ReadUInt16(),
                reader.ReadUInt32(),
                reader.ReadUInt32(),
                reader.ReadUInt32(),
                reader.ReadUInt16(),
                reader.ReadUInt16());
            var baseStream = reader.BaseStream;
            baseStream.Read(header.FileName);
            baseStream.Read(header.ExtraField);

            return header;
        }
    };
}
