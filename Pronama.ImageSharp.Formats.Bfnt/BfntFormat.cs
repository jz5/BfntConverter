using SixLabors.ImageSharp.Formats;

namespace Pronama.ImageSharp.Formats.Bfnt
{
    public sealed class BfntFormat : IImageFormat<BfntMetadata>
    {
        /// <summary>
        /// Gets the current instance.
        /// </summary>
        public static BfntFormat Instance { get; } = new BfntFormat();

        public string Name => "BFNT";
        public string DefaultMimeType => "image/x-bfnt";
        public IEnumerable<string> MimeTypes => BfntConstants.MimeTypes;
        public IEnumerable<string> FileExtensions => BfntConstants.FileExtensions;

        public BfntMetadata CreateDefaultFormatMetadata() => new();
    }
}
