using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.X86;
using System.Security;
using System.Text;
using System.Threading.Tasks;

using RecompressZip.Zip;

using ArgumentParserSharp;
using NLog;
using ZopfliSharp;


namespace RecompressZip
{
    /// <summary>
    /// Zip recompression tool.
    /// </summary>
    static class Program
    {
        /// <summary>
        /// Chunk type string of PNG, "IDAT".
        /// </summary>
        private const string ChunkNameIdat = "IDAT";
        /// <summary>
        /// Chunk type string of PNG, "IEND".
        /// </summary>
        private const string ChunkNameIend = "IEND";
        /// <summary>
        /// Signature of PNG file.
        /// </summary>
        private static readonly byte[] _pngSignature;
        /// <summary>
        /// Logging instance.
        /// </summary>
        private static readonly Logger _logger;
        /// <summary>
        /// <see cref="TaskFactory"/> with an upper limit on the number of tasks that can be executed concurrently
        /// by <see cref="LimitedConcurrencyLevelTaskScheduler"/>.
        /// </summary>
        private static TaskFactory _taskFactory;
        /// <summary>
        /// Cache of system encoding.
        /// </summary>
        private static Encoding? _systemEncoding;
        /// <summary>
        /// Cache of password encoding.
        /// </summary>
        private static Encoding _passwordEncoding;


        /// <summary>
        /// Setup DLL search path and logging instance.
        /// </summary>
        static Program()
        {
            var dllDir = Path.Combine(
                AppContext.BaseDirectory,
                Environment.Is64BitProcess ? "x64" : "x86");
            UnsafeNativeMethods.AddDllDirectory(dllDir);
            if (Avx2.IsSupported)
            {
                UnsafeNativeMethods.AddDllDirectory(Path.Combine(dllDir, "avx2"));
            }
            UnsafeNativeMethods.SetDefaultDllDirectories(LoadLibrarySearchFlags.DefaultDirs);

            _pngSignature = new byte[] { 0x89, (byte)'P', (byte)'N', (byte)'G', 0x0d, 0x0a, 0x1a, 0x0a };
            _logger = LogManager.GetCurrentClassLogger();
            _taskFactory = new TaskFactory();
            _passwordEncoding = Encoding.Default;
        }


        /// <summary>
        /// An entry point of this program.
        /// </summary>
        /// <param name="args">Command-line arguments.</param>
        /// <returns>Status code.</returns>
        static int Main(string[] args)
        {
            var (targets, zopfliOptions, execOptions) = ParseCommadLineArguments(args);
            ShowParameters(zopfliOptions, execOptions);

            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            if (execOptions.PasswordEncodingName != null)
            {
                _passwordEncoding = Encoding.GetEncoding(execOptions.PasswordEncodingName);
            }

            if (execOptions.NumberOfThreads > 0)
            {
                _taskFactory = new TaskFactory(new LimitedConcurrencyLevelTaskScheduler(execOptions.NumberOfThreads));
            }

            foreach (var target in targets)
            {
                var zipFilePath = target;
                if (Directory.Exists(target))
                {
                    zipFilePath += ".zip";

                    if (File.Exists(zipFilePath))
                    {
                        File.Delete(zipFilePath);
                    }

                    _logger.Info("Compress directory: {0} to {1} ...", target, zipFilePath);

                    var sw = Stopwatch.StartNew();
                    ZipFile.CreateFromDirectory(target, zipFilePath, CompressionLevel.Fastest, true);
                    _logger.Info("Compress directory: {0} to {1} done: {2:F3} seconds", target, zipFilePath, sw.ElapsedMilliseconds / 1000.0);
                }
                if (!File.Exists(zipFilePath))
                {
                    _logger.Fatal("Specified file doesn't exist: {0}", zipFilePath);
                    continue;
                }
                try
                {
                    if (IsZipFile(zipFilePath))
                    {
                        RecompressZip(zipFilePath, zopfliOptions, execOptions);
                    }
                    else if (IsGZipFile(zipFilePath))
                    {
                        RecompressGZip(zipFilePath, zopfliOptions, execOptions);
                    }
                    else if (IsPngFile(zipFilePath))
                    {
                        RecompressPng(zipFilePath, zopfliOptions, execOptions);
                    }
                    else
                    {
                        _logger.Fatal("Specified file isn't neither zip archive nor gzip file: {0}", zipFilePath);
                    }
                }
                catch (Exception ex)
                {
                    _logger.Fatal(ex, "An exception occured:");
                }
            }

            return 0;
        }

        /// <summary>
        /// Parse command line arguments and retrieve the result.
        /// </summary>
        /// <param name="args">Command-line arguments</param>
        /// <returns>Parse result tuple.</returns>
        private static (List<string> Targets, ZopfliOptions zopfliOptions, ExecuteOptions ExecOptions) ParseCommadLineArguments(string[] args)
        {
            var ap = new ArgumentParser()
            {
                Description = "<<< Zip Re-compressor using zopfli >>>"
            };

            var indent1 = ap.IndentString;
            var indent2 = indent1 + indent1;

            var zo = ZopfliOptions.GetDefault();

            ap.Add('b', "block-split-max", OptionType.RequiredArgument,
                "Maximum amount of blocks to split into (0 for unlimited, but this can give extreme results that hurt compression on some files).\n"
                + indent2 + "Default: 15", "NUM", zo.BlockSplittingMax);
            ap.Add('d', "dry-run", "Don't save any files, just see the console output.");
            ap.Add('f', "compress-force", "Compress no-compressed data.");
            ap.AddHelp();
            ap.Add('i', "num-iteration", OptionType.RequiredArgument,
                "Maximum amount of times to rerun forward and backward pass to optimize LZ77 compression cost.\n"
                + indent2 + "Default: 15",
                "NUM", zo.NumIterations);
            ap.Add('n', "num-thread", OptionType.RequiredArgument,
                "Number of threads for re-compressing. 0 or negative value means unlimited.",
                "N", ExecuteOptions.DefaultNumberOfThreads);
            ap.Add('p', "password", OptionType.RequiredArgument, "Specify password of zip archive", "PASSWORD");
            ap.Add('r', "replace-force", "Do the replacement even if the size of the recompressed data is larger than the size of the original data.");
            ap.Add('v', "verbose", "Allow to output to stdout from zopfli.dll.");
            ap.Add('V', "verbose-more", "Allow to output more information to stdout from zopfli.dll.");
            ap.Add("no-block-split", "Don't splits the data in multiple deflate blocks with optimal choice for the block boundaries.");
            ap.Add("no-overwrite", "Don't overwrite PNG files and create images to new zip archive file or directory.");
            ap.Add("password-encoding", OptionType.RequiredArgument, "Encoding of password.", "ENCODING");
            ap.Add("verify-crc32", "Verify CRC-32 value of each zip entry.");

            ap.Parse(args);

            if (ap.GetValue<bool>('h'))
            {
                ap.ShowUsage();
                Environment.Exit(0);
            }

            var targets = ap.Arguments;
            if (targets.Count == 0)
            {
                Console.Error.WriteLine("Please specify one or more zip files.");
                Environment.Exit(0);
            }

            zo.NumIterations = ap.GetValue<int>('i');
            zo.BlockSplitting = !ap.GetValue<bool>("no-block-split");
            zo.BlockSplittingMax = ap.GetValue<int>('b');
            zo.Verbose = ap.GetValue<bool>('v');
            zo.VerboseMore = ap.GetValue<bool>('V');

            return (
                targets,
                zo,
                new ExecuteOptions(
                    ap.GetValue<int>('n'),
                    ap.GetValue('p'),
                    ap.GetValue("password-encoding"),
                    ap.GetValue<bool>('f'),
                    ap.GetValue<bool>("verify-crc32"),
                    !ap.GetValue<bool>("no-overwrite"),
                    ap.GetValue<bool>('r'),
                    ap.GetValue<bool>('d')));
        }

