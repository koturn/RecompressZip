using System;
using System.IO;
using System.Runtime.InteropServices;


namespace RecompressZip
{
    public static class SpanUtil
    {
        /// <summary>
        /// Create <see cref="Span{T}"/> from <see cref="MemoryStream"/>.
        /// </summary>
        /// <param name="ms">An instance of <see cref="MemoryStream"/>.</param>
        /// <returns><see cref="Span{T}"/> of <paramref name="ms"/>.</returns>
        public static Span<byte> CreateSpan(MemoryStream ms)
        {
            return ms.GetBuffer().AsSpan(0, (int)ms.Length);
        }

        /// <summary>
        /// Create <see cref="Span{T}"/> from <see cref="SafeBuffer"/>.
        /// </summary>
        /// <param name="sb">An instance of <see cref="SafeBuffer"/>.</param>
        /// <returns><see cref="Span{T}"/> of <paramref name="sb"/>.</returns>
        public static unsafe Span<byte> CreateSpan(SafeBuffer sb)
        {
            return new Span<byte>((void*)sb.DangerousGetHandle(), (int)sb.ByteLength);
        }
    }
}
