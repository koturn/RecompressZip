using System;
using System.Diagnostics.Contracts;
using System.Text;
using System.Runtime.CompilerServices;


namespace RecompressZip.Zip
{
    /// <summary>
    /// Base class of <see cref="ZipEncryptor"/> and <see cref="ZipDecryptor"/>.
    /// </summary>
    public abstract class ZipCryptor
    {
        /// <summary>
        /// Size of <see cref="CryptHeader"/>.
        /// </summary>
        public const int CryptHeaderSize = 12;
        /// <summary>
        /// Initial ZipCrypt key.
        /// </summary>
        private static readonly uint[] InitialKey;

        /// <summary>
        /// Initialize <see cref="InitialKey"/>.
        /// </summary>
        static ZipCryptor()
        {
            InitialKey = new uint[] {305419896, 591751049, 878082192};
        }

        /// <summary>
        /// Clone and return <see cref="InitialKey"/>.
        /// </summary>
        private static uint[] CreateInitialKey()
        {
            return (uint[])InitialKey.Clone();
        }


        /// <summary>
        /// Crypt header.
        /// </summary>
        public byte[] CryptHeader { get; }

        /// <summary>
        /// ZipCrypt key.
        /// </summary>
        private uint[] _key;


        /// <summary>
        /// Allocate memory for <see cref="CryptHeader"/> and <see cref="_key"/>, and initialize <see cref="_key"/>.
        /// </summary>
        public ZipCryptor()
        {
            CryptHeader = new byte[CryptHeaderSize];
            _key = CreateInitialKey();
        }


        /// <summary>
        /// Initialize <see cref="_key"/> with <paramref name="password"/>.
        /// </summary>
        /// <param name="password">Zip password.</param>
        /// <param name="enc">Encoding of <paramref name="password"/>.</param>
        protected void InitializeKeysWithPassword(string password, Encoding enc)
        {
            InitializeKeysWithPassword(enc.GetBytes(password));
        }

        /// <summary>
        /// Initialize <see cref="_key"/> with <paramref name="password"/>.
        /// </summary>
        /// <param name="passwordBytes">Byte sequence of password of zip archive.</param>
        protected void InitializeKeysWithPassword(ReadOnlySpan<byte> passwordBytes)
        {
            foreach (var b in passwordBytes)
            {
                UpdateKeys(b);
            }
        }

        /// <summary>
        /// Update <see cref="_key"/> with specified one byte.
        /// </summary>
        /// <param name="b">A byte data.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected void UpdateKeys(byte b)
        {
            var key = _key;
            key[0] = Crc32Calculator.Update(b, key[0]);
            key[1] = (key[1] + (key[0] & 0xff)) * 134775813 + 1;
            key[2] = Crc32Calculator.Update((byte)(key[1] >> 24), key[2]);
        }

        /// <summary>
        /// Get one byte for xor operation from <see cref="_key"/>.
        /// </summary>
        [Pure]
        protected byte GetDecryptByte()
        {
            var k = (ushort)(_key[2] | 0x0002);
            return (byte)(((k * (k ^ 0x0001)) >> 8) & 0xff);
        }
    }
}
