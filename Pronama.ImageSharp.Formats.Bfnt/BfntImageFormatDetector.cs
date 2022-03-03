using SixLabors.ImageSharp.Formats;

namespace Pronama.ImageSharp.Formats.Bfnt
{
    public  sealed class BfntImageFormatDetector : IImageFormatDetector
    {
        public int HeaderSize => 5;

        public IImageFormat DetectFormat(ReadOnlySpan<byte> header)
        {
            return IsSupportedFileFormat(header) ? BfntFormat.Instance : null;
        }

        private bool IsSupportedFileFormat(ReadOnlySpan<byte> header)
        {
            return header.Length >= HeaderSize &&
                   header[..5].SequenceEqual(BfntConstants.HeaderBytes);
        }
    }
}
