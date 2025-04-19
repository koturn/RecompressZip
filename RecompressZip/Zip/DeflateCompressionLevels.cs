namespace RecompressZip.Zip
{
    /// <summary>
    /// Compression levels for Bit 1 and Bit 2 of <see cref="GeneralPurpsoseBitFlags"/> of deflating.
    /// </summary>
    public enum DeflateCompressionLevels : byte
    {
        /// <summary>
        /// Normal compression.
        /// </summary>
        Normal = 0x00,
        /// <summary>
        /// Maximum compression.
        /// </summary>
        Maximum = 0x01,
        /// <summary>
        /// Fast compression.
        /// </summary>
        Fast = 0x02,
        /// <summary>
        /// Super Fast compression.
        /// </summary>
        SuperFast = 0x03
    }
}
