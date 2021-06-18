namespace RecompressZip.Zip
{
    /// <summary>
    /// Signature enum.
    /// </summary>
    public enum ZipSignature : uint
    {
        /// <summary>
        /// Signature for <see cref="Zip.LocalFileHeader"/>.
        /// </summary>
        LocalFileHeader = 0x04034b50,
        /// <summary>
        /// Signature for <see cref="Zip.CentralDirectoryFileHeader"/>
        /// </summary>
        CentralDirectoryFileHeader = 0x02014b50,
        /// <summary>
        /// Signature for <see cref="Zip.CentralDirectoryEndRecord"/>
        /// </summary>
        EndRecord = 0x06054b50
    }
}
