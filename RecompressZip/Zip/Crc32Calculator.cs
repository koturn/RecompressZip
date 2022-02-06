using System;


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
        public static uint Update(ReadOnlySpan<byte> buf, uint crc = 0xffffffff)
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
        public static uint Finalize(uint crc)
        {
            return crc ^ 0xffffffff;
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
