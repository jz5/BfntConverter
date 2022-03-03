using SixLabors.ImageSharp.Metadata;

namespace Pronama.ImageSharp.Formats.Bfnt
{
    public static class MetadataExtensions
    {
        /// <summary>
        /// Gets the BFNT format specific metadata for the image.
        /// </summary>
        /// <param name="metadata">The metadata this method extends.</param>
        /// <returns>The <see cref="BfntMetadata"/>.</returns>
        public static BfntMetadata GetBfntMetadata(this ImageMetadata metadata) => metadata.GetFormatMetadata(BfntFormat.Instance);
    }
}
