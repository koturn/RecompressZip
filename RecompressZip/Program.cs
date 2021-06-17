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
    static class Program
    {
        /// <summary>
        /// Logging instance.
        /// </summary>
        private static readonly Logger _logger;

        static Program()
        {
            UnsafeNativeMethods.SetDllDirectory(
                Path.Combine(
                    AppContext.BaseDirectory,
                    Environment.Is64BitProcess ? "x64" : "x86"));
            _logger = LogManager.GetCurrentClassLogger();
        }


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

        static void RecompressZip(string srcFilePath, string dstFilePath)
        {
            using var ifs = new FileStream(srcFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var ofs = new FileStream(dstFilePath, FileMode.Create, FileAccess.Write, FileShare.Read);
            RecompressZip(ifs, ofs);
        }

        static void RecompressZip(Stream srcStream, Stream dstStream)
        {
            using var reader = new BinaryReader(srcStream, Encoding.Default, true);
            using var writer = new BinaryWriter(dstStream, Encoding.Default, true);
            RecompressZip(reader, writer);
        }

        static void RecompressZip(BinaryReader reader, BinaryWriter writer)
        {
            var signature = ReadSignature(reader);

            var taskFactory = new TaskFactory(new LimitedConcurrencyLevelTaskScheduler(Environment.ProcessorCount));
            var taskList = new List<Task<CompressionResult>>();
            while (signature == (uint)SignatureType.LocalFileHeader)
            {
                var offset = (uint)writer.BaseStream.Position;
                taskList.Add(RecompressEntryAsync(reader, writer, taskFactory, taskList.Count)
                    .ContinueWith(task =>
                    {
                        var result = task.Result;
                        return new CompressionResult
                        {
                            Offset = offset,
                            CompressedLength = result.CompressedLength,
                            Length = result.Length,
                        };
                    }));
                signature = ReadSignature(reader);
            }

            Task.WaitAll(taskList.ToArray());

            var resultList = taskList.Select(task => task.Result).ToList();

            int listCnt = 0;
            var central_dir_offset = writer.BaseStream.Position;
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
                header.Offset = (uint)central_dir_offset;
                WriteCentralDirectoryEndRecord(writer, header);
            }
        }

        private static async Task<(uint Length, uint CompressedLength)> RecompressEntryAsync(BinaryReader reader, BinaryWriter writer, TaskFactory taskFactory, int procIndex)
        {
            var header = ReadLocalFileHeader(reader);
            header.Signature = (uint)SignatureType.LocalFileHeader;
            var src = reader.ReadBytes((int)header.CompressedLength);

            // Is not deflate
            if (header.Method != 8)
            {
                lock (writer)
                {
                    WriteLocalFileHeader(writer, header);
                    writer.Write(src);
                }
                return (header.Length, header.CompressedLength);
            }

            using (var decompressedMs = new MemoryStream((int)header.Length))
            {
                using (var compressedMs = new MemoryStream(src))
                using (var dds = new DeflateStream(compressedMs, CompressionMode.Decompress))
                {
                    dds.CopyTo(decompressedMs);
                }

                var sw = Stopwatch.StartNew();

                var entryName = Encoding.ASCII.GetString(header.ExtraData, 0, header.FileNameLength);

                // Take a long long time ...
                var recompressedData = await taskFactory.StartNew(() =>
                {
                    _logger.Info("[{0}] Compress {1} ...", procIndex, entryName);
                    return Zopfli.Compress(
                        decompressedMs.GetBuffer(),
                        0,
                        (int)decompressedMs.Length,
                        ZopfliFormat.Deflate);
                });

                _logger.Log(
                    recompressedData.Length < src.Length ? LogLevel.Info : LogLevel.Warn, 
                    "[{0}] Compress {1} done: {2:F3} seconds, {3:F3} KiB -> {4:F3} KiB (deflated {5:F2}%)",
                    procIndex,
                    entryName,
                    sw.ElapsedMilliseconds / 1000.0,
                    ToKiB(src.Length),
                    ToKiB(recompressedData.Length),
                    CalcDeflatedRate(src.Length, recompressedData.Length) * 100.0);

                lock (writer)
                {
                    if (recompressedData.Length < src.Length)
                    {
                        header.CompressedLength = (uint)recompressedData.Length;
                        WriteLocalFileHeader(writer, header);
                        writer.Write(recompressedData);
                    }
                    else
                    {
                        WriteLocalFileHeader(writer, header);
                        writer.Write(src);
                    }
                }

                return (header.Length, header.CompressedLength);
            }
        }

        static uint ReadSignature(BinaryReader reader)
        {
            return reader.ReadUInt32();
        }

        static LocalFileHeader ReadLocalFileHeader(BinaryReader reader)
        {
            var header = new LocalFileHeader
            {
                Signature = (uint)SignatureType.LocalFileHeader,
                VerExtract = reader.ReadUInt16(),
                BitFlag = reader.ReadUInt16(),
                Method = reader.ReadUInt16(),
                Reserved = reader.ReadUInt32(),
                Crc32 = reader.ReadUInt32(),
                CompressedLength = reader.ReadUInt32(),
                Length = reader.ReadUInt32(),
                FileNameLength = reader.ReadUInt16(),
                ExtraLength = reader.ReadUInt16()
            };
            header.ExtraData = reader.ReadBytes(header.FileNameLength + header.ExtraLength);

            return header;
        }

        private static void WriteLocalFileHeader(BinaryWriter writer, LocalFileHeader header)
        {
            writer.Write(header.Signature);
            writer.Write(header.VerExtract);
            writer.Write(header.BitFlag);
            writer.Write(header.Method);
            writer.Write(header.Reserved);
            writer.Write(header.Crc32);
            writer.Write(header.CompressedLength);
            writer.Write(header.Length);
            writer.Write(header.FileNameLength);
            writer.Write(header.ExtraLength);
            writer.Write(header.ExtraData);
        }

        static CentralDirectoryFileHeader ReadCentralDirectoryFileHeader(BinaryReader reader)
        {
            var header = new CentralDirectoryFileHeader
            {
                Signature = (uint)SignatureType.CentralDirectoryFileHeader,
                VerMadeBy = reader.ReadUInt16(),
                VerExtract = reader.ReadUInt16(),
                BitFlag = reader.ReadUInt16(),
                Method = reader.ReadUInt16(),
                Reserved1 = reader.ReadUInt32(),
                Crc32 = reader.ReadUInt32(),
                CompressedLength = reader.ReadUInt32(),
                Length = reader.ReadUInt32(),
                FileNameLength = reader.ReadUInt16(),
                ExtraLength = reader.ReadUInt16(),
                CommentLength = reader.ReadUInt16(),
                Reserved2 = reader.ReadUInt64(),
                Offset = reader.ReadUInt32()
            };
            header.ExtraData = reader.ReadBytes(header.FileNameLength + header.ExtraLength + header.CommentLength);

            return header;
        }

        private static void WriteCentralDirectoryFileHeader(BinaryWriter writer, CentralDirectoryFileHeader header)
        {
            writer.Write(header.Signature);
            writer.Write(header.VerMadeBy);
            writer.Write(header.VerExtract);
            writer.Write(header.BitFlag);
            writer.Write(header.Method);
            writer.Write(header.Reserved1);
            writer.Write(header.Crc32);
            writer.Write(header.CompressedLength);
            writer.Write(header.Length);
            writer.Write(header.FileNameLength);
            writer.Write(header.ExtraLength);
            writer.Write(header.CommentLength);
            writer.Write(header.Reserved2);
            writer.Write(header.Offset);
            writer.Write(header.ExtraData);
        }

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
            header.ExtraData = reader.ReadBytes(header.CommentLength);

            return header;
        }

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
            writer.Write(header.ExtraData);
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

    public enum SignatureType : uint
    {
        LocalFileHeader = 0x04034b50,
        CentralDirectoryFileHeader = 0x02014b50,
        EndRecord = 0x06054b50
    }


    public struct CompressionResult
    {
        public uint CompressedLength;
        public uint Length;
        public uint Offset;
    }

    public class Header
    {
        public uint Signature;
    };

    public class LocalFileHeader : Header
    {
        public ushort VerExtract { get; set; }
        public ushort BitFlag { get; set; }
        public ushort Method { get; set; }
        public uint Reserved { get; set; }
        public uint Crc32 { get; set; }
        public uint CompressedLength { get; set; }
        public uint Length { get; set; }
        public ushort FileNameLength { get; set; }
        public ushort ExtraLength { get; set; }
        public byte[] ExtraData { get; set; }
    };

    public class CentralDirectoryFileHeader : Header
    {
        public ushort VerMadeBy { get; set; }
        public ushort VerExtract { get; set; }
        public ushort BitFlag { get; set; }
        public ushort Method { get; set; }
        public uint Reserved1 { get; set; }
        public uint Crc32 { get; set; }
        public uint CompressedLength { get; set; }
        public uint Length { get; set; }
        public ushort FileNameLength { get; set; }
        public ushort ExtraLength { get; set; }
        public ushort CommentLength { get; set; }
        public ulong Reserved2 { get; set; }
        public uint Offset { get; set; }
        public byte[] ExtraData { get; set; }
    };

    public class CentralDirectoryEndRecord : Header
    {
        public ushort NumDisks { get; set; }
        public ushort Disk { get; set; }
        public ushort NumRecords { get; set; }
        public ushort TotalRecords { get; set; }
        public uint CentralDirectorySize { get; set; }
        public uint Offset { get; set; }
        public ushort CommentLength { get; set; }
        public byte[] ExtraData { get; set; }
    };
}
