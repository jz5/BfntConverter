using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Pronama.ImageSharp.Formats.Bfnt
{
    internal sealed class BfntEncoderCore
    {
        private readonly IBfntEncoderOptions _options;

        public BfntEncoderCore(IBfntEncoderOptions options)
        {
            _options = options;
        }

        public void Encode<TPixel>(Image<TPixel> image, Stream stream, CancellationToken cancellationToken)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            // パレット作成
            var palettes = new SortedDictionary<uint, int>(); // key: RGBA, value: palette no.
            var transparentPalettes = new Dictionary<uint, int>(); // key: RGBA, value: palette no.

            image.ProcessPixelRows(accessor =>
            {
                for (var y = 0; y < accessor.Height; y++)
                {
                    var pixelRow = accessor.GetRowSpan(y);

                    for (var x = 0; x < pixelRow.Length; x++)
                    {
                        ref var pixel = ref pixelRow[x];
                        var rgba = new Rgba32();
                        pixel.ToRgba32(ref rgba);

                        if (palettes.ContainsKey(rgba.PackedValue))
                            continue;

                        palettes.Add(rgba.PackedValue, 0/*仮の値*/);

                        if (rgba.A == 0)
                            transparentPalettes.Add(rgba.PackedValue, 0/*仮の値*/);
                    }
                }

                // パレットをソート
                var index = 0;
                foreach (var key in palettes.Keys.ToList())
                {
                    palettes[key] = index;

                    if (transparentPalettes.ContainsKey(key))
                    {
                        transparentPalettes[key] = index;
                    }
                    ++index;
                }
            });

            // 色数
            var colorBits = 0;
            var count = palettes.Count;

            for (var i = 0; i < 24; i++)
            {
                if (count > 1 << (i + 1))
                    continue;

                colorBits = i;
                break;
            }


            var bw = new BinaryWriter(stream);

            // prefix
            bw.Write(BfntConstants.HeaderBytes);
            // Col
            bw.Write((byte)(_options.IncludesPalette ? (1 << 7) | colorBits : colorBits));

            bw.Write((byte)0x16); // Ver
            bw.Write((byte)0); // 0x00

            // Xdots
            bw.Write(_options.Xdots);
            // Ydots
            bw.Write(_options.Ydots);
            // START
            bw.Write(_options.Start);
            // END
            var end = image.Width * image.Height / (_options.Xdots * _options.Ydots);
            if (end > ushort.MaxValue)
            {
                throw new InvalidImageContentException("Invalid Start value.");
            }
            bw.Write((ushort)end);

            // font name
            bw.Write(new byte[8]);
            // time
            bw.Write((uint)0);
            // extSize
            bw.Write((ushort)0);
            // hdrSize
            bw.Write((ushort)0);

            // パレット
            if (_options.IncludesPalette)
            {
                var paletteCount = 1 << (colorBits + 1);
                var packedValues = palettes.Keys.ToList();

                foreach (var rgba in packedValues.Select(packed => new Rgba32(packed)))
                {
                    bw.Write(rgba.B);
                    bw.Write(rgba.R);
                    bw.Write(rgba.G);
                }

                var length = (paletteCount - packedValues.Count) * 3;
                if (length > 0)
                {
                    var blankBuffer = new byte[length];
                    bw.Write(blankBuffer);
                }
            }

            // ベタデータ
            var columns = image.Width / _options.Xdots;
            (int, int) GetColRow(int code)
            {
                return (code % columns, code / columns);
            }

            image.ProcessPixelRows(accessor =>
            {
                byte data = 0;
                var shift = 0;

                switch (colorBits)
                {
                    case 0:
                        shift = 7;
                        break;
                    case 1:
                        shift = 6;
                        break;
                    case 2:
                    case 3:
                        shift = 4;
                        break;
                }

                for (var code = 0; code < 256; code++)
                {
                    var (col, row) = GetColRow(code);
                    var offsetY = row * _options.Xdots;
                    var offsetX = col * _options.Ydots;

                    for (var y = 0; y < _options.Ydots; y++)
                    {
                        var pixelRow = accessor.GetRowSpan(y + offsetY);
                        for (var x = 0; x < _options.Xdots; x++)
                        {
                            ref var pixel = ref pixelRow[x + offsetX];

                            var rgba = new Rgba32();
                            pixel.ToRgba32(ref rgba);

                            if (colorBits == 0)
                            {
                                // 2 colors
                                // MSB                           LSB
                                // +---+---+---+---+---+---+---+---+
                                // |X+0|X+1|X+2|X+3|X+4|X+5|X+6|X+7|
                                // +---+---+---+---+---+---+---+---+
                                data |= (byte)(palettes[rgba.PackedValue] << shift);
                                --shift;
                                if (shift >= 0) continue;

                                bw.Write(data);

                                // reset
                                shift = 7;
                                data = 0;
                            }
                            else if (colorBits == 1)
                            {
                                // 4 colors
                                // MSB                           LSB
                                // +---+---+---+---+---+---+---+---+
                                // | X + 0 | X + 1 | X + 2 | X + 3 |
                                // +---+---+---+---+---+---+---+---+
                                data |= (byte)(palettes[rgba.PackedValue] << shift);
                                shift -= 2;
                                if (shift >= 0) continue;

                                bw.Write(data);

                                // reset
                                shift = 6;
                                data = 0;
                            }
                            else if (colorBits is 2 or 3)
                            {
                                // 8 colors
                                // MSB                           LSB
                                // +---+---+---+---+---+---+---+---+
                                // |0x0|   X + 0   |0x0|   X + 1   |
                                // +---+---+---+---+---+---+---+---+
                                // 16 colors
                                // MSB                           LSB
                                // +---+---+---+---+---+---+---+---+
                                // |     X + 0     |     X + 1     |
                                // +---+---+---+---+---+---+---+---+
                                data |= (byte)(palettes[rgba.PackedValue] << shift);
                                shift -= 4;
                                if (shift >= 0) continue;

                                bw.Write(data);

                                // reset
                                shift = 4;
                                data = 0;
                            }
                            else if (colorBits is >= 4 and <= 7)
                            {
                                // 32 colors ～ 256 colors (1bytes / dot)
                                // MSB                           LSB
                                // +---+---+---+---+---+---+---+---+
                                // |             X + 0             |
                                // +---+---+---+---+---+---+---+---+
                                bw.Write((byte)(palettes[rgba.PackedValue]));
                            }
                            else if (colorBits is >= 8 and <= 15)
                            {
                                // 512 colors ～ 65536 colors (2bytes / dot)
                                // MSB                                                           LSB
                                // +---+---+---+---+---+---+---+---+---+---+---+---+---+---+---+---+
                                // |                             X + 0                             |
                                // +---+---+---+---+---+---+---+---+---+---+---+---+---+---+---+---+
                                var index = palettes[rgba.PackedValue];
                                bw.Write((byte)(index >> 8));
                                bw.Write((byte)(index & 0xff));
                            }
                            else if (colorBits is >= 16 and <= 23)
                            {
                                // 131027 colors ～ 16777216 colors (3bytes / dot)
                                // MSB                                                                   LSB
                                // +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
                                // |                                 X + 0                                 |
                                // +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
                                var index = palettes[rgba.PackedValue];

                                bw.Write((byte)(index >> 16));
                                bw.Write((byte)((index >> 8) & 0xff));
                                bw.Write((byte)(index & 0xff));
                            }
                        }
                    }
                }

                switch (colorBits)
                {
                    case 0:
                        if (shift != 7)
                            bw.Write(data);
                        break;
                    case 1:
                        if (shift != 6)
                            bw.Write(data);
                        break;
                    case 2:
                    case 3:
                        if (shift != 4)
                            bw.Write(data);

                        break;
                }
            });

            stream.Flush();
        }
    }
}
