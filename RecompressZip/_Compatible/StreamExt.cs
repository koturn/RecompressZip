#if !NET7_0_OR_GREATER
using RecompressZip.Internals;


#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace System.IO
#pragma warning restore IDE0130 // Namespace does not match folder structure
{
    /// <summary>
    /// Provides extension methods for compatibility.
    /// </summary>
    public static class StreamExt
    {
        /// <summary>
        /// Reads <paramref name="count"/> number of bytes from the current stream and advances the position within the stream.
        /// </summary>
        /// <param name="buffer">
        /// An array of bytes. When this method returns, the buffer contains the specified byte array with the values
        /// between <paramref name="offset"/> and (<paramref name="offset"/> + <paramref name="count"/> - 1) replaced
        /// by the bytes read from the current stream.
        /// </param>
        /// <param name="stream"><see cref="Stream"/> to read.</param>
        /// <param name="offset">The byte offset in <paramref name="buffer"/> at which to begin storing the data read from the current stream.</param>
        /// <param name="count">The number of bytes to be read from the current stream.</param>
        /// <exception cref="ArgumentNullException"><paramref name="buffer"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="offset"/> is outside the bounds of <paramref name="buffer"/>.
        /// -or-
        /// <paramref name="count"/> is negative.
        /// -or-
        /// The range specified by the combination of <paramref name="offset"/> and <paramref name="count"/> exceeds the
        /// length of <paramref name="buffer"/>.
        /// </exception>
        /// <exception cref="EndOfStreamException">
        /// The end of the stream is reached before reading <paramref name="count"/> number of bytes.
        /// </exception>
        /// <remarks>
        /// When <paramref name="count"/> is 0 (zero), this read operation will be completed without waiting for available data in the stream.
        /// </remarks>
        public static int ReadExactly(this Stream stream, byte[] buffer, int offset, int count)
        {
            if (stream.Read(buffer, offset, count) < count)
            {
                ThrowHelper.ThrowEndOfStreamException();
            }
            return count;
        }
    }
}


#endif  // !NET7_0_OR_GREATER
