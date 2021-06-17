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
                            offset = offset,
                            comp_size = result.CompressedLength,
                            uncomp_size = result.Length,
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
                header.comp_size = cr.comp_size;
                header.uncomp_size = cr.uncomp_size;
                header.offset = cr.offset;
                WriteCentralDirectoryFileHeader(writer, header);

                signature = ReadSignature(reader);
            }

            if (signature == (uint)SignatureType.EndRecord)
            {
                var header = ReadCentralDirectoryEndRecord(reader);
                header.offset = (uint)central_dir_offset;
                WriteCentralDirectoryEndRecord(writer, header);
            }
        }

        private static async Task<(uint Length, uint CompressedLength)> RecompressEntryAsync(BinaryReader reader, BinaryWriter writer, TaskFactory taskFactory, int procIndex)
        {
            var header = ReadLocalFileHeader(reader);
            header.signature = (uint)SignatureType.LocalFileHeader;
            var src = reader.ReadBytes((int)header.comp_size);

            // Is not deflate
            if (header.method != 8)
            {
                lock (writer)
                {
                    WriteLocalFileHeader(writer, header);
                    writer.Write(src);
                }
                return (header.uncomp_size, header.comp_size);
            }

            using (var decompressedMs = new MemoryStream((int)header.uncomp_size))
            {
                using (var compressedMs = new MemoryStream(src))
                using (var dds = new DeflateStream(compressedMs, CompressionMode.Decompress))
                {
                    dds.CopyTo(decompressedMs);
                }

                var sw = Stopwatch.StartNew();

                var entryName = Encoding.ASCII.GetString(header.ext, 0, header.filename_len);

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
                        header.comp_size = (uint)recompressedData.Length;
                        WriteLocalFileHeader(writer, header);
                        writer.Write(recompressedData);
                    }
                    else
                    {
                        WriteLocalFileHeader(writer, header);
                        writer.Write(src);
                    }
                }

                return (header.uncomp_size, header.comp_size);
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
                signature = (uint)SignatureType.LocalFileHeader,
                ver_extract = reader.ReadUInt16(),
                bit_flag = reader.ReadUInt16(),
                method = reader.ReadUInt16(),
                not_used1 = reader.ReadUInt32(),
                crc32 = reader.ReadUInt32(),
                comp_size = reader.ReadUInt32(),
                uncomp_size = reader.ReadUInt32(),
                filename_len = reader.ReadUInt16(),
                extra_len = reader.ReadUInt16()
            };
            header.ext = reader.ReadBytes(header.filename_len + header.extra_len);

            return header;
        }

        private static void WriteLocalFileHeader(BinaryWriter writer, LocalFileHeader header)
        {
            writer.Write(header.signature);
            writer.Write(header.ver_extract);
            writer.Write(header.bit_flag);
            writer.Write(header.method);
            writer.Write(header.not_used1);
            writer.Write(header.crc32);
            writer.Write(header.comp_size);
            writer.Write(header.uncomp_size);
            writer.Write(header.filename_len);
            writer.Write(header.extra_len);
            writer.Write(header.ext);
        }

        static CentralDirectoryFileHeader ReadCentralDirectoryFileHeader(BinaryReader reader)
        {
            var header = new CentralDirectoryFileHeader
            {
                signature = (uint)SignatureType.CentralDirectoryFileHeader,
                ver_made_by = reader.ReadUInt16(),
                ver_extract = reader.ReadUInt16(),
                bit_flag = reader.ReadUInt16(),
                method = reader.ReadUInt16(),
                not_used1 = reader.ReadUInt32(),
                crc32 = reader.ReadUInt32(),
                comp_size = reader.ReadUInt32(),
                uncomp_size = reader.ReadUInt32(),
                filename_len = reader.ReadUInt16(),
                extra_len = reader.ReadUInt16(),
                comment_len = reader.ReadUInt16(),
                not_used2 = reader.ReadUInt64(),
                offset = reader.ReadUInt32()
            };
            header.ext = reader.ReadBytes(header.filename_len + header.extra_len + header.comment_len);

            return header;
        }

        private static void WriteCentralDirectoryFileHeader(BinaryWriter writer, CentralDirectoryFileHeader header)
        {
            writer.Write(header.signature);
            writer.Write(header.ver_made_by);
            writer.Write(header.ver_extract);
            writer.Write(header.bit_flag);
            writer.Write(header.method);
            writer.Write(header.not_used1);
            writer.Write(header.crc32);
            writer.Write(header.comp_size);
            writer.Write(header.uncomp_size);
            writer.Write(header.filename_len);
            writer.Write(header.extra_len);
            writer.Write(header.comment_len);
            writer.Write(header.not_used2);
            writer.Write(header.offset);
            writer.Write(header.ext);
        }

        static CentralDirectoryEndRecord ReadCentralDirectoryEndRecord(BinaryReader reader)
        {
            var header = new CentralDirectoryEndRecord
            {
                signature = (uint)SignatureType.EndRecord,
                num_disks = reader.ReadUInt16(),
                disk = reader.ReadUInt16(),
                num_records = reader.ReadUInt16(),
                total_records = reader.ReadUInt16(),
                central_dir_size = reader.ReadUInt32(),
                offset = reader.ReadUInt32(),
                comment_len = reader.ReadUInt16()
            };
            header.ext = reader.ReadBytes(header.comment_len);

            return header;
        }

        private static void WriteCentralDirectoryEndRecord(BinaryWriter writer, CentralDirectoryEndRecord header)
        {
            writer.Write(header.signature);
            writer.Write(header.num_disks);
            writer.Write(header.disk);
            writer.Write(header.num_records);
            writer.Write(header.total_records);
            writer.Write(header.central_dir_size);
            writer.Write(header.offset);
            writer.Write(header.comment_len);
            writer.Write(header.ext);
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
        public uint comp_size;
        public uint uncomp_size;
        public uint offset;
    }

    public class Header
    {
        public uint signature;
    };

    public class LocalFileHeader : Header
    {
        public ushort ver_extract;
        public ushort bit_flag;
        public ushort method;
        public uint not_used1;
        public uint crc32;
        public uint comp_size;
        public uint uncomp_size;
        public ushort filename_len;
        public ushort extra_len;
        public byte[] ext;
    };

    public class CentralDirectoryFileHeader : Header
    {
        public ushort ver_made_by;
        public ushort ver_extract;
        public ushort bit_flag;
        public ushort method;
        public uint not_used1;
        public uint crc32;
        public uint comp_size;
        public uint uncomp_size;
        public ushort filename_len;
        public ushort extra_len;
        public ushort comment_len;
        public ulong not_used2;
        public uint offset;
        public byte[] ext;
    };

    public class CentralDirectoryEndRecord : Header
    {
        public ushort num_disks;
        public ushort disk;
        public ushort num_records;
        public ushort total_records;
        public uint central_dir_size;
        public uint offset;
        public ushort comment_len;
        public byte[] ext;
    };
}
