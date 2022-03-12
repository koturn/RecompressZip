using System;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;


namespace RecompressZip.Zip
{
    /// <summary>
    /// CRC-32 calculation class.
    /// </summary>
    public class Crc32Calculator
    {
        /// <summary>
        /// Cache of CRC-32 table.
        /// </summary>
        private static uint[]? _table;

        /// <summary>
        /// Compute CRC-32 value.
        /// </summary>
        /// <param name="buf"><see cref="byte"/> data array.</param>
        /// <returns>CRC-32 value.</returns>
        public static uint Compute(byte[] buf)
        {
            return Compute(buf.AsSpan());
        }

        /// <summary>
        /// Compute CRC-32 value.
        /// </summary>
        /// <param name="buf"><see cref="byte"/> data array.</param>
        /// <param name="offset">Offset of <paramref name="buf"/>.</param>
        /// <param name="count">Data count of <paramref name="buf"/>.</param>
        /// <returns>CRC-32 value.</returns>
        public static uint Compute(byte[] buf, int offset, int count)
        {
            return Compute(buf.AsSpan(offset, count));
        }

        /// <summary>
        /// Compute CRC-32 value.
        /// </summary>
        /// <param name="buf"><see cref="Span{T}"/> of <see cref="byte"/> data.</param>
        /// <returns>CRC-32 value.</returns>
        public static uint Compute(ReadOnlySpan<byte> buf)
        {
            return Finalize(Update(buf));
        }


        /// <summary>
        /// <para>Update intermidiate CRC-32 value.</para>
        /// <para>Use default value of <paramref name="crc"/> at first time.</para>
        /// </summary>
        /// <param name="buf"><see cref="byte"/> data array.</param>
        /// <param name="crc">Intermidiate CRC-32 value.</param>
        /// <returns>Updated intermidiate CRC-32 value.</returns>
        public static uint Update(byte[] buf, uint crc = 0xffffffff)
        {
            return Update(buf.AsSpan(), crc);
        }

        /// <summary>
        /// <para>Update intermidiate CRC-32 value.</para>
        /// <para>Use default value of <paramref name="crc"/> at first time.</para>
        /// </summary>
        /// <param name="buf"><see cref="byte"/> data array.</param>
        /// <param name="offset">Offset of <paramref name="buf"/>.</param>
        /// <param name="count">Data count of <paramref name="buf"/>.</param>
        /// <param name="crc">Intermidiate CRC-32 value.</param>
        /// <returns>Updated intermidiate CRC-32 value.</returns>
        public static uint Update(byte[] buf, int offset, int count, uint crc = 0xffffffff)
        {
            return Update(buf.AsSpan(offset, count), crc);
        }

        /// <summary>
        /// <para>Update intermidiate CRC-32 value.</para>
        /// <para>Use default value of <paramref name="crc"/> at first time.</para>
        /// </summary>
        /// <param name="buf"><see cref="Span{T}"/> of <see cref="byte"/> data.</param>
        /// <param name="crc">Intermidiate CRC-32 value.</param>
        /// <returns>Updated intermidiate CRC-32 value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint Update(ReadOnlySpan<byte> buf, uint crc = 0xffffffff)
        {
            // This method call will be repplaced by calling UpdateSse41() or UpdateNaive() at JIT compiling time.
            return Sse41.IsSupported && Pclmulqdq.IsSupported ? UpdateSse41(buf, crc) : UpdateNaive(buf, crc);
        }

        /// <summary>
        /// <para>Update intermidiate CRC-32 value.</para>
        /// <para>Use default value of <paramref name="crc"/> at first time.</para>
        /// </summary>
        /// <param name="x">A value of <see cref="byte"/>.</param>
        /// <param name="crc">Intermidiate CRC-32 value.</param>
        /// <returns>Updated intermidiate CRC-32 value.</returns>
        public static uint Update(byte x, uint crc = 0xffffffff)
        {
            return GetTable()[(crc ^ x) & 0xff] ^ (crc >> 8);
        }

        /// <summary>
        /// Calculate CRC-32 value from intermidiate CRC-32 value.
        /// </summary>
        /// <param name="crc">Intermidiate CRC-32 value</param>
        /// <returns>CRC-32 value.</returns>
        [Pure]
        public static uint Finalize(uint crc)
        {
            return ~crc;
        }


