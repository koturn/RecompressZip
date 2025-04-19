using System;
using System.IO;


namespace RecompressZip.Zip
{
    /// <summary>
    /// Central directory file header.
    /// </summary>
    public class CentralDirectoryFileHeader : ZipHeader
    {
        /// <summary>
        /// Base size of the central directory file header.
        /// </summary>
        public const uint BaseSize = 46u;

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
        public GeneralPurpsoseBitFlags BitFlag { get; set; }
        /// <summary>
        /// Compression method.
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
        /// <summary>
        /// Total size of this central directory file header.
        /// </summary>
        public uint TotalSize => BaseSize + FileNameLength + ExtraLength + CommentLength;
        /// <summary>
        /// Indicates data of zip entry is encrypted or not.
        /// </summary>
        public bool IsEncrypted
        {
            get => (BitFlag & GeneralPurpsoseBitFlags.Encrypted) != 0;
            set => BitFlag = value ? (BitFlag | GeneralPurpsoseBitFlags.Encrypted) : (BitFlag & ~GeneralPurpsoseBitFlags.Encrypted);
        }
        /// <summary>
        /// Deflate compression level, represented by the Bit 1 and Bit 2 of <see cref="BitFlag"/>.
        /// </summary>
        public DeflateCompressionLevels DeflateCompressionLevel
        {
            get => (DeflateCompressionLevels)((byte)(BitFlag & GeneralPurpsoseBitFlags.CompressFeatureMask) >> 1);
            set
            {
#if NET5_0_OR_GREATER
                if (!Enum.IsDefined(value))
#else
                if (!Enum.IsDefined(typeof(DeflateCompressionLevels), value))
#endif  // NET5_0_OR_GREATER
                {
                    ThrowArgumentException($"Value is not defined in {nameof(DeflateCompressionLevels)}.", nameof(DeflateCompressionLevel));
                }
                // Unset Bit 1 and Bit 2.
                BitFlag &= ~GeneralPurpsoseBitFlags.CompressFeatureMask;
                // Set Bit 1 and Bit 2.
                BitFlag |= (GeneralPurpsoseBitFlags)((ushort)value << 1);
            }
        }
        /// <summary>
        /// Indicates zip entry has data descriptor.
        /// </summary>
        public bool HasDataDescriptor
        {
            get => (BitFlag & GeneralPurpsoseBitFlags.HasDataDescriptor) != 0;
            set => BitFlag = value ? (BitFlag | GeneralPurpsoseBitFlags.HasDataDescriptor) : (BitFlag & ~GeneralPurpsoseBitFlags.HasDataDescriptor);
        }
        /// <summary>
        /// Indicates <see cref="FileName"/> and <see cref="Comment"/> is UTF-8 string byte sequence or not.
        /// </summary>
        public bool IsUtf8NameAndComment
        {
            get => (BitFlag & GeneralPurpsoseBitFlags.Utf8NameAndComment) != 0;
            set => BitFlag = value ? (BitFlag | GeneralPurpsoseBitFlags.Utf8NameAndComment) : (BitFlag & ~GeneralPurpsoseBitFlags.Utf8NameAndComment);
        }


