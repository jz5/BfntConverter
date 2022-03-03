using SixLabors.ImageSharp;

namespace Pronama.ImageSharp.Formats.Bfnt
{
    /// <summary>
    /// Registers the image encoders, decoders and mime type detectors for the bfnt format.
    /// </summary>
    public sealed class BfntConfigurationModule : IConfigurationModule
    {
        public void Configure(Configuration configuration)
        {
            //configuration.ImageFormatsManager.SetEncoder(BfntFormat.Instance, new PngEncoder());
            configuration.ImageFormatsManager.SetDecoder(BfntFormat.Instance, new BfntDecoder());
            configuration.ImageFormatsManager.AddImageFormatDetector(new BfntImageFormatDetector());
        }
    }
}