        /// <summary>
        /// <para>Update intermidiate CRC-32 value.</para>
        /// <para>Use default value of <paramref name="crc"/> at first time.</para>
        /// <para>This method is implemented without SIMD instructions.</para>
        /// </summary>
        /// <param name="buf"><see cref="Span{T}"/> of <see cref="byte"/> data.</param>
        /// <param name="crc">Intermidiate CRC-32 value.</param>
        /// <returns>Updated intermidiate CRC-32 value.</returns>
        public static uint UpdateNaive(ReadOnlySpan<byte> buf, uint crc = 0xffffffff)
        {
            var crcTable = GetTable();

            var c = crc;
            foreach (var x in buf)
            {
                c = crcTable[(c ^ x) & 0xff] ^ (c >> 8);
            }

            return c;
        }

        /// <summary>
        /// <para>Update intermidiate CRC-32 value.</para>
        /// <para>Use default value of <paramref name="crc"/> at first time.</para>
        /// <para>This method is implemented with SSE4.1 and PCLMULQDQ.</para>
        /// </summary>
        /// <param name="buf"><see cref="Span{T}"/> of <see cref="byte"/> data.</param>
        /// <param name="crc">Intermidiate CRC-32 value.</param>
        /// <returns>Updated intermidiate CRC-32 value.</returns>
        /// <remarks><seealso href="https://www.intel.com/content/dam/www/public/us/en/documents/white-papers/fast-crc-computation-generic-polynomials-pclmulqdq-paper.pdf"/></remarks>
        /// <remarks><seealso href="https://chromium.googlesource.com/chromium/src/+/master/third_party/zlib/crc32_simd.c"/></remarks>
        private static uint UpdateSse41(ReadOnlySpan<byte> buf, uint crc32 = 0xffffffff)
        {
            var len = buf.Length;
            if (len < 64)
            {
                return UpdateNaive(buf, crc32);
            }

            unsafe
            {
                fixed (byte* pp = buf)
                {
                    var p = pp;
                    var x1 = Sse2.Xor(
                        Sse2.LoadVector128(p).AsUInt32(),
                        Sse2.ConvertScalarToVector128UInt32(crc32));
                    var x2 = Sse2.LoadVector128(p + 16).AsUInt32();
                    var x3 = Sse2.LoadVector128(p + 32).AsUInt32();
                    var x4 = Sse2.LoadVector128(p + 48).AsUInt32();

                    p += 64;
                    len -= 64;

                    // Parallel fold blocks of 64, if any.
                    var k1k2 = Vector128.Create(0x0000000154442bd4U, 0x00000001c6e41596U);
                    for (;  len >= 64; len -= 64)
                    {
                        x1 = Sse2.Xor(
                            Sse2.LoadVector128(p).AsUInt32(),
                            Sse2.Xor(
                                Pclmulqdq.CarrylessMultiply(x1.AsUInt64(), k1k2, 0x11),
                                Pclmulqdq.CarrylessMultiply(x1.AsUInt64(), k1k2, 0x00)).AsUInt32());
                        x2 = Sse2.Xor(
                            Sse2.LoadVector128(p + 16).AsUInt32(),
                            Sse2.Xor(
                                Pclmulqdq.CarrylessMultiply(x2.AsUInt64(), k1k2, 0x11),
                                Pclmulqdq.CarrylessMultiply(x2.AsUInt64(), k1k2, 0x00)).AsUInt32());
                        x3 = Sse2.Xor(
                            Sse2.LoadVector128(p + 32).AsUInt32(),
                            Sse2.Xor(
                                Pclmulqdq.CarrylessMultiply(x3.AsUInt64(), k1k2, 0x11),
                                Pclmulqdq.CarrylessMultiply(x3.AsUInt64(), k1k2, 0x00)).AsUInt32());
                        x4 = Sse2.Xor(
                            Sse2.LoadVector128(p + 48).AsUInt32(),
                            Sse2.Xor(
                                Pclmulqdq.CarrylessMultiply(x4.AsUInt64(), k1k2, 0x11),
                                Pclmulqdq.CarrylessMultiply(x4.AsUInt64(), k1k2, 0x00)).AsUInt32());
                        p += 64;
                    }

                    // Fold into 128-bits.
                    var k3k4 = Vector128.Create(0x00000001751997d0U, 0x00000000ccaa009eU);
                    x1 = Sse2.Xor(
                        Pclmulqdq.CarrylessMultiply(x1.AsUInt64(), k3k4, 0x00).AsUInt32(),
                        Sse2.Xor(
                            x2,
                            Pclmulqdq.CarrylessMultiply(x1.AsUInt64(), k3k4, 0x11).AsUInt32()));
                    x1 = Sse2.Xor(
                        Pclmulqdq.CarrylessMultiply(x1.AsUInt64(), k3k4, 0x00).AsUInt32(),
                        Sse2.Xor(
                            x3,
                            Pclmulqdq.CarrylessMultiply(x1.AsUInt64(), k3k4, 0x11).AsUInt32()));
                    x1 = Sse2.Xor(
                        Pclmulqdq.CarrylessMultiply(x1.AsUInt64(), k3k4, 0x00).AsUInt32(),
                        Sse2.Xor(
                            x4,
                            Pclmulqdq.CarrylessMultiply(x1.AsUInt64(), k3k4, 0x11).AsUInt32()));

                    // Single fold blocks of 16, if any.
                    for (; len >= 16; len -= 16)
                    {
                        x1 = Sse2.Xor(
                            Pclmulqdq.CarrylessMultiply(x1.AsUInt64(), k3k4, 0x00).AsUInt32(),
                            Sse2.Xor(
                                Sse2.LoadVector128((uint*)p),
                                Pclmulqdq.CarrylessMultiply(x1.AsUInt64(), k3k4, 0x11).AsUInt32()));
                        p += 16;
                    }

                    // Fold 128-bits to 64-bits.
                    var bwaFactor = Vector128.Create(0xffffffffu, 0x00000000u, 0xffffffffu, 0x00000000u);
                    x1 = Sse2.Xor(
                        Sse2.ShiftRightLogical128BitLane(x1, 8),
                        Pclmulqdq.CarrylessMultiply(x1.AsUInt64(), k3k4, 0x10).AsUInt32());
                    x1 = Sse2.Xor(
                        Sse2.ShiftRightLogical128BitLane(x1, 4),
                        Pclmulqdq.CarrylessMultiply(
                            Sse2.And(x1, bwaFactor).AsUInt64(),
                            Vector128.CreateScalar(0x0000000163cd6124U),
                            0x00).AsUInt32());

                    // Barret reduce to 32-bits.
                    var poly = Vector128.Create(0x00000001db710641U, 0x0000001f7011641U);
                    crc32 = Sse41.Extract(
                        Sse2.Xor(
                            x1,
                            Pclmulqdq.CarrylessMultiply(
                                poly,
                                Sse2.And(
                                    bwaFactor,
                                    Pclmulqdq.CarrylessMultiply(
                                        poly,
                                        Sse2.And(
                                            x1,
                                            bwaFactor).AsUInt64(),
                                        0x01).AsUInt32()).AsUInt64(),
                                0x00).AsUInt32()),
                        1);
                }
            }

            return len == 0 ? crc32 : UpdateNaive(buf[^len..], crc32);
        }


        /// <summary>
        /// <para>Get CRC-32 table cache.</para>
        /// <para>If the cache is not generated, generate and return it.</para>
        /// </summary>
        /// <returns>CRC-32 table</returns>
        private static uint[] GetTable()
        {
            return _table ??= GenerateTable();
        }

        /// <summary>
        /// Generate CRC-32 value.
        /// This method only used in <see cref="GenerateTable"/>.
        /// </summary>
        /// <returns>CRC-32 table.</returns>
        private static uint[] GenerateTable()
        {
            var crcTable = new uint[256];

            for (int n = 0; n < crcTable.Length; n++)
            {
                var c = (uint)n;
                for (var k = 0; k < 8; k++)
                {
                    c = (c & 1) != 0 ? (0xedb88320 ^ (c >> 1)) : (c >> 1);
                }
                crcTable[n] = c;
            }

            return crcTable;
        }
    }
}