        /// <summary>
        /// Initialize all properties.
        /// </summary>
        /// <param name="verMadeBy">Version made by.</param>
        /// <param name="verExtract">Version needed to extract (minimum).</param>
        /// <param name="bitFlag">General purpose bit flag.</param>
        /// <param name="method">Compression method.</param>
        /// <param name="lastModificationTime">File last modification time.</param>
        /// <param name="lastModificationDate">File last modification date.</param>
        /// <param name="crc32">CRC-32 of uncompressed data.</param>
        /// <param name="compressedLength">Compressed size (or 0xffffffff for ZIP64).</param>
        /// <param name="length">Uncompressed size (or 0xffffffff for ZIP64).</param>
        /// <param name="fileNameLength">File name length.</param>
        /// <param name="extraLength">Extra field length.</param>
        /// <param name="commentLength">File comment length.</param>
        /// <param name="diskNumber">Disk number where file starts.</param>
        /// <param name="internalFileAttribute">Internal file attributes.</param>
        /// <param name="externalFileAttribute">External file attributes.</param>
        /// <param name="offset">Relative offset of local file header.</param>
        public CentralDirectoryFileHeader(
            ushort verMadeBy,
            ushort verExtract,
            GeneralPurpsoseBitFlags bitFlag,
            CompressionMethod method,
            ushort lastModificationTime,
            ushort lastModificationDate,
            uint crc32,
            uint compressedLength,
            uint length,
            ushort fileNameLength,
            ushort extraLength,
            ushort commentLength,
            ushort diskNumber,
            ushort internalFileAttribute,
            uint externalFileAttribute,
            uint offset)
        {
            Signature = ZipSignature.CentralDirectoryFileHeader;
            VerMadeBy = verMadeBy;
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
            CommentLength = commentLength;
            DiskNumber = diskNumber;
            InternalFileAttribute = internalFileAttribute;
            ExternalFileAttribute = externalFileAttribute;
            Offset = offset;
            FileName = new byte[fileNameLength];
            ExtraField = new byte[extraLength];
            Comment = new byte[commentLength];
        }


        /// <summary>
        /// Write to specified <see cref="BinaryWriter"/>.
        /// </summary>
        /// <param name="writer"><see cref="BinaryWriter"/> of destination stream.</param>
        public void WriteTo(BinaryWriter writer)
        {
            writer.Write((uint)Signature);
            writer.Write(VerMadeBy);
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
            writer.Write(CommentLength);
            writer.Write(DiskNumber);
            writer.Write(InternalFileAttribute);
            writer.Write(ExternalFileAttribute);
            writer.Write(Offset);
            writer.Write(FileName);
            writer.Write(ExtraField);
            writer.Write(Comment);
        }


        /// <summary>
        /// Read central directory file header from <see cref="BinaryReader"/>.
        /// </summary>
        /// <param name="reader"><see cref="BinaryReader"/> of zip data.</param>
        /// <param name="isIncludeSignature">If <c>true</c>, read signature at first.</param>
        /// <returns>Central directory file header data.</returns>
        /// <exception cref="InvalidDataException">Throw when Read signature is not <see cref="ZipSignature.CentralDirectoryFileHeader"/>.</exception>
        public static CentralDirectoryFileHeader ReadFrom(BinaryReader reader, bool isIncludeSignature = false)
        {
            if (isIncludeSignature)
            {
                var signature = ReadSignature(reader);
                if (signature != ZipSignature.CentralDirectoryFileHeader)
                {
                    throw new InvalidDataException($"Read signature is invalid: {signature}");
                }
            }

            var header = new CentralDirectoryFileHeader(
                reader.ReadUInt16(),
                reader.ReadUInt16(),
                (GeneralPurpsoseBitFlags)reader.ReadUInt16(),
                (CompressionMethod)reader.ReadUInt16(),
                reader.ReadUInt16(),
                reader.ReadUInt16(),
                reader.ReadUInt32(),
                reader.ReadUInt32(),
                reader.ReadUInt32(),
                reader.ReadUInt16(),
                reader.ReadUInt16(),
                reader.ReadUInt16(),
                reader.ReadUInt16(),
                reader.ReadUInt16(),
                reader.ReadUInt32(),
                reader.ReadUInt32());
            var baseStream = reader.BaseStream;
#if NETCOREAPP2_1_OR_GREATER
            baseStream.Read(header.FileName);
            baseStream.Read(header.ExtraField);
            baseStream.Read(header.Comment);
#else
            baseStream.Read(header.FileName, 0, header.FileName.Length);
            baseStream.Read(header.ExtraField, 0, header.ExtraField.Length);
            baseStream.Read(header.Comment, 0, header.Comment.Length);
#endif  // NETCOREAPP2_1_OR_GREATER

            return header;
        }
    }
}
