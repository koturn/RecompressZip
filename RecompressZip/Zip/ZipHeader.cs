using System.IO;


namespace RecompressZip.Zip
{
    /// <summary>
    /// Common part of header, <see cref="LocalFileHeader"/>, <see cref="CentralDirectoryFileHeader"/>
    /// or <see cref="CentralDirectoryEndRecord"/>.
    /// </summary>
    public class ZipHeader
    {
        /// <summary>
        /// File header signature.
        /// <list type="table">
        ///   <listheader>
        ///     <term><see cref="LocalFileHeader"/></term>
        ///     <description>0x04034b50 (<see cref="ZipSignature.LocalFileHeader"/>)</description>
        ///   </listheader>
        ///   <item>
        ///     <term><see cref="CentralDirectoryFileHeader"/></term>
        ///     <description>0x02014b50 (<see cref="ZipSignature.CentralDirectoryFileHeader"/>)</description>
        ///   </item>
        ///   <item>
        ///     <term><see cref="CentralDirectoryEndRecord"/></term>
        ///     <description>0x06054b50 (<see cref="ZipSignature.EndRecord"/>)</description>
        ///   </item>
        /// </list>
        /// </summary>
        public ZipSignature Signature { get; set; }

        /// <summary>
        /// Read zip signature, just read 4 bytes.
        /// </summary>
        /// <param name="reader"><see cref="BinaryReader"/> of zip data.</param>
        /// <returns>Signature.</returns>
        public static ZipSignature ReadSignature(BinaryReader reader)
        {
            return (ZipSignature)reader.ReadUInt32();
        }
    };
}
