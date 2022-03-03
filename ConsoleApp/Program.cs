using Pronama.ImageSharp.Formats.Bfnt;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Pbm;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Tga;
using SixLabors.ImageSharp.Formats.Tiff;
using SixLabors.ImageSharp.Formats.Webp;

var file = @"C:\Users\jz5\LocalApps\np2_fd\drive\ROLLING.FNT";
//file = @"\\Mac\Home\Downloads\thumbnail.png";

var configuration = new Configuration(
    new PngConfigurationModule(),
    new JpegConfigurationModule(),
    new GifConfigurationModule(),
    new BmpConfigurationModule(),
    new PbmConfigurationModule(),
    new TgaConfigurationModule(),
    new TiffConfigurationModule(),
    new WebpConfigurationModule(),
    new BfntConfigurationModule());


//var imageInfo = Image.Identify(configuration, file, out var format);
//var metadata = imageInfo.Metadata.GetBfntMetadata();


//Console.Write(format);

//Console.WriteLine($"{imageInfo.Width}x{imageInfo.Height} | BPP: {imageInfo.PixelType.BitsPerPixel}");
//Console.WriteLine($"Xdots: {metadata.Xdots}");
//Console.WriteLine($"Ydots: {metadata.Ydots}");
//Console.WriteLine($"Start: {metadata.Start}");
//Console.WriteLine($"End: {metadata.End}");

using var image = Image.Load(configuration, file);
image.SaveAsPng("test.png");