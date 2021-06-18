using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using System.Threading.Tasks;
using ZopfliSharp;

using NLog;


namespace RecompressZip
{
    /// <summary>
    /// Zip recompression tool.
    /// </summary>
    static class Program
    {
        /// <summary>
        /// Logging instance.
        /// </summary>
        private static readonly Logger _logger;


        /// <summary>
        /// Setup DLL search path and logging instance.
        /// </summary>
        static Program()
        {
            UnsafeNativeMethods.SetDllDirectory(
                Path.Combine(
                    AppContext.BaseDirectory,
                    Environment.Is64BitProcess ? "x64" : "x86"));
            _logger = LogManager.GetCurrentClassLogger();
        }


        /// <summary>
        /// An entry point of this program.
        /// </summary>
        /// <param name="args">Command-line arguments.</param>
        /// <returns>Status code.</returns>
        static int Main(string[] args)
        {
            if (args.Length != 2)
            {
                Console.Error.WriteLine("The number of arguments must be two.");
                return 1;
            }

            RecompressZip(args[0], args[1]);

            return 0;
        }

        /// <summary>
        /// Recompress zip file.
        /// </summary>
        /// <param name="srcFilePath">Source zip file path.</param>
        /// <param name="dstFilePath">Destination zip file path.</param>
        static void RecompressZip(string srcFilePath, string dstFilePath)
        {
            using var ifs = new FileStream(srcFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var ofs = new FileStream(dstFilePath, FileMode.Create, FileAccess.Write, FileShare.Read);
            RecompressZip(ifs, ofs);
        }

        /// <summary>
        /// Recompress zip file.
        /// </summary>
        /// <param name="srcStream">Source <see cref="Stream"/> of zip file data.</param>
        /// <param name="dstStream">Destination <see cref="Stream"/>.</param>
        static void RecompressZip(Stream srcStream, Stream dstStream)
        {
            using var reader = new BinaryReader(srcStream, Encoding.Default, true);
            using var writer = new BinaryWriter(dstStream, Encoding.Default, true);
            RecompressZip(reader, writer);
        }

        /// <summary>
        /// Recompress zip file.
        /// </summary>
        /// <param name="reader">Source <see cref="BinaryReader"/> of zip file data.</param>
        /// <param name="writer">Destination <see cref="BinaryWriter"/>.</param>
        static void RecompressZip(BinaryReader reader, BinaryWriter writer)
        {
            var signature = ReadSignature(reader);

            var taskFactory = new TaskFactory(new LimitedConcurrencyLevelTaskScheduler(Environment.ProcessorCount));
            var taskList = new List<Task<(LocalFileHeader Header, byte[] CompressedData)>>();
            while (signature == (uint)SignatureType.LocalFileHeader)
            {
                taskList.Add(RecompressEntryAsync(reader, taskFactory, taskList.Count));
                signature = ReadSignature(reader);
            }

            // Wait all compression done and write the data.
            var resultList = taskList.Select(task =>
            {
                var offset = (uint)writer.BaseStream.Position;

                var result = task.Result;
                WriteLocalFileHeader(writer, result.Header);
                writer.Write(result.CompressedData);

                return new CompressionResult
                {
                    Offset = offset,
                    CompressedLength = (uint)result.CompressedData.Length,
                    Length = result.Header.Length
                };
            }).ToList();

            int listCnt = 0;
            var centralDirOffset = writer.BaseStream.Position;
            while (signature == (uint)SignatureType.CentralDirectoryFileHeader)
            {
                var header = ReadCentralDirectoryFileHeader(reader);
                var cr = resultList[listCnt++];
                header.CompressedLength = cr.CompressedLength;
                header.Length = cr.Length;
                header.Offset = cr.Offset;
                WriteCentralDirectoryFileHeader(writer, header);

                signature = ReadSignature(reader);
            }

            if (signature == (uint)SignatureType.EndRecord)
            {
                var header = ReadCentralDirectoryEndRecord(reader);
                header.Offset = (uint)centralDirOffset;
                WriteCentralDirectoryEndRecord(writer, header);
            }
        }

        /// <summary>
        /// <para>Read one local file header and its data, then try to recompress the data.</para>
        /// <para>If the entry is not compressed with DEFLATE or the size of the recompression is greater than or equal to the one before recompression,
        /// return the original data and its header as is.</para>
        /// </summary>
        /// <param name="reader">Source <see cref="BinaryReader"/> of zip file data.</param>
        /// <param name="taskFactory">A task factory.</param>
        /// <param name="procIndex">Process index for logging.</param>
        /// <returns>A tuple of new local file header and compressed data.</returns>
        private static async Task<(LocalFileHeader Header, byte[] CompressedData)> RecompressEntryAsync(BinaryReader reader, TaskFactory taskFactory, int procIndex)
        {
            var header = ReadLocalFileHeader(reader);
            header.Signature = (uint)SignatureType.LocalFileHeader;
            var src = reader.ReadBytes((int)header.CompressedLength);

            // Is not deflate
            if (header.Method != 8)
            {
                return (header, src);
            }

            using (var decompressedMs = new MemoryStream((int)header.Length))
            {
                using (var compressedMs = new MemoryStream(src))
                using (var dds = new DeflateStream(compressedMs, CompressionMode.Decompress))
                {
                    dds.CopyTo(decompressedMs);
                }

                var recompressedData = await taskFactory.StartNew(() =>
                {
                    var entryName = Encoding.ASCII.GetString(header.FileName);
                    _logger.Info("[{0}] Compress {1} ...", procIndex, entryName);

                    var sw = Stopwatch.StartNew();

                    // Take a long long time ...
                    var recompressedData = Zopfli.Compress(
                        decompressedMs.GetBuffer(),
                        0,
                        (int)decompressedMs.Length,
                        ZopfliFormat.Deflate);

                    _logger.Log(
                        recompressedData.Length < src.Length ? LogLevel.Info : LogLevel.Warn,
                        "[{0}] Compress {1} done: {2:F3} seconds, {3:F3} KiB -> {4:F3} KiB (deflated {5:F2}%)",
                        procIndex,
                        entryName,
                        sw.ElapsedMilliseconds / 1000.0,
                        ToKiB(src.Length),
                        ToKiB(recompressedData.Length),
                        CalcDeflatedRate(src.Length, recompressedData.Length) * 100.0);

                    return recompressedData;
                });

                if (recompressedData.Length < src.Length)
                {
                    header.CompressedLength = (uint)recompressedData.Length;
                    return (header, recompressedData);
                }
                else
                {
                    return (header, src);
                }
            }
        }

        /// <summary>
        /// Read zip signature, just read 4 bytes.
        /// </summary>
        /// <param name="reader"><see cref="BinaryReader"/> of zip data.</param>
        /// <returns>Signature.</returns>
        static uint ReadSignature(BinaryReader reader)
        {
            return reader.ReadUInt32();
        }

        /// <summary>
        /// Read local file header from <see cref="BinaryReader"/>.
        /// </summary>
        /// <param name="reader"><see cref="BinaryReader"/> of zip data.</param>
        /// <returns>Local file header data.</returns>
        static LocalFileHeader ReadLocalFileHeader(BinaryReader reader)
        {
            var header = new LocalFileHeader
            {
                Signature = (uint)SignatureType.LocalFileHeader,
                VerExtract = reader.ReadUInt16(),
                BitFlag = reader.ReadUInt16(),
                Method = reader.ReadUInt16(),
                LastModificationTime = reader.ReadUInt16(),
                LastModificationDate = reader.ReadUInt16(),
                Crc32 = reader.ReadUInt32(),
                CompressedLength = reader.ReadUInt32(),
                Length = reader.ReadUInt32(),
                FileNameLength = reader.ReadUInt16(),
                ExtraLength = reader.ReadUInt16()
            };
            header.FileName = reader.ReadBytes(header.FileNameLength);
            header.ExtraField = reader.ReadBytes(header.ExtraLength);

            return header;
        }

        /// <summary>
        /// Write specified <see cref="LocalFileHeader"/>.
        /// </summary>
        /// <param name="writer"><see cref="BinaryWriter"/> of destination stream.</param>
        /// <param name="header"><see cref="LocalFileHeader"/> to write.</param>
        private static void WriteLocalFileHeader(BinaryWriter writer, LocalFileHeader header)
        {
            writer.Write(header.Signature);
            writer.Write(header.VerExtract);
            writer.Write(header.BitFlag);
            writer.Write(header.Method);
            writer.Write(header.LastModificationTime);
            writer.Write(header.LastModificationDate);
            writer.Write(header.Crc32);
            writer.Write(header.CompressedLength);
            writer.Write(header.Length);
            writer.Write(header.FileNameLength);
            writer.Write(header.ExtraLength);
            writer.Write(header.FileName);
            writer.Write(header.ExtraField);
        }

        /// <summary>
        /// Read central directory file header from <see cref="BinaryReader"/>.
        /// </summary>
        /// <param name="reader"><see cref="BinaryReader"/> of zip data.</param>
        /// <returns>Central directory file header data.</returns>
        static CentralDirectoryFileHeader ReadCentralDirectoryFileHeader(BinaryReader reader)
        {
            var header = new CentralDirectoryFileHeader
            {
                Signature = (uint)SignatureType.CentralDirectoryFileHeader,
                VerMadeBy = reader.ReadUInt16(),
                VerExtract = reader.ReadUInt16(),
                BitFlag = reader.ReadUInt16(),
                Method = reader.ReadUInt16(),
                LastModificationTime = reader.ReadUInt16(),
                LastModificationDate = reader.ReadUInt16(),
                Crc32 = reader.ReadUInt32(),
                CompressedLength = reader.ReadUInt32(),
                Length = reader.ReadUInt32(),
                FileNameLength = reader.ReadUInt16(),
                ExtraLength = reader.ReadUInt16(),
                CommentLength = reader.ReadUInt16(),
                DiskNumber = reader.ReadUInt16(),
                InternalFileAttribute = reader.ReadUInt16(),
                ExternalFileAttribute = reader.ReadUInt32(),
                Offset = reader.ReadUInt32()
            };
            header.FileName = reader.ReadBytes(header.FileNameLength);
            header.ExtraField = reader.ReadBytes(header.ExtraLength);
            header.Comment = reader.ReadBytes(header.CommentLength);

            return header;
        }

        /// <summary>
        /// Write specified <see cref="CentralDirectoryFileHeader"/>.
        /// </summary>
        /// <param name="writer"><see cref="BinaryWriter"/> of destination stream.</param>
        /// <param name="header"><see cref="CentralDirectoryFileHeader"/> to write.</param>
        private static void WriteCentralDirectoryFileHeader(BinaryWriter writer, CentralDirectoryFileHeader header)
        {
            writer.Write(header.Signature);
            writer.Write(header.VerMadeBy);
            writer.Write(header.VerExtract);
            writer.Write(header.BitFlag);
            writer.Write(header.Method);
            writer.Write(header.LastModificationTime);
            writer.Write(header.LastModificationDate);
            writer.Write(header.Crc32);
            writer.Write(header.CompressedLength);
            writer.Write(header.Length);
            writer.Write(header.FileNameLength);
            writer.Write(header.ExtraLength);
            writer.Write(header.CommentLength);
            writer.Write(header.DiskNumber);
            writer.Write(header.InternalFileAttribute);
            writer.Write(header.ExternalFileAttribute);
            writer.Write(header.Offset);
            writer.Write(header.FileName);
            writer.Write(header.ExtraField);
            writer.Write(header.Comment);
        }

        /// <summary>
        /// Read central directory end record from <see cref="BinaryReader"/>.
        /// </summary>
        /// <param name="reader"><see cref="BinaryReader"/> of zip data.</param>
        /// <returns>Central directory end record data.</returns>
        static CentralDirectoryEndRecord ReadCentralDirectoryEndRecord(BinaryReader reader)
        {
            var header = new CentralDirectoryEndRecord
            {
                Signature = (uint)SignatureType.EndRecord,
                NumDisks = reader.ReadUInt16(),
                Disk = reader.ReadUInt16(),
                NumRecords = reader.ReadUInt16(),
                TotalRecords = reader.ReadUInt16(),
                CentralDirectorySize = reader.ReadUInt32(),
                Offset = reader.ReadUInt32(),
                CommentLength = reader.ReadUInt16()
            };
            header.Comment = reader.ReadBytes(header.CommentLength);

            return header;
        }

        /// <summary>
        /// Write specified <see cref="CentralDirectoryEndRecord"/>.
        /// </summary>
        /// <param name="writer"><see cref="BinaryWriter"/> of destination stream.</param>
        /// <param name="header"><see cref="CentralDirectoryEndRecord"/> to write.</param>
        private static void WriteCentralDirectoryEndRecord(BinaryWriter writer, CentralDirectoryEndRecord header)
        {
            writer.Write(header.Signature);
            writer.Write(header.NumDisks);
            writer.Write(header.Disk);
            writer.Write(header.NumRecords);
            writer.Write(header.TotalRecords);
            writer.Write(header.CentralDirectorySize);
            writer.Write(header.Offset);
            writer.Write(header.CommentLength);
            writer.Write(header.Comment);
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
        /// Native methods.
        /// </summary>
        [SuppressUnmanagedCodeSecurity]
        internal class UnsafeNativeMethods
        {
            /// <summary>
            /// Adds a directory to the search path used to locate DLLs for the application.
            /// </summary>
            /// <param name="path">Path to DLL directory.</param>
            /// <returns>True if success to set directory, otherwise false.</returns>
            [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
            [SuppressUnmanagedCodeSecurity]
            public static extern bool SetDllDirectory([In] string path);
        }
    }

    /// <summary>
    /// Signature enum.
    /// </summary>
    public enum SignatureType : uint
    {
        /// <summary>
        /// Signature for <see cref="LocalFileHeader"/>.
        /// </summary>
        LocalFileHeader = 0x04034b50,
        /// <summary>
        /// Signature for <see cref="CentralDirectoryFileHeader"/>
        /// </summary>
        CentralDirectoryFileHeader = 0x02014b50,
        /// <summary>
        /// Signature for <see cref="CentralDirectoryEndRecord"/>
        /// </summary>
        EndRecord = 0x06054b50
    }

    /// <summary>
    /// Comression result.
    /// </summary>
    public struct CompressionResult
    {
        /// <summary>
        /// Compressed size.
        /// </summary>
        public uint CompressedLength;
        /// <summary>
        /// Uncompressed size.
        /// </summary>
        public uint Length;
        /// <summary>
        /// Relative offset of local file header.
        /// </summary>
        public uint Offset;
    }

    /// <summary>
    /// Common part of header, <see cref="LocalFileHeader"/>, <see cref="CentralDirectoryFileHeader"/>
    /// or <see cref="CentralDirectoryEndRecord"/>.
    /// </summary>
    public class Header
    {
        /// <summary>
        /// File header signature.
        /// <list type="table">
        ///   <listheader>
        ///     <term><see cref="LocalFileHeader"/></term>
        ///     <description>0x04034b50 (<see cref="SignatureType.LocalFileHeader"/>)</description>
        ///   </listheader>
        ///   <item>
        ///     <term><see cref="CentralDirectoryFileHeader"/></term>
        ///     <description>0x02014b50 (<see cref="SignatureType.CentralDirectoryFileHeader"/>)</description>
        ///   </item>
        ///   <item>
        ///     <term><see cref="CentralDirectoryEndRecord"/></term>
        ///     <description>0x06054b50 (<see cref="SignatureType.EndRecord"/>)</description>
        ///   </item>
        /// </list>
        /// </summary>
        public uint Signature;
    };

    /// <summary>
    /// <para>Header for files.</para>
    /// <para>All multi-byte values in the header are stored in little-endian byte order.</para>
    /// <para>All length fields count the length in bytes.</para>
    /// </summary>
    public class LocalFileHeader : Header
    {
        /// <summary>
        /// Version needed to extract (minimum).
        /// </summary>
        public ushort VerExtract { get; set; }
        /// <summary>
        /// General purpose bit flag.
        /// </summary>
        public ushort BitFlag { get; set; }
        /// <summary>
        /// Compression method; e.g. none = 0, DEFLATE = 8 (or "\0x08\0x00")
        /// </summary>
        public ushort Method { get; set; }
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
        ///	File name.
        /// </summary>
        public byte[] FileName { get; set; }
        /// <summary>
        ///	Extra field.
        /// </summary>
        public byte[] ExtraField { get; set; }
    };

    /// <summary>
    /// Central directory file header.
    /// </summary>
    public class CentralDirectoryFileHeader : Header
    {
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
        public ushort BitFlag { get; set; }
        /// <summary>
        /// Compression method.
        /// </summary>
        public ushort Method { get; set; }
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
        ///	Uncompressed size (or 0xffffffff for ZIP64).
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
        ///	File name.
        /// </summary>
        public byte[] FileName { get; set; }
        /// <summary>
        ///	Extra field.
        /// </summary>
        public byte[] ExtraField { get; set; }
        /// <summary>
        /// Comment.
        /// </summary>
        public byte[] Comment { get; set; }
    };

    /// <summary>
    /// <para>End of central directory record (EOCD)</para>
    /// <para>After all the central directory entries comes the end of central directory (EOCD) record,
    /// which marks the end of the ZIP file.</para>
    /// </summary>
    public class CentralDirectoryEndRecord : Header
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
    };
}
