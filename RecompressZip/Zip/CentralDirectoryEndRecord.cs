using System.IO;


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


        /// <summary>
        /// Initialize all properties.
        /// </summary>
        /// <param name="numDisks">Number of this disk (or 0xffff for ZIP64).</param>
        /// <param name="disk">Disk where central directory starts (or 0xffff for ZIP64).</param>
        /// <param name="numRecords">Number of central directory records on this disk (or 0xffff for ZIP64).</param>
        /// <param name="totalRecords">Number of central directory records on this disk (or 0xffff for ZIP64).</param>
        /// <param name="centralDirectorySize">Size of central directory (bytes) (or 0xffffffff for ZIP64).</param>
        /// <param name="offset">Offset of start of central directory, relative to start of archive (or 0xffffffff for ZIP64).</param>
        /// <param name="commentLength">Comment length.</param>
        public CentralDirectoryEndRecord(
            ushort numDisks,
            ushort disk,
            ushort numRecords,
            ushort totalRecords,
            uint centralDirectorySize,
            uint offset,
            ushort commentLength)
        {
            Signature = ZipSignature.EndRecord;
            NumDisks = numDisks;
            Disk = disk;
            NumRecords = numRecords;
            TotalRecords = totalRecords;
            CentralDirectorySize = centralDirectorySize;
            Offset = offset;
            CommentLength = commentLength;
            Comment = new byte[commentLength];
        }


        /// <summary>
        /// Write to specified <see cref="BinaryWriter"/>.
        /// </summary>
        /// <param name="writer"><see cref="BinaryWriter"/> of destination stream.</param>
        public void WriteTo(BinaryWriter writer)
        {
            writer.Write((uint)Signature);
            writer.Write(NumDisks);
            writer.Write(Disk);
            writer.Write(NumRecords);
            writer.Write(TotalRecords);
            writer.Write(CentralDirectorySize);
            writer.Write(Offset);
            writer.Write(CommentLength);
            writer.Write(Comment);
        }


        /// <summary>
        /// Read central directory end record from <see cref="BinaryReader"/>.
        /// </summary>
        /// <param name="reader"><see cref="BinaryReader"/> of zip data.</param>
        /// <param name="isIncludeSignature">If <c>true</c>, read signature at first.</param>
        /// <returns>Local file header data.</returns>
        /// <exception cref="InvalidDataException">Throw when Read signature is not <see cref="ZipSignature.EndRecord"/>.</exception>
        public static CentralDirectoryEndRecord ReadFrom(BinaryReader reader, bool isIncludeSignature = false)
        {
            if (isIncludeSignature)
            {
                var signature = ReadSignature(reader);
                if (signature != ZipSignature.EndRecord)
                {
                    throw new InvalidDataException($"Read signature is invalid: {signature}");
                }
            }

            var header = new CentralDirectoryEndRecord(
                reader.ReadUInt16(),
                reader.ReadUInt16(),
                reader.ReadUInt16(),
                reader.ReadUInt16(),
                reader.ReadUInt32(),
                reader.ReadUInt32(),
                reader.ReadUInt16());
            reader.BaseStream.Read(header.Comment);

            return header;
        }
    }
}
