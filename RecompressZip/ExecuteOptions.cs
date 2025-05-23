using System;
using System.Diagnostics.Contracts;


namespace RecompressZip
{
    /// <summary>
    /// Option values class for execution.
    /// </summary>
    /// <remarks>
    /// Primary ctor: Initialize all members.
    /// </remarks>
    /// <param name="numberOfThreads">Number of threads for re-compressing. -1 means unlimited.</param>
    /// <param name="encodingName">Encoding name of non UTF-8 name and comment of zip entries.</param>
    /// <param name="password">Password of zip archive.</param>
    /// <param name="passwordEncodingName">Encoding name of <paramref name="password"/>.</param>
    /// <param name="isForceCompress">Force compress non compressed data.</param>
    /// <param name="isRemoveDirectoryEntries">Remove directory entries or not.</param>
    /// <param name="isVerifyCrc32">Verify CRC-32 value of zip entry or not.</param>
    /// <param name="isOverwrite">Overwrite original files.</param>
    /// <param name="isReplaceForce">Do the replacement even if the size of the recompressed data is larger than the size of the original data.</param>
    /// <param name="isDryRun">Don't save any files, just see the console output.</param>
    public class ExecuteOptions(
        int numberOfThreads = ExecuteOptions.DefaultNumberOfThreads,
        string? encodingName = null,
        string? password = null,
        string? passwordEncodingName = null,
        bool isForceCompress = false,
        bool isRemoveDirectoryEntries = false,
        bool isVerifyCrc32 = false,
        bool isOverwrite = false,
        bool isReplaceForce = false,
        bool isDryRun = false) : ICloneable
    {
        /// <summary>
        /// Default value for <see cref="NumberOfThreads"/>.
        /// </summary>
        public const int DefaultNumberOfThreads = -1;

        /// <summary>
        /// Number of threads for re-compressing. -1 means unlimited.
        /// </summary>
        public int NumberOfThreads { get; set; } = numberOfThreads;
        /// <summary>
        /// Encoding name of non UTF-8 name and comment of zip entries.
        /// </summary>
        public string? EncodingName { get; set; } = encodingName;
        /// <summary>
        /// Password of zip archive.
        /// </summary>
        public string? Password { get; set; } = password;
        /// <summary>
        /// Encoding name of <see cref="Password"/>.
        /// </summary>
        public string? PasswordEncodingName { get; set; } = passwordEncodingName;
        /// <summary>
        /// Force compress non compressed data.
        /// </summary>
        public bool IsForceCompress { get; set; } = isForceCompress;
        /// <summary>
        /// Remove directory entries or not.
        /// </summary>
        public bool IsRemoveDirectoryEntries { get; set; } = isRemoveDirectoryEntries;
        /// <summary>
        /// Verify CRC-32 value of zip entry or not.
        /// </summary>
        public bool IsVerifyCrc32 { get; set; } = isVerifyCrc32;
        /// <summary>
        /// Overwrite original files.
        /// </summary>
        public bool IsOverwrite { get; set; } = isOverwrite;
        /// <summary>
        /// Do the replacement even if the size of the recompressed data is larger than the size of the original data.
        /// </summary>
        public bool IsReplaceForce { get; set; } = isReplaceForce;
        /// <summary>
        /// Don't save any files, just see the console output.
        /// </summary>
        public bool IsDryRun { get; set; } = isDryRun;
        /// <summary>
        /// True if <see cref="IsOverwrite"/> is false and <see cref="IsDryRun"/> is false.
        /// </summary>
        public bool IsCreateNewFile
        {
            get { return !IsOverwrite && !IsDryRun; }
        }

        /// <summary>
        /// Clone this instance.
        /// </summary>
        /// <returns>Cloned instance.</returns>
        [Pure]
        public object Clone()
        {
            return new ExecuteOptions(
                NumberOfThreads,
                EncodingName,
                Password,
                PasswordEncodingName,
                IsForceCompress,
                IsVerifyCrc32,
                IsOverwrite,
                IsReplaceForce,
                IsDryRun);
        }
    }
}
