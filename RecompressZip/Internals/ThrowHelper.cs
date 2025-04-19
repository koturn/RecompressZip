using System.IO;


namespace RecompressZip.Internals
{
    /// <summary>
    /// Provides exception throwing methods.
    /// </summary>
    internal static class ThrowHelper
    {
        /// <summary>
        /// Throws <see cref="EndOfStreamException"/>.
        /// </summary>
        /// <exception cref="EndOfStreamException">Always thrown.</exception>
        internal static void ThrowEndOfStreamException()
        {
            throw new EndOfStreamException();
        }
    }
}
