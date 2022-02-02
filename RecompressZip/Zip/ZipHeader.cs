using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;


namespace RecompressZip.Zip
{
    /// <summary>
    /// Common part of header, <see cref="LocalFileHeader"/>, <see cref="CentralDirectoryFileHeader"/>
    /// or <see cref="CentralDirectoryEndRecord"/>.
    /// </summary>
    public abstract class ZipHeader
    {
        /// <summary>
        /// File header signature.
        /// <list type="table">
        ///   <listheader>
        ///     <term><see cref="LocalFileHeader"/></term>
        ///     <description>0x04034b50 (<see cref="ZipSignature.LocalFileHeader"/>)</description>
        ///   </listheader>
        ///   <item>
        ///     <term><see cref="CentralDirectoryFileHeader"/></term>
        ///     <description>0x02014b50 (<see cref="ZipSignature.CentralDirectoryFileHeader"/>)</description>
        ///   </item>
        ///   <item>
        ///     <term><see cref="CentralDirectoryEndRecord"/></term>
        ///     <description>0x06054b50 (<see cref="ZipSignature.EndRecord"/>)</description>
        ///   </item>
        /// </list>
        /// </summary>
        public ZipSignature Signature { get; set; }

        /// <summary>
        /// Read zip signature, just read 4 bytes.
        /// </summary>
        /// <param name="reader"><see cref="BinaryReader"/> of zip data.</param>
        /// <returns>Signature.</returns>
        public static ZipSignature ReadSignature(BinaryReader reader)
        {
            var signature = (ZipSignature)reader.ReadUInt32();
            if (!Enum.IsDefined(signature))
            {
                ThrowInvalidDataException(signature, reader.BaseStream.Position - sizeof(ZipSignature));
            }
            return signature;
        }


        /// <summary>
        /// Throw <see cref="InvalidDataException"/>.
        /// </summary>
        /// <param name="signature">Error signature value.</param>
        /// <param name="position">Stream position.</param>
        /// <exception cref="InvalidDataException">Always thrown from this method.</exception>
        [DoesNotReturn]
        private static void ThrowInvalidDataException(ZipSignature signature, long position)
        {
            ThrowInvalidDataException($"Invalid zip signature 0x{(uint)signature:X8} at 0x{position:X8}");
        }

        /// <summary>
        /// Throw <see cref="InvalidDataException"/>.
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception.</param>
        /// <exception cref="InvalidDataException">Always thrown from this method.</exception>
        [DoesNotReturn]
        private static void ThrowInvalidDataException(string message)
        {
            throw new InvalidDataException(message);
        }
    }
}