        /// <summary>
        /// Output zopfli and execution options.
        /// </summary>
        /// <param name="zopfliOptions">Options for zopfli</param>
        /// <param name="execOptions">Options for execution.</param>
        private static void ShowParameters(in ZopfliOptions zopfliOptions, ExecuteOptions execOptions)
        {
            Console.WriteLine("- - - Zopfli Parameters - - -");
            Console.WriteLine($"Number of Iterations: {zopfliOptions.NumIterations}");
            Console.WriteLine($"Block Splitting: {zopfliOptions.BlockSplitting}");
            Console.WriteLine($"Block Splitting Max: {zopfliOptions.BlockSplittingMax}");
            Console.WriteLine($"Verbose: {zopfliOptions.Verbose}");
            Console.WriteLine($"Verbose More: {zopfliOptions.VerboseMore}");

            Console.WriteLine("- - - Execution Parameters - - -");
            Console.WriteLine($"Number of Threads: {execOptions.NumberOfThreads}");
            Console.WriteLine($"Password: {(execOptions.Password == null ? "Not specified" : "Specified")}");
            Console.WriteLine($"Verify CRC-32: {execOptions.IsVerifyCrc32}");
            Console.WriteLine($"Overwrite: {execOptions.IsOverwrite}");
            Console.WriteLine($"Replace Force: {execOptions.IsReplaceForce}");
            Console.WriteLine($"Dry Run: {execOptions.IsDryRun}");

            Console.WriteLine("- - -");
        }

