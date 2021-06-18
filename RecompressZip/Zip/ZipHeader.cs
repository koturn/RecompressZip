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
        public uint Signature;
    };
}
