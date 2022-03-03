namespace Pronama.ImageSharp.Formats.Bfnt
{
    internal static class BfntConstants
    {
        /// <summary>
        /// The list of mimetypes that equate to a BFNT.
        /// </summary>
        public static readonly IEnumerable<string> MimeTypes = new[] { "image/x-bfnt" };

        /// <summary>
        /// The list of file extensions that equate to a BFNT.
        /// </summary>
        public static readonly IEnumerable<string> FileExtensions = new[] { "BFT", "FNT" };

        /// <summary>
        /// Gets the header bytes identifying a BFNT.
        /// </summary>
        public static ReadOnlySpan<byte> HeaderBytes => new byte[]
        {
            (byte)'B',
            (byte)'F',
            (byte)'N',
            (byte)'T',
            0x1A
        };
    }
}
