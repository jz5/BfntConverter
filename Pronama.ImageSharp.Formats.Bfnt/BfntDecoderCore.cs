using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.PixelFormats;

namespace Pronama.ImageSharp.Formats.Bfnt
{
    internal sealed class BfntDecoderCore
    {
        /// <summary>
        /// The stream to decode from.
        /// </summary>
        private Stream currentStream;

        private BinaryReader currentBinaryReader;

        private BfntMetadata bfntMetadata;

        private HashSet<int> transparentPallets;

        /// <summary>
        /// Initializes a new instance of the <see cref="BfntDecoderCore"/> class.
        /// </summary>
        /// <param name="configuration">The configuration.</param>
        /// <param name="options">The options.</param>
        public BfntDecoderCore(Configuration configuration, IBfntDecoderOptions options)
        {
            this.Configuration = configuration;
        }

        public Configuration Configuration { get; }

        /// <summary>
        /// Gets the <see cref="ImageMetadata"/> decoded by this decoder instance.
        /// </summary>
        public ImageMetadata Metadata { get; private set; }

        public Image<TPixel> Decode<TPixel>(Stream stream) where TPixel : unmanaged, IPixel<TPixel>
        {
            //Image<TPixel> image = new Image<TPixel>(400, 400);

            //TPixel color = default;
            //color.FromRgba32(new Rgba32(0, 0, 0));
            //image[200, 200] = color; // also works on ImageFrame<T>

            //return image;



            currentStream = stream;
            currentBinaryReader = new BinaryReader(stream);

            ReadHeader();
            var image = ReadData<TPixel>();

            currentBinaryReader.Close();

            return image;
        }

        public IImageInfo Identify(Stream stream, CancellationToken cancellationToken)
        {
            currentStream = stream;
            currentBinaryReader = new BinaryReader(stream);
            ReadHeader();
            currentBinaryReader.Close();

            return new BfntImageInfo(new PixelTypeInfo(bfntMetadata.ColorBits), bfntMetadata.Xdots, bfntMetadata.Ydots, Metadata);
        }

        private void ReadHeader()
        {
            Metadata = new ImageMetadata();
            bfntMetadata = Metadata.GetFormatMetadata(BfntFormat.Instance);

            var log = new List<string>();

            var br = currentBinaryReader;

            //
            // 基本ヘッダ
            //
            var prefix = br.ReadBytes(5);
            if (!prefix.AsSpan().SequenceEqual(BfntConstants.HeaderBytes))
            {
                return;
            }

            bfntMetadata.Col = br.ReadByte();
            bfntMetadata.Ver = br.ReadByte();
            br.ReadByte();
            bfntMetadata.Xdots = br.ReadInt16(); // little endian
            bfntMetadata.Ydots = br.ReadInt16(); // little endian
            bfntMetadata.Start = br.ReadInt16(); // little endian
            bfntMetadata.End = br.ReadInt16(); // little endian

            bfntMetadata.FontName = new string(br.ReadChars(8));
            var time = br.ReadInt32();
            if (time > 0)
                bfntMetadata.Time = DateTimeOffset.FromUnixTimeSeconds(time).LocalDateTime;

            var extSize = br.ReadInt16();
            var hdrSize = br.ReadInt16();
            transparentPallets = new HashSet<int>();

            //
            // 拡張ヘッダ
            //
            if (extSize > 0)
            {
                // 拡張ヘッダ
                //          +00 +01 +02 +03 +04 +05 +06 +07 +08 +09 +0A +0B +0C +0D +0E +0F
                //          +---+---+---+---+---+---+---+---+---+---+---+---+---+---+---+---+
                // 00000000 |'B'|'F'|'N'|'T'|x1a|col|ver|x00| Xdots | Ydots | START | END   |
                //          +---+---+---+---+---+---+---+---+---+---+---+---+---+---+---+---+
                // 00000010 |←          font name          →|←     time    →|extSize|hdrSize|
                //          +---+---+---+---+---+---+---+---+---+---+---+---+---+---+---+---+
                // 00000020 |ID |      @@@      |hdrSize|ID |  @@@  | x0000 |
                //          +---+---+---+---+---+---+---+---+---+---+---+---+

                // extSize = 先頭からのoffset 20H以降に続く拡張ヘッダの総サイズ (0なら拡張ヘッダなし)
                // hdrSize = 続くヘッダのサイズ(hdrSize自身のサイズ2bytesを含む) (0なら終わり)
                // ID      = ヘッダの種類を表す
                // @@@     = ヘッダの実体(IDによって固定長または可変長)

                while (hdrSize > 0)
                {
                    var id = br.ReadByte(); // ID: ヘッダの種類を表す
                    if (id == 0)
                    {
                        // 拡張フォント名(可変長)
                        var extFontName = new string(br.ReadChars(hdrSize - 3));
                        if (bfntMetadata.ExtFontName != null)
                            log.Add($"拡張フォント名が複数あります: {extFontName}");
                        else
                            bfntMetadata.ExtFontName = extFontName;
                    }
                    else if (id == 0x03)
                    {
                        // 作成者(可変長)
                        var author = new string(br.ReadChars(hdrSize - 3));
                        if (bfntMetadata.Author != null)
                            log.Add($"作成者が複数あります: {author}");
                        else
                            bfntMetadata.Author = author;
                    }
                    else if (id == 0x10)
                    {
                        // 透明色パレット指定(3バイト固定)
                        var buf = br.ReadBytes(3);
                        var no = buf[0] << 16 | buf[1] << 8 | buf[2];
                        transparentPallets.Add(no);

                    }
                    else if (id == 0x3f)
                    {
                        // コメント(可変長)
                        var comment = new string(br.ReadChars(hdrSize - 3));
                        if (bfntMetadata.Comment != null)
                            log.Add($"作成者が複数あります: {comment}");
                        else
                            bfntMetadata.Comment = comment;
                    }
                    else
                    {
                        br.ReadChars(hdrSize - 3);
                        log.Add($"未知のIDを検出しました: ID = {id}");
                    }

                    // Read next hdrSize
                    hdrSize = br.ReadInt16();
                }
            }
        }

