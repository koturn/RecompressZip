using System;
using System.Diagnostics.Contracts;
using System.Text;


namespace RecompressZip.Zip
{
    /// <summary>
    /// Decryptor of ZipCrypto.
    /// </summary>
    public sealed class ZipDecryptor : ZipCryptor
    {
        /// <summary>
        /// Initialize crypt key with <paramref name="password"/> and <paramref name="cryptHeader"/>.
        /// </summary>
        /// <param name="password">Password of zip archive.</param>
        /// <param name="enc">Encoding of <paramref name="password"/>.</param>
        /// <param name="cryptHeader">Crypt header.</param>
        public ZipDecryptor(string password, Encoding enc, ReadOnlySpan<byte> cryptHeader)
        {
            InitializeKeysWithPassword(password, enc);
            InitializeCryptHeader(cryptHeader);
        }

        /// <summary>
        /// Initialize crypt key with <paramref name="passwordBytes"/> and <paramref name="cryptHeader"/>.
        /// </summary>
        /// <param name="passwordBytes">Byte sequence of password of zip archive.</param>
        /// <param name="cryptHeader">Crypt header.</param>
        public ZipDecryptor(ReadOnlySpan<byte> passwordBytes, ReadOnlySpan<byte> cryptHeader)
        {
            InitializeKeysWithPassword(passwordBytes);
            InitializeCryptHeader(cryptHeader);
        }

        /// <summary>
        /// Decrypt data.
        /// </summary>
        /// <param name="data">Encrypted data.</param>
        /// <returns>Decrypted data.</returns>
        public byte[] Decrypt(ReadOnlySpan<byte> data)
        {
            var dstData = new byte[data.Length];
            Decrypt(data, dstData);
            return dstData;
        }

        /// <summary>
        /// Decrypt data.
        /// </summary>
        /// <param name="srcData">Encrypted data.</param>
        /// <param name="dstData">Destination of decrypted data.</param>
        public void Decrypt(ReadOnlySpan<byte> srcData, Span<byte> dstData)
        {
            for (int i = 0; i < srcData.Length; i++) {
                dstData[i] = Decrypt(srcData[i]);
            }
        }

        /// <summary>
        /// Decrypt single byte.
        /// </summary>
        /// <param name="b">A target byte.</param>
        /// <returns>A decrypted byte.</returns>
        public byte Decrypt(byte b)
        {
            b ^= GetDecryptByte();
            UpdateKeys(b);
            return b;
        }


        /// <summary>
        /// Copy crypt header and update ZipCrypto keys.
        /// </summary>
        /// <param name="cryptHeader">Crypt header to copy.</param>
        private void InitializeCryptHeader(ReadOnlySpan<byte> cryptHeader)
        {
            var ch = CryptHeader;
            for (int i = 0; i < ch.Length; i++)
            {
                ch[i] = cryptHeader[i];
                Decrypt(ch[i]);
            }
        }


        /// <summary>
        /// Decrypt data with <paramref name="password"/> and <paramref name="cryptHeader"/>.
        /// </summary>
        /// <param name="data">Encrypted compressed zip entry data.</param>
        /// <param name="password">Password of zip archive.</param>
        /// <param name="enc">Encoding of <paramref name="password"/>.</param>
        /// <param name="cryptHeader">Crypt header.</param>
        /// <returns>Decrypted data.</returns>
        [Pure]
        public static byte[] DecryptData(ReadOnlySpan<byte> data, string password, Encoding enc, ReadOnlySpan<byte> cryptHeader)
        {
            return new ZipDecryptor(password, enc, cryptHeader).Decrypt(data);
        }

        /// <summary>
        /// Decrypt data with <paramref name="passwordBytes"/> and <paramref name="cryptHeader"/>.
        /// </summary>
        /// <param name="data">Encrypted compressed zip entry data.</param>
        /// <param name="passwordBytes">Byte sequence of password of zip archive.</param>
        /// <param name="cryptHeader">Crypt header.</param>
        /// <returns>Decrypted data.</returns>
        [Pure]
        public static byte[] DecryptData(ReadOnlySpan<byte> data, ReadOnlySpan<byte> passwordBytes, ReadOnlySpan<byte> cryptHeader)
        {
            return new ZipDecryptor(passwordBytes, cryptHeader).Decrypt(data);
        }

        /// <summary>
        /// Decrypt data with <paramref name="password"/> and <paramref name="cryptHeader"/>.
        /// </summary>
        /// <param name="srcData">Encrypted compressed zip entry data.</param>
        /// <param name="dstData">Destination of decrypted data.</param>
        /// <param name="password">Password of zip archive.</param>
        /// <param name="enc">Encoding of <paramref name="password"/>.</param>
        /// <param name="cryptHeader">Crypt header.</param>
        /// <returns>Decrypted data.</returns>
        public static void DecryptData(ReadOnlySpan<byte> srcData, Span<byte> dstData, string password, Encoding enc, ReadOnlySpan<byte> cryptHeader)
        {
            new ZipDecryptor(password, enc, cryptHeader).Decrypt(srcData, dstData);
        }

        /// <summary>
        /// Decrypt data with <paramref name="passwordBytes"/> and <paramref name="cryptHeader"/>.
        /// </summary>
        /// <param name="srcData">Encrypted compressed zip entry data.</param>
        /// <param name="dstData">Destination of decrypted data.</param>
        /// <param name="passwordBytes">Byte sequence of password of zip archive.</param>
        /// <param name="cryptHeader">Crypt header.</param>
        /// <returns>Decrypted data.</returns>
        public static void DecryptData(ReadOnlySpan<byte> srcData, Span<byte> dstData, ReadOnlySpan<byte> passwordBytes, ReadOnlySpan<byte> cryptHeader)
        {
            new ZipDecryptor(passwordBytes, cryptHeader).Decrypt(srcData, dstData);
        }
    }
}
