using System;


namespace RecompressZip
{
    /// <summary>
    /// Option values class for execution.
    /// </summary>
    public class ExecuteOptions : ICloneable
    {
        /// <summary>
        /// Default value for <see cref="NumberOfThreads"/>.
        /// </summary>
        public const int DefaultNumberOfThreads = -1;

        /// <summary>
        /// Number of threads for re-compressing. -1 means unlimited.
        /// </summary>
        public int NumberOfThreads { get; set; }
        /// <summary>
        /// Password of zip archive.
        /// </summary>
        public string? Password { get; set; }
        /// <summary>
        /// Force compress non compressed data.
        /// </summary>
        public bool IsForceCompress { get; set; }
        /// <summary>
        /// Verify CRC-32 value of zip entry or not.
        /// </summary>
        public bool IsVerifyCrc32 { get; set; }
        /// <summary>
        /// Overwrite original files.
        /// </summary>
        public bool IsOverwrite { get; set; }
        /// <summary>
        /// Do the replacement even if the size of the recompressed data is larger than the size of the original data.
        /// </summary>
        public bool IsReplaceForce { get; set; }
        /// <summary>
        /// Don't save any files, just see the console output.
        /// </summary>
        public bool IsDryRun { get; set; }
        /// <summary>
        /// True if <see cref="IsOverwrite"/> is false and <see cref="IsDryRun"/> is false.
        /// </summary>
        public bool IsCreateNewFile
        {
            get { return !IsOverwrite && !IsDryRun; }
        }

        /// <summary>
        /// Initialize all members.
        /// </summary>
        /// <param name="numberOfThreads">Number of threads for re-compressing. -1 means unlimited.</param>
        /// <param name="password">Password of zip archive.</param>
        /// <param name="isForceCompress">Force compress non compressed data.</param>
        /// <param name="isVerifyCrc32">Verify CRC-32 value of zip entry or not.</param>
        /// <param name="isOverwrite">Overwrite original files.</param>
        /// <param name="isReplaceForce">Do the replacement even if the size of the recompressed data is larger than the size of the original data.</param>
        /// <param name="isDryRun">Don't save any files, just see the console output.</param>
        public ExecuteOptions(
            int numberOfThreads = DefaultNumberOfThreads,
            string? password = null,
            bool isForceCompress = false,
            bool isVerifyCrc32 = false,
            bool isOverwrite = false,
            bool isReplaceForce = false,
            bool isDryRun = false)
        {
            NumberOfThreads = numberOfThreads;
            Password = password;
            IsForceCompress = isForceCompress;
            IsVerifyCrc32 = isVerifyCrc32;
            IsOverwrite = isOverwrite;
            IsReplaceForce = isReplaceForce;
            IsDryRun = isDryRun;
        }

        /// <summary>
        /// Clone this instance.
        /// </summary>
        /// <returns>Cloned instance.</returns>
        public object Clone()
        {
            return new ExecuteOptions(
                NumberOfThreads,
                Password,
                IsForceCompress,
                IsVerifyCrc32,
                IsOverwrite,
                IsReplaceForce,
                IsDryRun);
        }
    }
}