        private Image<TPixel> ReadData<TPixel>() where TPixel : unmanaged, IPixel<TPixel>
        {
            var br = currentBinaryReader;

            static (int, int) GetColRow(int code)
            {
                var col = 16;
                return (code % col, code / col);
            }

            var (_, rowStart) = GetColRow(bfntMetadata.Start);
            var (_, rowEnd) = GetColRow(bfntMetadata.End);

            var rowCount = rowEnd - rowStart + 1;

            var tableImage = new Image<TPixel>(Configuration,bfntMetadata.Xdots * 16, bfntMetadata.Ydots * rowCount);
            

            //
            // パレット
            //
            var pallets = new List<byte[]>();
            if (bfntMetadata.HasPallet)
            {
                for (var i = 0; i < bfntMetadata.ColorCount; i++)
                {
                    var brg = br.ReadBytes(3);
                    pallets.Add(brg);
                }
            }
            else
            {
                // 256色以下でパレット無しの場合はグレースケールのパレットを生成して使用（この Converter の仕様）
                if (bfntMetadata.ColorBits <= 7) // <= 256 colors
                {
                    for (var i = 0; i < bfntMetadata.ColorCount; i++)
                    {
                        var val = (byte)(0xff / (bfntMetadata.ColorCount - 1) * i);
                        //Console.WriteLine($"pallet {i}: {val.ToString("x2")}");
                        pallets.Add(new[] { val, val, val });
                    }
                }
                else
                {
                    throw new NotSupportedException("色数が512色以上でパレットデータ無しのファイルは、サポートしていません。");
                }
            }

            //
            // ベタデータ
            //
            var hasTransparent = bfntMetadata.TransparentPallets?.Any() ?? false;

            for (var code = bfntMetadata.Start; code <= bfntMetadata.End; code++)
            {
                Bgra32[] bgraBytes = new Bgra32[bfntMetadata.Xdots * bfntMetadata.Ydots + 7];

                if (bfntMetadata.ColorBits == 0)
                {
                    // 2 colors
                    // MSB                           LSB
                    // +---+---+---+---+---+---+---+---+
                    // |X+0|X+1|X+2|X+3|X+4|X+5|X+6|X+7|
                    // +---+---+---+---+---+---+---+---+
                    var index = 0;
                    for (var i = 0; i < (bfntMetadata.Xdots * bfntMetadata.Ydots + 1) / 8; i++)
                    {
                        var buf = br.ReadByte();
                        for (var s = 0; s < 8; s++)
                        {
                            var no = (buf >> (7 - s)) & 0x1;
                            var a = (hasTransparent && (bfntMetadata.TransparentPallets?.Contains(no) ?? false)) ? (byte)0 : (byte)0xff;
                            bgraBytes[index++] = new Bgra32(pallets[no][1], pallets[no][2], pallets[no][0], a);
                        }
                    }
                }
                else if (bfntMetadata.ColorBits == 1)
                {
                    // 4 colors
                    // MSB                           LSB
                    // +---+---+---+---+---+---+---+---+
                    // | X + 0 | X + 1 | X + 2 | X + 3 |
                    // +---+---+---+---+---+---+---+---+
                    var index = 0;
                    for (var i = 0; i < (bfntMetadata.Xdots * bfntMetadata.Ydots + 1) / 4; i++)
                    {
                        var buf = br.ReadByte();
                        for (var s = 0; s < 8; s += 2)
                        {
                            var no = (buf >> (6 - s)) & 0b11;
                            var a = (hasTransparent && (bfntMetadata.TransparentPallets?.Contains(no) ?? false)) ? (byte)0 : (byte)0xff;
                            bgraBytes[index++] = new Bgra32(pallets[no][1], pallets[no][2], pallets[no][0], a);
                        }
                    }
                }
                else if (bfntMetadata.ColorBits == 2 || bfntMetadata.ColorBits == 3)
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
                    var index = 0;
                    for (var i = 0; i < (bfntMetadata.Xdots * bfntMetadata.Ydots + 1) / 2; i++)
                    {
                        var buf = br.ReadByte();
                        var no1 = buf >> 4;
                        var no2 = buf & 0b1111;

                        var a1 = (hasTransparent && (bfntMetadata.TransparentPallets?.Contains(no1) ?? false)) ? (byte)0 : (byte)0xff;
                        var a2 = (hasTransparent && (bfntMetadata.TransparentPallets?.Contains(no2) ?? false)) ? (byte)0 : (byte)0xff;

                        bgraBytes[index++] = new Bgra32(pallets[no1][1], pallets[no1][2], pallets[no1][0], a1);
                        bgraBytes[index++] = new Bgra32(pallets[no2][1], pallets[no2][2], pallets[no2][0], a2);
                    }
                }
                else if (bfntMetadata.ColorBits >= 4 && bfntMetadata.ColorBits <= 7)
                {
                    // 32 colors ～ 256 colors (1bytes / dot)
                    // MSB                           LSB
                    // +---+---+---+---+---+---+---+---+
                    // |             X + 0             |
                    // +---+---+---+---+---+---+---+---+
                    for (var i = 0; i < bfntMetadata.Xdots * bfntMetadata.Ydots; i++)
                    {
                        var no = br.ReadByte();
                        var a = (hasTransparent && (bfntMetadata.TransparentPallets?.Contains(no) ?? false)) ? (byte)0 : (byte)0xff;
                        bgraBytes[i] = new Bgra32(pallets[no][1], pallets[no][2], pallets[no][0], a);
                    }
                }
                else if (bfntMetadata.ColorBits >= 8 && bfntMetadata.ColorBits <= 15)
                {
                    // 512 colors ～ 65536 colors (2bytes / dot)
                    // MSB                                                           LSB
                    // +---+---+---+---+---+---+---+---+---+---+---+---+---+---+---+---+
                    // |                             X + 0                             |
                    // +---+---+---+---+---+---+---+---+---+---+---+---+---+---+---+---+
                    for (var i = 0; i < bfntMetadata.Xdots * bfntMetadata.Ydots; i++)
                    {
                        var buf = br.ReadBytes(2);
                        var no = buf[0] << 8 | buf[1];
                        var a = (hasTransparent && (bfntMetadata.TransparentPallets?.Contains(no) ?? false)) ? (byte)0 : (byte)0xff;
                        bgraBytes[i] = new Bgra32(pallets[no][1], pallets[no][2], pallets[no][0], a);
                    }
                }
                else if (bfntMetadata.ColorBits is >= 16 and <= 23)
                {
                    // 131027 colors ～ 16777216 colors (3bytes / dot)
                    // MSB                                                                   LSB
                    // +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
                    // |                                 X + 0                                 |
                    // +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
                    for (var i = 0; i < bfntMetadata.Xdots * bfntMetadata.Ydots; i++)
                    {
                        var buf = br.ReadBytes(3);
                        var no = buf[0] << 16 | buf[1] << 8 | buf[2];
                        var a = (hasTransparent && (bfntMetadata.TransparentPallets?.Contains(no) ?? false)) ? (byte)0 : (byte)0xff;
                        bgraBytes[i] = new Bgra32(pallets[no][1], pallets[no][2], pallets[no][0], a);
                    }
                }

                //// Create individual image
                //var image = new Image<TPixel>(bfntMetadata.Xdots, bfntMetadata.Ydots);
                //image.ProcessPixelRows(accessor =>
                //{
                //    var index = 0;
                //    for (var y = 0; y < accessor.Height; y++)
                //    {
                //        var pixelRow = accessor.GetRowSpan(y);

                //        for (var x = 0; x < pixelRow.Length; x++)
                //        {
                //            TPixel color = default;
                //            color.FromBgra32(bgraBytes[index]);
                //            pixelRow[x] = color;
                //            ++index;
                //        }
                //    }
                //});
                //return image;


                var (col, row) = GetColRow(code);
                var offsetY = row * bfntMetadata.Ydots;
                var offsetX = col * bfntMetadata.Xdots;

                tableImage.ProcessPixelRows(accessor =>
                {
                    var index = 0;
                    for (var y = 0; y < bfntMetadata.Ydots; y++)
                    {
                        var pixelRow = accessor.GetRowSpan(y + offsetY);

                        for (var x = 0; x < bfntMetadata.Xdots; x++)
                        {
                            TPixel color = default;
                            color.FromBgra32(bgraBytes[index]);
                            pixelRow[x + offsetX] = color;
                            ++index;
                        }
                    }
                });

            }

            return tableImage;
        }
    }
}