        /// <summary>
        /// <para>Identify zip archive file or not.</para>
        /// <para>Just determine if the first four bytes are 'P', 'K', 0x03 and 0x04.</para>
        /// </summary>
        /// <param name="zipFilePath">Target zip file path,</param>
        /// <returns>True if specified file is a zip archive file, otherwise false.</returns>
        private static bool IsZipFile(string zipFilePath)
        {
            Span<byte> buffer = stackalloc byte[4];
            using (var fs = new FileStream(zipFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                if (fs.Read(buffer) < buffer.Length)
                {
                    return false;
                }
            }
            return HasZipSignature(buffer);
        }

        /// <summary>
        /// <para>Identify the specified binary data has a zip signature or not.</para>
        /// <para>Just determine if the first four bytes are 'P', 'K', 0x03 and 0x04.</para>
        /// </summary>
        /// <param name="data">Binary data</param>
        /// <returns>True if the specified binary has a zip signature, otherwise false.</returns>
        private static bool HasZipSignature(ReadOnlySpan<byte> data)
        {
            return data.Length >= 4
                && data[0] == 'P'
                && data[1] == 'K'
                && data[2] == 0x03
                && data[3] == 0x04;
        }

        /// <summary>
        /// <para>Identify gzip compressed file or not.</para>
        /// <para>Just determine if the first three bytes are 0x1f, 0x8b and 0x08.</para>
        /// </summary>
        /// <param name="zipFilePath">Target gzip compressed file path,</param>
        /// <returns>True if specified file is a gzip compressed file, otherwise false.</returns>
        private static bool IsGZipFile(string zipFilePath)
        {
            Span<byte> buffer = stackalloc byte[3];
            using (var fs = new FileStream(zipFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                if (fs.Read(buffer) < buffer.Length)
                {
                    return false;
                }
            }
            return HasGZipSignature(buffer);
        }

        /// <summary>
        /// <para>Identify gzip compressed file or not.</para>
        /// <para>Just determine if the first three bytes are 0x1f, 0x8b and 0x08.</para>
        /// </summary>
        /// <param name="data">Binary data</param>
        /// <returns>True if the specified binary has a gzip signature, otherwise false.</returns>
        private static bool HasGZipSignature(ReadOnlySpan<byte> data)
        {
            return data.Length >= 3
                && data[0] == 0x1f
                && data[1] == 0x8b
                && data[2] == 0x08;
        }

        /// <summary>
        /// <para>Identify PNG file or not.</para>
        /// <para>Just determine if the first eight bytes are equals to <see cref="_pngSignature"/>.</para>
        /// </summary>
        /// <param name="pngFilePath">Target PNG file path,</param>
        /// <returns>True if specified file is a gzip compressed file, otherwise false.</returns>
        private static bool IsPngFile(string zipFilePath)
        {
            Span<byte> buffer = stackalloc byte[_pngSignature.Length];
            using (var fs = new FileStream(zipFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                if (fs.Read(buffer) < buffer.Length)
                {
                    return false;
                }
            }
            return HasPngSignature(buffer);
        }

        /// <summary>
        /// Identify the specified binary data has a PNG signature or not.
        /// </summary>
        /// <param name="data">Binary data</param>
        /// <returns>True if the specified binary has a PNG signature, otherwise false.</returns>
        private static bool HasPngSignature(ReadOnlySpan<byte> data)
        {
            var pngSignature = _pngSignature;
            if (data.Length < pngSignature.Length)
            {
                return false;
            }

            for (int i = 0; i < pngSignature.Length; i++)
            {
                if (data[i] != pngSignature[i])
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Recompress zip file.
        /// </summary>
        /// <param name="srcFilePath">Source zip file path.</param>
        /// <param name="zopfliOptions">Options for zopfli.</param>
        /// <param name="execOptions">Options for execution.</param>
        private static void RecompressZip(string srcFilePath, in ZopfliOptions zopfliOptions, ExecuteOptions execOptions)
        {
            var dstFilePath = execOptions.IsDryRun ? null : Path.Combine(
                Path.GetDirectoryName(srcFilePath) ?? "",
                Path.GetFileNameWithoutExtension(srcFilePath) + ".zopfli" + Path.GetExtension(srcFilePath));
            RecompressZip(srcFilePath, dstFilePath, zopfliOptions, execOptions);
        }

        /// <summary>
        /// Recompress zip file.
        /// </summary>
        /// <param name="srcFilePath">Source zip file path.</param>
        /// <param name="dstFilePath">Destination zip file path.</param>
        /// <param name="zopfliOptions">Options for zopfli.</param>
        /// <param name="execOptions">Options for execution.</param>
        private static void RecompressZip(string srcFilePath, string? dstFilePath, in ZopfliOptions zopfliOptions, ExecuteOptions execOptions)
        {
            var isRecompressDone = false;
            try
            {
                _logger.Info("Recompress {0} start", srcFilePath);

                var srcFileSize = new FileInfo(srcFilePath).Length;
                var totalSw = Stopwatch.StartNew();
                int entryCount;
                long dstFileSize;

                using (var ims = new MemoryStream((int)srcFileSize))
                {
                    using (var ifs = new FileStream(srcFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        ifs.CopyTo(ims);
                    }
                    ims.Position = 0;
                    using (var ofs = dstFilePath == null ? (Stream)new MemoryStream((int)srcFileSize)
                        : new FileStream(dstFilePath, FileMode.Create, FileAccess.Write, FileShare.Read))
                    {
                        entryCount = RecompressZip(ims, ofs, zopfliOptions, execOptions);
                        dstFileSize = ofs.Length;
                    }
                }

                isRecompressDone = true;

                _logger.Log(
                    dstFileSize < srcFileSize ? LogLevel.Info : LogLevel.Warn,
                    "Recompress {0} done: {1:F3} MiB -> {2:F3} MiB (deflated {3:F2}%, {4:F3} seconds, {5} files)",
                    srcFilePath,
                    ToMiB(srcFileSize),
                    ToMiB(dstFileSize),
                    CalcDeflatedRate(srcFileSize, dstFileSize) * 100.0,
                    totalSw.ElapsedMilliseconds / 1000.0,
                    entryCount);

                if (dstFilePath != null && execOptions.IsOverwrite)
                {
                    File.Delete(srcFilePath);
                    File.Move(dstFilePath, srcFilePath);
                }
            }
            catch
            {
                if (dstFilePath != null && !isRecompressDone)
                {
                    File.Delete(dstFilePath);
                }
                throw;
            }
        }

        /// <summary>
        /// Recompress zip file.
        /// </summary>
        /// <param name="srcStream">Source <see cref="Stream"/> of zip file data.</param>
        /// <param name="dstStream">Destination <see cref="Stream"/>.</param>
        /// <param name="zopfliOptions">Options for zopfli.</param>
        /// <param name="execOptions">Options for execution.</param>
        private static int RecompressZip(Stream srcStream, Stream dstStream, in ZopfliOptions zopfliOptions, ExecuteOptions execOptions)
        {
            using var reader = new BinaryReader(srcStream, Encoding.Default, true);
            using var writer = new BinaryWriter(dstStream, Encoding.Default, true);
            return RecompressZip(reader, writer, zopfliOptions, execOptions);
        }

        /// <summary>
        /// Recompress zip file.
        /// </summary>
        /// <param name="reader">Source <see cref="BinaryReader"/> of zip file data.</param>
        /// <param name="writer">Destination <see cref="BinaryWriter"/>.</param>
        /// <param name="zopfliOptions">Options for zopfli.</param>
        /// <param name="execOptions">Options for execution.</param>
        /// <returns>Number of entries in zip file.</returns>
        private static int RecompressZip(BinaryReader reader, BinaryWriter writer, in ZopfliOptions zopfliOptions, ExecuteOptions execOptions)
        {
            var signature = ZipHeader.ReadSignature(reader);

            var taskList = new List<Task<(LocalFileHeader Header, byte[]? cryptHeader, byte[]? CompressedData, SafeBuffer? RecompressedData)>>();
            while (signature == ZipSignature.LocalFileHeader)
            {
                taskList.Add(RecompressEntryAsync(reader, zopfliOptions, execOptions, taskList.Count + 1));
                signature = ZipHeader.ReadSignature(reader);
            }

            // Wait all compression done and write the data.
            var resultList = taskList.Select(task =>
            {
                var offset = (uint)writer.BaseStream.Position;

                var (header, cryptHeader, compressedData, recompressedData) = task.Result;
                header.WriteTo(writer);
                if (cryptHeader != null && header.IsEncrypted)
                {
                    writer.Write(cryptHeader);
                }

                if (recompressedData == null && compressedData == null)
                {
                    throw new InvalidDataException($"Both {nameof(compressedData)} and {nameof(recompressedData)} is null");
                }

                if (recompressedData != null)
                {
                    using (recompressedData)
                    {
                        writer.Write(CreateSpan(recompressedData));
                    }
                }
                else if (compressedData != null)
                {
                    writer.Write(compressedData);
                }
                return new CompressionResult(header, offset);
            }).ToList();

            var resultEnumerator = resultList.GetEnumerator();
            var centralDirOffset = writer.BaseStream.Position;
            while (signature == ZipSignature.CentralDirectoryFileHeader)
            {
                var cdHeader = CentralDirectoryFileHeader.ReadFrom(reader);
                resultEnumerator.MoveNext();
                var cr = resultEnumerator.Current;
                var lfHeader = cr.Header;
                cdHeader.BitFlag = lfHeader.BitFlag;
                cdHeader.Method = lfHeader.Method;
                cdHeader.CompressedLength = lfHeader.CompressedLength;
                cdHeader.Length = lfHeader.Length;
                cdHeader.Offset = cr.Offset;
                cdHeader.WriteTo(writer);

                signature = ZipHeader.ReadSignature(reader);
            }

            if (signature == ZipSignature.EndRecord)
            {
                var header = CentralDirectoryEndRecord.ReadFrom(reader);
                header.Offset = (uint)centralDirOffset;
                header.WriteTo(writer);
            }

            return resultList.Count;
        }

        /// <summary>
        /// <para>Read one local file header and its data, then try to recompress the data.</para>
        /// <para>If the entry is not compressed with DEFLATE or the size of the recompression is greater than or equal to the one before recompression,
        /// return the original data and its header as is.</para>
        /// </summary>
        /// <param name="reader">Source <see cref="BinaryReader"/> of zip file data.</param>
        /// <param name="zopfliOptions">Options for zopfli.</param>
        /// <param name="execOptions">Options for execution.</param>
        /// <param name="procIndex">Process index for logging.</param>
        /// <returns>A tuple of new local file header, crypt header and compressed data.</returns>
        private static async Task<(LocalFileHeader Header, byte[]? cryptHeader, byte[]? CompressedData, SafeBuffer? RecompressedData)> RecompressEntryAsync(BinaryReader reader, ZopfliOptions zopfliOptions, ExecuteOptions execOptions, int procIndex)
        {
            var header = LocalFileHeader.ReadFrom(reader);

            if (header.IsEncrypted && execOptions.Password == null)
            {
                Console.WriteLine("Encrypted entry is found.");
                Console.Write("Please enter password: ");
                execOptions.Password = ReadPassword();
            }

            // Read encrypt header.
            byte[]? cryptHeader = null;
            if (header.IsEncrypted)
            {
                cryptHeader = new byte[ZipCryptor.CryptHeaderSize];
                reader.BaseStream.Read(cryptHeader);
            }
            var cryptHeaderLength = cryptHeader == null ? 0 : cryptHeader.Length;

            var isExistsDataDescriptorSignature = false;
            var dataDesciptorSize = 0;
            if (header.HasDataDescriptor)
            {
                (isExistsDataDescriptorSignature, header.Crc32, header.CompressedLength, header.Length) = FindDataDescriptor(reader);
                dataDesciptorSize = isExistsDataDescriptorSignature ? 16 : 12;
                header.HasDataDescriptor = false;
            }

            var entryName = (header.IsUtf8NameAndComment ? Encoding.UTF8 : GetSystemEncoding()).GetString(header.FileName);

            // Data part is not exists
            if (header.Length == 0)
            {
                var compressedLength = header.CompressedLength;
                if (compressedLength > 0)
                {
                    reader.BaseStream.Position += compressedLength;
                    header.Method = CompressionMethod.NoCompression;
                    header.CompressedLength = 0;
                }
                if (dataDesciptorSize > 0)
                {
                    reader.BaseStream.Position += dataDesciptorSize;
                }

                _logger.Info(
                    "[{0}] No data entry: {1} (Method = {2}: {3}){4}{5}",
                    procIndex,
                    entryName,
                    (ushort)header.Method,
                    header.Method,
                    header.CompressedLength == 0 ? "" : $" (Removed {compressedLength} bytes padding)",
                    dataDesciptorSize == 0 ? "" : $" (Removed {dataDesciptorSize} bytes data descriptor)");

                return (header, cryptHeader, new byte[0], null);
            }

            var compressedData = reader.ReadBytes((int)header.CompressedLength - cryptHeaderLength);

            // Skip data descriptor.
            if (dataDesciptorSize > 0)
            {
                reader.BaseStream.Position += dataDesciptorSize;
            }

            // Is not deflate
            if (header.Method != CompressionMethod.Deflate)
            {
                _logger.Info(
                    "[{0}] Non deflated entry: {1} (Method = {2}: {3})",
                    procIndex,
                    entryName,
                    (ushort)header.Method,
                    header.Method);
                if (header.Method != CompressionMethod.NoCompression || !execOptions.IsForceCompress)
                {
                    return (header, cryptHeader, compressedData, null);
                }
            }

            using (var decompressedMs = new MemoryStream((int)header.Length))
            {
                // Decrypt data if necessary.
                var decryptedCompressedData = compressedData;
                if (header.IsEncrypted)
                {
                    var password = execOptions.Password;
                    if (password == null)
                    {
                        throw new ArgumentNullException(nameof(execOptions.Password), "Encrypted entry is found but no password is specified.");
                    }
                    decryptedCompressedData = ZipDecryptor.DecryptData(compressedData, password, _passwordEncoding, cryptHeader);
                }

                if (header.Method == CompressionMethod.NoCompression)
                {
                    // Copy no deflated data.
                    using (var ims = new MemoryStream(decryptedCompressedData))
                    {
                        ims.CopyTo(decompressedMs);
                    }
                    header.Method = CompressionMethod.Deflate;
                }
                else
                {
                    // Deflate compressed data.
                    using (var compressedMs = new MemoryStream(decryptedCompressedData))
                    using (var dds = new DeflateStream(compressedMs, CompressionMode.Decompress))
                    {
                        try
                        {
                            dds.CopyTo(decompressedMs);
                        }
                        catch (InvalidDataException e)
                        {
                            if (header.IsEncrypted)
                            {
                                throw new InvalidDataException("Password may be incorrect.", e);
                            }
                            else
                            {
                                throw;
                            }
                        }
                    }
                }

                // Verify CRC-32.
                if (execOptions.IsVerifyCrc32 || execOptions.Password != null)
                {
                    VerifyCrc32(CreateSpan(decompressedMs), header.Crc32);
                }

                var recompressedData = await _taskFactory.StartNew(() =>
                {
                    _logger.Info("[{0}] Compress {1} ...", procIndex, entryName);

                    var sw = Stopwatch.StartNew();

                    // Take a long long time ...
                    var recompressedData = Zopfli.CompressUnmanaged(
                        decompressedMs.GetBuffer(),
                        0,
                        (int)decompressedMs.Length,
                        zopfliOptions,
                        ZopfliFormat.Deflate);

                    var byteLength = (int)recompressedData.ByteLength;
                    _logger.Log(
                        byteLength < decryptedCompressedData.Length ? LogLevel.Info : LogLevel.Warn,
                        "[{0}] Compress {1} done: {2:F3} KiB -> {3:F3} KiB (deflated {4:F2}%, {5:F3} seconds){6}",
                        procIndex,
                        entryName,
                        ToKiB(decryptedCompressedData.Length),
                        ToKiB(byteLength),
                        CalcDeflatedRate(decryptedCompressedData.Length, byteLength) * 100.0,
                        sw.ElapsedMilliseconds / 1000.0,
                        dataDesciptorSize == 0 ? "" : $" (Removed {dataDesciptorSize} bytes data descriptor)");

                    // Encrypt recompressed data if necessary.
                    if (header.IsEncrypted && (byteLength < decryptedCompressedData.Length || execOptions.IsReplaceForce))
                    {
                        var password = execOptions.Password;
                        if (password == null)
                        {
                            throw new ArgumentNullException(nameof(execOptions.Password), "Password must no be null to encrypt data.");
                        }
                        var rdSpan = CreateSpan(recompressedData);
                        ZipEncryptor.EncryptData(rdSpan, rdSpan, password, _passwordEncoding, cryptHeader);
                    }

                    return recompressedData;
                });

                var byteLength = recompressedData.ByteLength;
                if (byteLength < (ulong)compressedData.Length || execOptions.IsReplaceForce)
                {
                    header.DeflateCompressionLevel = DeflateCompressionLevels.Maximum;
                    header.CompressedLength = (uint)byteLength + (uint)cryptHeaderLength;
                    return (header, cryptHeader, null, recompressedData);
                }
                else
                {
                    return (header, cryptHeader, compressedData, null);
                }
            }
        }

        /// <summary>
        /// Find data descriptor from current stream position.
        /// </summary>
        /// <param name="reader">A wrapper of <see cref="MemoryStream"/>.</param>
        /// <returns>Data descriptor tuple.</returns>
        /// <exception cref="InvalidDataException">Thrown if data descriptor is not found.</exception>
        private static (bool HasSignature, uint Crc32, uint CompressedLength, uint Length) FindDataDescriptor(BinaryReader reader)
        {
            var ms = (MemoryStream)reader.BaseStream;
            var curPos = ms.Position;
            var data = ms.GetBuffer();

            for (int i = (int)curPos; i + 3 < data.Length; i++)
            {
                // Not equals to first two bytes of signature, 'P' and 'K'.
                if ((ushort)(data[i] | (data[i + 1] << 8)) != 0x4b50)
                {
                    continue;
                }

                var sigPostfix = (ushort)(data[i + 2] | (data[i + 3] << 8));
                if (sigPostfix == 0x0403 || sigPostfix == 0x0201)
                {
                    // Signature of local file header or central directory header is found.
                    ms.Position = i - 12;
                    var dataDescriptor = (false, reader.ReadUInt32(), reader.ReadUInt32(), reader.ReadUInt32());
                    ms.Position = curPos;
                    return dataDescriptor;
                }
                else if (sigPostfix == 0x0807)
                {
                    // Signature of data descriptor is found.
                    ms.Position = i + 4;
                    var dataDescriptor = (true, reader.ReadUInt32(), reader.ReadUInt32(), reader.ReadUInt32());
                    ms.Position = curPos;
                    return dataDescriptor;
                }
            }

            ms.Position = curPos;
            throw new InvalidDataException("Data Descriptor not found.");
        }

        /// <summary>
        /// Get system encoding.
        /// </summary>
        /// <returns>System encoding.</returns>
        private static Encoding GetSystemEncoding()
        {
            return _systemEncoding ??= Encoding.GetEncoding((int)UnsafeNativeMethods.GetACP());
        }

        /// <summary>
        /// Recompress zip compressed file.
        /// </summary>
        /// <param name="srcFilePath">Source zip file path.</param>
        /// <param name="zopfliOptions">Options for zopfli.</param>
        /// <param name="execOptions">Options for execution.</param>
        private static void RecompressGZip(string srcFilePath, in ZopfliOptions zopfliOptions, ExecuteOptions execOptions)
        {
            var dstFilePath = execOptions.IsDryRun ? null
                : execOptions.IsOverwrite ? srcFilePath
                : Path.Combine(
                    Path.GetDirectoryName(srcFilePath) ?? "",
                    Path.GetFileNameWithoutExtension(srcFilePath) + ".zopfli" + Path.GetExtension(srcFilePath));
            RecompressGZip(srcFilePath, dstFilePath, zopfliOptions, execOptions);
        }

        /// <summary>
        /// Recompress gzip compressed file.
        /// </summary>
        /// <param name="srcFilePath">Source zip file path.</param>
        /// <param name="dstFilePath">Destination zip file path.</param>
        /// <param name="zopfliOptions">Options for zopfli.</param>
        /// <param name="execOptions">Options for execution.</param>
        private static void RecompressGZip(string srcFilePath, string? dstFilePath, in ZopfliOptions zopfliOptions, ExecuteOptions execOptions)
        {
            _logger.Info("Recompress {0} start", srcFilePath);

            var srcFileSize = new FileInfo(srcFilePath).Length;
            var totalSw = Stopwatch.StartNew();

            using var decompressedMs = DecompressGZipToMemoryStream(srcFilePath);
            using var recompressedData = Zopfli.CompressUnmanaged(
                 decompressedMs.GetBuffer(),
                 0,
                 (int)decompressedMs.Length,
                 zopfliOptions,
                 ZopfliFormat.GZip);

            _logger.Log(
                (long)recompressedData.ByteLength < srcFileSize ? LogLevel.Info : LogLevel.Warn,
                "Recompress {0} done: {1:F3} MiB -> {2:F3} MiB (deflated {3:F2}%, {4:F3} seconds)",
                srcFilePath,
                ToMiB(srcFileSize),
                ToMiB((long)recompressedData.ByteLength),
                CalcDeflatedRate(srcFileSize, (long)recompressedData.ByteLength) * 100.0,
                totalSw.ElapsedMilliseconds / 1000.0);

            if (dstFilePath == null)
            {
                return;
            }

            if ((long)recompressedData.ByteLength < srcFileSize || execOptions.IsReplaceForce)
            {
                using var ofs = new FileStream(dstFilePath, FileMode.Create, FileAccess.Write, FileShare.Read);
                ofs.Write(CreateSpan(recompressedData));
            }
            else if (srcFilePath != dstFilePath)
            {
                File.Copy(srcFilePath, dstFilePath, true);
            }
        }

        /// <summary>
        /// Decompress gzip compressed file to <see cref="MemoryStream"/>.
        /// </summary>
        /// <param name="gzipFilePath">Gzip compressed file path</param>
        /// <returns><see cref="MemoryStream"/> of decompress data.</returns>
        private static MemoryStream DecompressGZipToMemoryStream(string gzipFilePath)
        {
            using var ifs = new FileStream(gzipFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            return DecompressGZipToMemoryStream(ifs);
        }

        /// <summary>
        /// Decompress gzip compressed data in specified <see cref="Stream"/> to <see cref="MemoryStream"/>.
        /// </summary>
        /// <param name="gzipCompressedStream">Gzip compressed file path</param>
        /// <returns><see cref="MemoryStream"/> of decompress data.</returns>
        private static MemoryStream DecompressGZipToMemoryStream(Stream gzipCompressedStream)
        {
            var pos = gzipCompressedStream.Position;
            gzipCompressedStream.Seek(-4, SeekOrigin.End);

            Span<byte> buf = stackalloc byte[4];
            var nRead = gzipCompressedStream.Read(buf);

            gzipCompressedStream.Position = pos;

            if (nRead < buf.Length)
            {
                ThrowInvalidDataException("Failed to read original data size of gzip.");
            }

            var decompressedSize = buf[0] | (buf[1] << 8) | (buf[2] << 16) | (buf[3] << 24);

            var decompressedMs = new MemoryStream(decompressedSize);
            using (var dgs = new GZipStream(gzipCompressedStream, CompressionMode.Decompress))
            {
                dgs.CopyTo(decompressedMs);
            }

            if (decompressedSize != decompressedMs.Length)
            {
                _logger.Warn(
                    "The decompressed size recorded in the gzip file differs from the actual decompressed size: {0} Bytes and {1} Bytes.",
                    decompressedSize,
                    decompressedMs.Length);
            }

            return decompressedMs;
        }

        /// <summary>
        /// Recompress PNG file.
        /// </summary>
        /// <param name="srcFilePath">Source PNG file path.</param>
        /// <param name="zopfliOptions">Options for zopfli.</param>
        /// <param name="execOptions">Options for execution.</param>
        private static void RecompressPng(string srcFilePath, in ZopfliOptions zopfliOptions, ExecuteOptions execOptions)
        {
            var dstFilePath = execOptions.IsDryRun ? null
                : execOptions.IsOverwrite ? srcFilePath
                : Path.Combine(
                    Path.GetDirectoryName(srcFilePath) ?? "",
                    Path.GetFileNameWithoutExtension(srcFilePath) + ".zopfli" + Path.GetExtension(srcFilePath));
            RecompressPng(srcFilePath, dstFilePath, zopfliOptions, execOptions);
        }

        /// <summary>
        /// Recompress PNG file.
        /// </summary>
        /// <param name="srcFilePath">Source PNG file path.</param>
        /// <param name="dstFilePath">Destination PNG file path.</param>
        /// <param name="zopfliOptions">Options for zopfli.</param>
        /// <param name="execOptions">Options for execution.</param>
        private static void RecompressPng(string srcFilePath, string? dstFilePath, in ZopfliOptions zopfliOptions, ExecuteOptions execOptions)
        {
            _logger.Info("Recompress {0} start", srcFilePath);

            var srcFileSize = new FileInfo(srcFilePath).Length;
            var totalSw = Stopwatch.StartNew();

            using var oms = new MemoryStream((int)srcFileSize);

            using (var ifs = new FileStream(srcFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                RecompressPng(ifs, oms, zopfliOptions, execOptions);
            }

            var recompressedData = CreateSpan(oms);

            _logger.Log(
                recompressedData.Length < srcFileSize ? LogLevel.Info : LogLevel.Warn,
                "Recompress {0} done: {1:F3} MiB -> {2:F3} MiB (deflated {3:F2}%, {4:F3} seconds)",
                srcFilePath,
                ToMiB(srcFileSize),
                ToMiB(recompressedData.Length),
                CalcDeflatedRate(srcFileSize, recompressedData.Length) * 100.0,
                totalSw.ElapsedMilliseconds / 1000.0);

            if (dstFilePath == null)
            {
                return;
            }

            if (recompressedData.Length < srcFileSize || execOptions.IsReplaceForce)
            {
                using var ofs = new FileStream(dstFilePath, FileMode.Create, FileAccess.Write, FileShare.Read);
                ofs.Write(recompressedData);
            }
            else if (srcFilePath != dstFilePath)
            {
                File.Copy(srcFilePath, dstFilePath, true);
            }
        }

        /// <summary>
        /// Recompress PNG file.
        /// </summary>
        /// <param name="srcStream">Source <see cref="Stream"/> of PNG file data.</param>
        /// <param name="dstStream">Destination <see cref="Stream"/>.</param>
        /// <param name="zopfliOptions">Options for zopfli.</param>
        /// <param name="execOptions">Options for execution.</param>
        private static void RecompressPng(Stream srcStream, Stream dstStream, in ZopfliOptions zopfliOptions, ExecuteOptions execOptions)
        {
            using var reader = new BinaryReader(srcStream, Encoding.Default, true);
            using var writer = new BinaryWriter(dstStream, Encoding.Default, true);
            RecompressPng(reader, writer, zopfliOptions, execOptions);
        }

        /// <summary>
        /// Recompress PNG file.
        /// </summary>
        /// <param name="reader">Source <see cref="BinaryReader"/> of PNG file data.</param>
        /// <param name="writer">Destination <see cref="BinaryWriter"/>.</param>
        /// <param name="zopfliOptions">Options for zopfli.</param>
        /// <param name="execOptions">Options for execution.</param>
        private static void RecompressPng(BinaryReader reader, BinaryWriter writer, in ZopfliOptions zopfliOptions, ExecuteOptions execOptions)
        {
            Span<byte> signature = stackalloc byte[8];
            Span<byte> chunkTypeData = stackalloc byte[4];
            var buffer = new byte[81920];
            string? chunkType;

            reader.Read(signature);
            if (!HasPngSignature(signature))
            {
                ThrowInvalidDataException("First eight byte of data stream isn't PNG signature");
            }
            writer.Write(signature);

            do
            {
                var dataLength = BinaryPrimitives.ReverseEndianness(reader.ReadUInt32());

                if (reader.Read(chunkTypeData) < chunkTypeData.Length)
                {
                    ThrowInvalidDataException("Failed to read chunk type.");
                }
                chunkType = Encoding.ASCII.GetString(chunkTypeData);

                if (chunkType == ChunkNameIdat)
                {
                    // Combine all IDAT data.
                    var ims = new MemoryStream((int)dataLength);
                    do
                    {
                        var idatData = reader.ReadBytes((int)dataLength);
                        ims.Write(idatData);
                        reader.ReadInt32();  // Skip CRC-32

                        dataLength = BinaryPrimitives.ReverseEndianness(reader.ReadUInt32());
                        if (reader.Read(chunkTypeData) < chunkTypeData.Length)
                        {
                            ThrowInvalidDataException("Failed to read chunk type.");
                        }
                        chunkType = Encoding.ASCII.GetString(chunkTypeData);
                    } while (chunkType == ChunkNameIdat);

                    // Recompress combined IDAT data.
                    ims.Position = 0;
                    using var recompressedData = RecompressZLibFormatData(ims, zopfliOptions);
                    if (recompressedData.ByteLength >= (ulong)ims.Length)
                    {
                        _logger.Warn("Recompressed data size is large than original: {0} Bytes / {1} Bytes", recompressedData.ByteLength, ims.Length);
                    }

                    // Write new IDAT chunks.
                    WriteChunk(writer, ChunkNameIdat, CreateSpan(recompressedData));
                }

                // Copy current chunk
                writer.Write(BinaryPrimitives.ReverseEndianness(dataLength));
                writer.Write(chunkTypeData);

                // Data + CRC-32
                var remLength = (int)dataLength + 4;
                buffer = EnsureCapacity(buffer, remLength);
                if (reader.BaseStream.Read(buffer, 0, remLength) < remLength)
                {
                    ThrowInvalidDataException("Failed to read chunk data and CRC.");
                }
                writer.BaseStream.Write(buffer, 0, remLength);
            } while (chunkType != ChunkNameIend);
        }

        /// <summary>
        /// Decompress data which compressed with zlib format and recompress the data.
        /// </summary>
        /// <param name="ims">Zlib format data.</param>
        /// <param name="zopfliOptions">Options for zopfli.</param>
        /// <returns>Unmanaged memory handle of recompressed data.</returns>
        private static SafeBuffer RecompressZLibFormatData(MemoryStream ims, in ZopfliOptions zopfliOptions)
        {
            using var oms = new MemoryStream((int)ims.Length);

            using (var izs = new ZLibStream(ims, CompressionMode.Decompress, true))
            {
                izs.CopyTo(oms);
            }
            return Zopfli.CompressUnmanaged(
                oms.GetBuffer(),
                0,
                (int)oms.Length,
                zopfliOptions,
                ZopfliFormat.ZLib);
        }

        /// <summary>
        /// Write one PNG chunk data.
        /// </summary>
        /// <param name="bw"><see cref="BinaryWriter"/> of data destination.</param>
        /// <param name="chunkType">Chunk type name.</param>
        /// <param name="chunkData">Chunk data.</param>
        private static void WriteChunk(BinaryWriter bw, string chunkType, Span<byte> chunkData)
        {
            WriteChunk(bw, Encoding.ASCII.GetBytes(chunkType), chunkData);
        }

        /// <summary>
        /// Write one PNG chunk data.
        /// </summary>
        /// <param name="bw"><see cref="BinaryWriter"/> of data destination.</param>
        /// <param name="chunkTypeAscii">Chunk type name byte sequance.</param>
        /// <param name="chunkData">Chunk data.</param>
        private static void WriteChunk(BinaryWriter bw, ReadOnlySpan<byte> chunkTypeAscii, Span<byte> chunkData)
        {
            bw.Write(BinaryPrimitives.ReverseEndianness(chunkData.Length));
            bw.Write(chunkTypeAscii);
            bw.Write(chunkData);

            var crc = Crc32Calculator.Update(chunkTypeAscii);
            crc = Crc32Calculator.Update(chunkData, crc);
            crc = Crc32Calculator.Finalize(crc);
            bw.Write(BinaryPrimitives.ReverseEndianness(crc));
        }

        /// <summary>
        /// Ensures that the capacity of <paramref name="data"/> is at least the specified value, <paramref name="required"/>.
        /// </summary>
        /// <param name="data">Souce <see cref="byte"/> array.</param>
        /// <param name="required">Required capacity</param>
        /// <returns><paramref name="data"/> if <c><paramref name="data"/>.Length &gt;= <paramref name="required"/></c>,
        /// otherwise new allocated <see cref="byte"/> array.</returns>
        private static byte[] EnsureCapacity(byte[] data, int required)
        {
            return data.Length < required ? new byte[required] : data;
        }

        /// <summary>
        /// Read password from stdin.
        /// </summary>
        /// <returns>Inputted password.</returns>
        private static string ReadPassword()
        {
            var sb = new StringBuilder();
            while (true)
            {
                var cki = Console.ReadKey(true);
                if (cki.Key == ConsoleKey.Enter)
                {
                    Console.WriteLine();
                    break;
                }
                else if (cki.Key == ConsoleKey.Backspace)
                {
                    if (sb.Length > 0)
                    {
                        Console.Write("\b\0\b");
                        sb.Length--;
                    }
                    continue;
                }
                else if (!IsCharKey(cki))
                {
                    continue;
                }
                Console.Write('*');
                sb.Append(cki.KeyChar);
            }

            return sb.ToString();
        }

        /// <summary>
        /// Identify inputted key is character or not.
        /// </summary>
        /// <param name="cki">Inputted key information.</param>
        /// <returns>True if inputted key is a character, otherwise false.</returns>
        private static bool IsCharKey(ConsoleKeyInfo cki)
        {
            switch (cki.Key)
            {
                case ConsoleKey.Backspace:
                case ConsoleKey.Tab:
                case ConsoleKey.Clear:
                case ConsoleKey.Enter:
                case ConsoleKey.Pause:
                case ConsoleKey.Escape:
                case ConsoleKey.Spacebar:
                case ConsoleKey.PageUp:
                case ConsoleKey.PageDown:
                case ConsoleKey.End:
                case ConsoleKey.Home:
                case ConsoleKey.LeftArrow:
                case ConsoleKey.UpArrow:
                case ConsoleKey.RightArrow:
                case ConsoleKey.DownArrow:
                case ConsoleKey.Select:
                case ConsoleKey.Print:
                case ConsoleKey.Execute:
                case ConsoleKey.PrintScreen:
                case ConsoleKey.Insert:
                case ConsoleKey.Delete:
                case ConsoleKey.Help:
                case ConsoleKey.LeftWindows:
                case ConsoleKey.RightWindows:
                case ConsoleKey.Applications:
                case ConsoleKey.Sleep:
                case ConsoleKey.F1:
                case ConsoleKey.F2:
                case ConsoleKey.F3:
                case ConsoleKey.F4:
                case ConsoleKey.F5:
                case ConsoleKey.F6:
                case ConsoleKey.F7:
                case ConsoleKey.F8:
                case ConsoleKey.F9:
                case ConsoleKey.F10:
                case ConsoleKey.F11:
                case ConsoleKey.F12:
                case ConsoleKey.F13:
                case ConsoleKey.F14:
                case ConsoleKey.F15:
                case ConsoleKey.F16:
                case ConsoleKey.F17:
                case ConsoleKey.F18:
                case ConsoleKey.F19:
                case ConsoleKey.F20:
                case ConsoleKey.F21:
                case ConsoleKey.F22:
                case ConsoleKey.F23:
                case ConsoleKey.F24:
                case ConsoleKey.BrowserBack:
                case ConsoleKey.BrowserForward:
                case ConsoleKey.BrowserRefresh:
                case ConsoleKey.BrowserStop:
                case ConsoleKey.BrowserSearch:
                case ConsoleKey.BrowserFavorites:
                case ConsoleKey.BrowserHome:
                case ConsoleKey.VolumeMute:
                case ConsoleKey.VolumeDown:
                case ConsoleKey.VolumeUp:
                case ConsoleKey.MediaNext:
                case ConsoleKey.MediaPrevious:
                case ConsoleKey.MediaStop:
                case ConsoleKey.MediaPlay:
                case ConsoleKey.LaunchMail:
                case ConsoleKey.LaunchMediaSelect:
                case ConsoleKey.LaunchApp1:
                case ConsoleKey.LaunchApp2:
                case ConsoleKey.Process:
                case ConsoleKey.Packet:
                case ConsoleKey.Attention:
                case ConsoleKey.CrSel:
                case ConsoleKey.ExSel:
                case ConsoleKey.EraseEndOfFile:
                case ConsoleKey.Play:
                case ConsoleKey.Zoom:
                case ConsoleKey.NoName:
                case ConsoleKey.Pa1:
                case ConsoleKey.OemClear:
                    return false;
                default:
                    return true;
            }
        }

        /// <summary>
        /// Create <see cref="Span{T}"/> from <see cref="MemoryStream"/>.
        /// </summary>
        /// <param name="ms">An instance of <see cref="MemoryStream"/>.</param>
        /// <returns><see cref="Span{T}"/> of <paramref name="ms"/>.</returns>
        private static Span<byte> CreateSpan(MemoryStream ms)
        {
            return ms.GetBuffer().AsSpan(0, (int)ms.Length);
        }

        /// <summary>
        /// Create <see cref="Span{T}"/> from <see cref="SafeBuffer"/>.
        /// </summary>
        /// <param name="sb">An instance of <see cref="SafeBuffer"/>.</param>
        /// <returns><see cref="Span{T}"/> of <paramref name="sb"/>.</returns>
        private static unsafe Span<byte> CreateSpan(SafeBuffer sb)
        {
            return new Span<byte>((void*)sb.DangerousGetHandle(), (int)sb.ByteLength);
        }

        /// <summary>
        /// <para>Verify CRC-32 value of data.</para>
        /// <para>Throw <see cref="InvalidDataException"/> if invalid CRC-32 value is detected.</para>
        /// </summary>
        /// <param name="data">Data to check.</param>
        /// <param name="crc32">Expected CRC-32 value.</param>
        private static void VerifyCrc32(ReadOnlySpan<byte> data, uint crc32)
        {
            var actualCrc32 = Crc32Calculator.Compute(data);
            if (actualCrc32 != crc32)
            {
                ThrowInvalidDataException($"Invalid CRC-32 value detected. Expected: {crc32:X8}, Actual: {actualCrc32:X8}");
            }
        }

        /// <summary>
        /// Converts a number in bytes to a number in KiB.
        /// </summary>
        /// <param name="byteSize">A number in bytes.</param>
        /// <returns>A number in KiB.</returns>
        private static double ToKiB(long byteSize)
        {
            return byteSize / 1024.0;
        }

        /// <summary>
        /// Converts a number in bytes to a number in MiB.
        /// </summary>
        /// <param name="byteSize">A number in bytes.</param>
        /// <returns>A number in MiB.</returns>
        private static double ToMiB(long byteSize)
        {
            return byteSize / 1024.0 / 1024.0;
        }

        /// <summary>
        /// Calculate deflated rate.
        /// </summary>
        /// <param name="originalSize">Original size.</param>
        /// <param name="compressedSize">Compressed size.</param>
        /// <returns>Deflated rete.</returns>
        private static double CalcDeflatedRate(long originalSize, long compressedSize)
        {
            return 1.0 - (double)compressedSize / originalSize;
        }

        /// <summary>
        /// Throw <see cref="InvalidDataException"/>.
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception.</param>
        /// <exception cref="InvalidDataException">Always thrown.</exception>
        [DoesNotReturn]
        private static void ThrowInvalidDataException(string message)
        {
            throw new InvalidDataException(message);
        }

        /// <summary>
        /// Native methods.
        /// </summary>
        [SuppressUnmanagedCodeSecurity]
        internal class UnsafeNativeMethods
        {
            /// <summary>
            /// Adds a directory to the process DLL search path.
            /// </summary>
            /// <param name="path">Path to DLL directory.</param>
            /// <returns>True if success to set directory, otherwise false.</returns>
            [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
            [SuppressUnmanagedCodeSecurity]
            public static extern bool AddDllDirectory([In] string path);
            /// <summary>
            /// <para>Specifies a default set of directories to search when the calling process loads a DLL.</para>
            /// <para>This search path is used when LoadLibraryEx is called with no <see cref="LoadLibrarySearchFlags"/> flags.</para>
            /// </summary>
            /// <param name="directoryFlags">The directories to search. This parameter can be any combination of the following values.</param>
            /// <returns>If the function succeeds, the return value is true.</returns>
            [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
            [SuppressUnmanagedCodeSecurity]
            public static extern bool SetDefaultDllDirectories(LoadLibrarySearchFlags directoryFlags);
            /// <summary>
            /// Retrieves the current Windows ANSI code page identifier for the operating system.
            /// </summary>
            /// <returns>If the function succeeds, the return value is true.</returns>
            [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
            [SuppressUnmanagedCodeSecurity]
            public static extern uint GetACP();
        }

        /// <summary>
        /// Flag values for <see cref="UnsafeNativeMethods.SetDefaultDllDirectories(LoadLibrarySearchFlags)"/>.
        /// </summary>
        [Flags]
        internal enum LoadLibrarySearchFlags
        {
            /// <summary>
            /// If this value is used, the application's installation directory is searched.
            /// </summary>
            ApplicationDir = 0x00000200,
            /// <summary>
            /// <para>If this value is used, any path explicitly added using the AddDllDirectory or SetDllDirectory function is searched.</para>
            /// <para>If more than one directory has been added, the order in which those directories are searched is unspecified.</para>
            /// </summary>
            UserDirs = 0x00000400,
            /// <summary>
            /// If this value is used, %windows%\system32 is searched.
            /// </summary>
            System32 = 0x00000800,
            /// <summary>
            /// <para>This value is a combination of <see cref="ApplicationDir"/>, <see cref="System32"/>, and <see cref="UserDirs"/>.</para>
            /// <para>This value represents the recommended maximum number of directories an application should include in its DLL search path.</para>
            /// </summary>
            DefaultDirs = 0x00001000
        }
    }
}
