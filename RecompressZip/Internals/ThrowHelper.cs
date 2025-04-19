#if !NET8_0_OR_GREATER
using System;
#endif  // !NET8_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
using System.IO;
#if !NET8_0_OR_GREATER
using System.Runtime.CompilerServices;
#endif  // !NET8_0_OR_GREATER


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
        [DoesNotReturn]
        internal static void ThrowEndOfStreamException()
        {
            throw new EndOfStreamException();
        }

#if !NET8_0_OR_GREATER
        /// <summary>
        /// Throw <see cref="ArgumentOutOfRangeException"/>.
        /// </summary>
        /// <typeparam name="T">The type of the objects.</typeparam>
        /// <param name="value">The value of the argument that causes this exception.</param>
        /// <param name="other">The value to compare with <paramref name="value"/>.</param>
        /// <param name="paramName">The name of the parameter with which <paramref name="value"/> corresponds.</param>
        /// <exception cref="ArgumentOutOfRangeException">Always thrown.</exception>
        [DoesNotReturn]
        private static void ThrowLess<T>(T value, T other, string? paramName)
        {
            throw new ArgumentOutOfRangeException(paramName, value, $"'{value}' must be greater than or equal to '{other}'.");
        }

        /// <summary>
        /// Throws an <see cref="ArgumentOutOfRangeException"/> if <paramref name="value"/> is less than <paramref name="other"/>.
        /// </summary>
        /// <typeparam name="T">The type of the objects to validate.</typeparam>
        /// <param name="value">The argument to validate as greater than or equal to <paramref name="other"/>.</param>
        /// <param name="other">The value to compare with <paramref name="value"/>.</param>
        /// <param name="paramName">The name of the parameter with which <paramref name="value"/> corresponds.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="value"/> is less than <paramref name="other"/>.</exception>
        internal static void ThrowIfLessThan<T>(T value, T other, [CallerArgumentExpression(nameof(value))] string? paramName = null)
            where T : IComparable<T>
        {
            if (value.CompareTo(other) < 0)
            {
                ThrowLess(value, other, paramName);
            }
        }
#endif  // !NET8_0_OR_GREATER
    }
}
