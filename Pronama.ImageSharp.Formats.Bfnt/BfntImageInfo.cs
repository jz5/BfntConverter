using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Metadata;

namespace Pronama.ImageSharp.Formats.Bfnt
{
    public class BfntImageInfo : IImageInfo
    {
        public BfntImageInfo(PixelTypeInfo pixelType, int width, int height, ImageMetadata metadata)
        {
            PixelType = pixelType;
            Width = width;
            Height = height;
            Metadata = metadata;
        }

        public PixelTypeInfo PixelType { get; }
        public int Width { get; }
        public int Height { get; }
        public ImageMetadata Metadata { get; }
    }
}