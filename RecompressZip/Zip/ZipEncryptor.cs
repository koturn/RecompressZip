using System;


namespace RecompressZip.Zip
{
    /// <summary>
    /// Encryptor of ZipCrypto.
    /// </summary>
    public class ZipEncryptor : ZipCryptor
    {
        /// <summary>
        /// Initialize crypt key with <paramref name="password"/> and <paramref name="crc32"/>
        /// and create new crypt header.
        /// </summary>
        /// <param name="password">Password of zip archive.</param>
        /// <param name="crc32">CRC-32 value of zip entry.</param>
        public ZipEncryptor(string password, uint crc32)
        {
            InitializeKeysWithPassword(password);
            InitializeCryptHeader(crc32);
        }

        /// <summary>
        /// Initialize crypt key with <paramref name="password"/> and <paramref name="cryptHeader"/>
        /// and create new crypt header.
        /// </summary>
        /// <param name="password">Password of zip archive.</param>
        /// <param name="cryptHeader">Crypt header.</param>
        public ZipEncryptor(string password, ReadOnlySpan<byte> cryptHeader)
        {
            InitializeKeysWithPassword(password);
            InitializeCryptHeader(cryptHeader);
        }

        /// <summary>
        /// Encrypt data.
        /// </summary>
        /// <param name="data">Source data.</param>
        /// <returns>Encrypted data.</returns>
        public byte[] Encrypt(ReadOnlySpan<byte> data)
        {
            var dstData = new byte[data.Length];
            Encrypt(data, dstData);
            return dstData;
        }

        /// <summary>
        /// Encrypt data.
        /// </summary>
        /// <param name="srcData">Source data.</param>
        /// <param name="dstData">Destination of encrypted data.</param>
        public void Encrypt(ReadOnlySpan<byte> srcData, Span<byte> dstData)
        {
            for (int i = 0; i < srcData.Length; i++) {
                dstData[i] = Encrypt(srcData[i]);
            }
        }

        /// <summary>
        /// Encrypt single byte.
        /// </summary>
        /// <param name="b">A target byte.</param>
        /// <returns>An encrypted byte.</returns>
        public byte Encrypt(byte b)
        {
            var t = GetDecryptByte();
            UpdateKeys(b);
            return (byte)(t ^ b);
        }

        /// <summary>
        /// Initialize crypt header and update ZipCrypto keys.
        /// </summary>
        /// <param name="crc32">CRC-32 value of zip entry.</param>
        private void InitializeCryptHeader(uint crc32)
        {
            var ch = CryptHeader;
            var random = new Random();
            for (int i = 0; i < ch.Length - 1; i++)
            {
                ch[i] = Encrypt((byte)random.Next(0, 255));
            }
            ch[^1] = Encrypt((byte)crc32);
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
                UpdateKeys((byte)(ch[i] ^ GetDecryptByte()));
            }
        }

        /// <summary>
        /// Encrypt data with specified password and CRC-32 value.
        /// </summary>
        /// <param name="data">Compressed zip entry data.</param>
        /// <param name="password">Password of zip archive.</param>
        /// <param name="crc32">CRC-32 value of zip entry.</param>
        /// <param name="outputCryptHeader">Destination of output crypt header.</param>
        /// <returns>Encrypted data.</returns>
        public static byte[] EncryptData(ReadOnlySpan<byte> data, string password, uint crc32, Span<byte> outputCryptHeader)
        {
            var ze = new ZipEncryptor(password, crc32);
            ze.CryptHeader.CopyTo(outputCryptHeader);
            var encryptedData = ze.Encrypt(data);
            return encryptedData;
        }

        /// <summary>
        /// Encrypt data with <paramref name="password"/> and <paramref name="crc32"/> and write it to <paramref name="dstData"/>.
        /// </summary>
        /// <param name="srcData">Compressed zip entry data.</param>
        /// <param name="dstData">Destination of encrypted data.</param>
        /// <param name="password">Password of zip archive.</param>
        /// <param name="crc32">CRC-32 value of zip entry.</param>
        /// <param name="outputCryptHeader">Destination of output crypt header.</param>
        public static void EncryptData(ReadOnlySpan<byte> srcData, Span<byte> dstData, string password, uint crc32, Span<byte> outputCryptHeader)
        {
            var ze = new ZipEncryptor(password, crc32);
            ze.CryptHeader.CopyTo(outputCryptHeader);
            ze.Encrypt(srcData, dstData);
        }

        /// <summary>
        /// Encrypt data with specified password and crypt header.
        /// </summary>
        /// <param name="data">Compressed zip entry data.</param>
        /// <param name="password">Password of zip archive.</param>
        /// <param name="cryptHeader">Crypt header.</param>
        /// <returns>Encrypted data.</returns>
        public static byte[] EncryptData(ReadOnlySpan<byte> data, string password, ReadOnlySpan<byte> cryptHeader)
        {
            return new ZipEncryptor(password, cryptHeader).Encrypt(data);
        }

        /// <summary>
        /// Encrypt data with specified password and crypt header.
        /// </summary>
        /// <param name="srcData">Compressed zip entry data.</param>
        /// <param name="dstData">Destination of encrypted data.</param>
        /// <param name="password">Password of zip archive.</param>
        /// <param name="cryptHeader">Crypt header.</param>
        public static void EncryptData(ReadOnlySpan<byte> srcData, Span<byte> dstData, string password, ReadOnlySpan<byte> cryptHeader)
        {
            new ZipEncryptor(password, cryptHeader).Encrypt(srcData, dstData);
        }
    }
}
