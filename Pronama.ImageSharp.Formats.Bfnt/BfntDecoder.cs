using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace Pronama.ImageSharp.Formats.Bfnt
{
    public sealed class BfntDecoder : IImageDecoder, IBfntDecoderOptions, IImageInfoDetector
    {
        public Image<TPixel> Decode<TPixel>(Configuration configuration, Stream stream) where TPixel : unmanaged, IPixel<TPixel>
        {
            var decoder = new BfntDecoderCore(configuration, this);
            try
            {
                return decoder.Decode<TPixel>(stream, default);
            }
            catch (InvalidMemoryOperationException ex)
            {
                throw new InvalidImageContentException("Cannot decode image.", ex);
            }
        }

        public Image Decode(Configuration configuration, Stream stream) => Decode<Bgra32>(configuration, stream);


        public Task<Image<TPixel>> DecodeAsync<TPixel>(Configuration configuration, Stream stream, CancellationToken cancellationToken) where TPixel : unmanaged, IPixel<TPixel>
        {
            throw new NotImplementedException();
        }

        public Task<Image> DecodeAsync(Configuration configuration, Stream stream, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public IImageInfo Identify(Configuration configuration, Stream stream) => new BfntDecoderCore(configuration, this).Identify(stream, default);

        public Task<IImageInfo> IdentifyAsync(Configuration configuration, Stream stream, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}
