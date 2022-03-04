using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.PixelFormats;

namespace Pronama.ImageSharp.Formats.Bfnt
{
    internal sealed class BfntDecoderCore
    {

        private BinaryReader _currentBinaryReader;

        private BfntMetadata _bfntMetadata;

        private HashSet<int> _transparentPalettes;

        private IBfntDecoderOptions _options;

        /// <summary>
        /// Initializes a new instance of the <see cref="BfntDecoderCore"/> class.
        /// </summary>
        /// <param name="configuration">The configuration.</param>
        /// <param name="options">The options.</param>
        public BfntDecoderCore(Configuration configuration, IBfntDecoderOptions options)
        {
            Configuration = configuration;
            _options=options;
        }

        public Configuration Configuration { get; }

        /// <summary>
        /// Gets the <see cref="ImageMetadata"/> decoded by this decoder instance.
        /// </summary>
        public ImageMetadata Metadata { get; private set; }

        public Image<TPixel> Decode<TPixel>(Stream stream, CancellationToken cancellationToken) where TPixel : unmanaged, IPixel<TPixel>
        {
            _currentBinaryReader = new BinaryReader(stream);

            ReadHeader();
            var image = ReadData<TPixel>();

            _currentBinaryReader.Close();

            return image;
        }

        public IImageInfo Identify(Stream stream, CancellationToken cancellationToken)
        {
            _currentBinaryReader = new BinaryReader(stream);
            ReadHeader();
            _currentBinaryReader.Close();

            return new BfntImageInfo(new PixelTypeInfo(_bfntMetadata.ColorBits), _bfntMetadata.Xdots, _bfntMetadata.Ydots, Metadata);
        }

        private void ReadHeader()
        {
            Metadata = new ImageMetadata();
            _bfntMetadata = Metadata.GetFormatMetadata(BfntFormat.Instance);

            var log = new List<string>();

            var br = _currentBinaryReader;

            //
            // 基本ヘッダ
            //
            var prefix = br.ReadBytes(5);
            if (!prefix.AsSpan().SequenceEqual(BfntConstants.HeaderBytes))
            {
                return;
            }

            _bfntMetadata.Col = br.ReadByte();
            _bfntMetadata.Ver = br.ReadByte();
            br.ReadByte();
            _bfntMetadata.Xdots = br.ReadUInt16(); // little endian
            _bfntMetadata.Ydots = br.ReadUInt16(); // little endian
            _bfntMetadata.Start = br.ReadUInt16(); // little endian
            _bfntMetadata.End = br.ReadUInt16(); // little endian

            _bfntMetadata.FontName = new string(br.ReadChars(8));
            var time = br.ReadInt32();
            if (time > 0)
                _bfntMetadata.Time = DateTimeOffset.FromUnixTimeSeconds(time).LocalDateTime;

            var extSize = br.ReadInt16();
            var hdrSize = br.ReadInt16();
            _transparentPalettes = new HashSet<int>();

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
                        if (_bfntMetadata.ExtFontName != null)
                            log.Add($"拡張フォント名が複数あります: {extFontName}");
                        else
                            _bfntMetadata.ExtFontName = extFontName;
                    }
                    else if (id == 0x03)
                    {
                        // 作成者(可変長)
                        var author = new string(br.ReadChars(hdrSize - 3));
                        if (_bfntMetadata.Author != null)
                            log.Add($"作成者が複数あります: {author}");
                        else
                            _bfntMetadata.Author = author;
                    }
                    else if (id == 0x10)
                    {
                        // 透明色パレット指定(3バイト固定)
                        var buf = br.ReadBytes(3);
                        var no = buf[0] << 16 | buf[1] << 8 | buf[2];
                        _transparentPalettes.Add(no);

                    }
                    else if (id == 0x3f)
                    {
                        // コメント(可変長)
                        var comment = new string(br.ReadChars(hdrSize - 3));
                        if (_bfntMetadata.Comment != null)
                            log.Add($"作成者が複数あります: {comment}");
                        else
                            _bfntMetadata.Comment = comment;
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
            var br = _currentBinaryReader;

            var columns = _options.Columns;
            (int, int) GetColRow(int code)
            {
                return (code % columns, code / columns);
            }

            var (_, rowStart) = GetColRow(_bfntMetadata.Start);
            var (_, rowEnd) = GetColRow(_bfntMetadata.End);

            var rowCount = rowEnd - rowStart + 1;

            var tableImage = new Image<TPixel>(Configuration, _bfntMetadata.Xdots * columns, _bfntMetadata.Ydots * rowCount);


            //
            // パレット
            //
            var palettes = new List<byte[]>();
            if (_bfntMetadata.HasPalette)
            {
                for (var i = 0; i < _bfntMetadata.ColorCount; i++)
                {
                    var brg = br.ReadBytes(3);
                    palettes.Add(brg);
                }
            }
            else
            {
                // 256色以下でパレット無しの場合はグレースケールのパレットを生成して使用（この Converter の仕様）
                if (_bfntMetadata.ColorBits <= 7) // <= 256 colors
                {
                    for (var i = 0; i < _bfntMetadata.ColorCount; i++)
                    {
                        var val = (byte)(0xff / (_bfntMetadata.ColorCount - 1) * i);
                        palettes.Add(new[] { val, val, val });
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
            var hasTransparent = _bfntMetadata.TransparentPallets.Any();

            for (var code = _bfntMetadata.Start; code <= _bfntMetadata.End; code++)
            {
                Bgra32[] bgraBytes = new Bgra32[_bfntMetadata.Xdots * _bfntMetadata.Ydots + 7];

                if (_bfntMetadata.ColorBits == 0)
                {
                    // 2 colors
                    // MSB                           LSB
                    // +---+---+---+---+---+---+---+---+
                    // |X+0|X+1|X+2|X+3|X+4|X+5|X+6|X+7|
                    // +---+---+---+---+---+---+---+---+
                    var index = 0;
                    for (var i = 0; i < (_bfntMetadata.Xdots * _bfntMetadata.Ydots + 1) / 8; i++)
                    {
                        var buf = br.ReadByte();
                        for (var s = 0; s < 8; s++)
                        {
                            var no = (buf >> (7 - s)) & 0x1;
                            var a = (hasTransparent && (_bfntMetadata.TransparentPallets?.Contains(no) ?? false)) ? (byte)0 : (byte)0xff;
                            bgraBytes[index++] = new Bgra32(palettes[no][1], palettes[no][2], palettes[no][0], a);
                        }
                    }
                }
                else if (_bfntMetadata.ColorBits == 1)
                {
                    // 4 colors
                    // MSB                           LSB
                    // +---+---+---+---+---+---+---+---+
                    // | X + 0 | X + 1 | X + 2 | X + 3 |
                    // +---+---+---+---+---+---+---+---+
                    var index = 0;
                    for (var i = 0; i < (_bfntMetadata.Xdots * _bfntMetadata.Ydots + 1) / 4; i++)
                    {
                        var buf = br.ReadByte();
                        for (var s = 0; s < 8; s += 2)
                        {
                            var no = (buf >> (6 - s)) & 0b11;
                            var a = (hasTransparent && (_bfntMetadata.TransparentPallets?.Contains(no) ?? false)) ? (byte)0 : (byte)0xff;
                            bgraBytes[index++] = new Bgra32(palettes[no][1], palettes[no][2], palettes[no][0], a);
                        }
                    }
                }
                else if (_bfntMetadata.ColorBits is 2 or 3)
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
                    for (var i = 0; i < (_bfntMetadata.Xdots * _bfntMetadata.Ydots + 1) / 2; i++)
                    {
                        var buf = br.ReadByte();
                        var no1 = buf >> 4;
                        var no2 = buf & 0b1111;

                        var a1 = (hasTransparent && (_bfntMetadata.TransparentPallets?.Contains(no1) ?? false)) ? (byte)0 : (byte)0xff;
                        var a2 = (hasTransparent && (_bfntMetadata.TransparentPallets?.Contains(no2) ?? false)) ? (byte)0 : (byte)0xff;

                        bgraBytes[index++] = new Bgra32(palettes[no1][1], palettes[no1][2], palettes[no1][0], a1);
                        bgraBytes[index++] = new Bgra32(palettes[no2][1], palettes[no2][2], palettes[no2][0], a2);
                    }
                }
                else if (_bfntMetadata.ColorBits is >= 4 and <= 7)
                {
                    // 32 colors ～ 256 colors (1bytes / dot)
                    // MSB                           LSB
                    // +---+---+---+---+---+---+---+---+
                    // |             X + 0             |
                    // +---+---+---+---+---+---+---+---+
                    for (var i = 0; i < _bfntMetadata.Xdots * _bfntMetadata.Ydots; i++)
                    {
                        var no = br.ReadByte();
                        var a = (hasTransparent && (_bfntMetadata.TransparentPallets?.Contains(no) ?? false)) ? (byte)0 : (byte)0xff;
                        bgraBytes[i] = new Bgra32(palettes[no][1], palettes[no][2], palettes[no][0], a);
                    }
                }
                else if (_bfntMetadata.ColorBits is >= 8 and <= 15)
                {
                    // 512 colors ～ 65536 colors (2bytes / dot)
                    // MSB                                                           LSB
                    // +---+---+---+---+---+---+---+---+---+---+---+---+---+---+---+---+
                    // |                             X + 0                             |
                    // +---+---+---+---+---+---+---+---+---+---+---+---+---+---+---+---+
                    for (var i = 0; i < _bfntMetadata.Xdots * _bfntMetadata.Ydots; i++)
                    {
                        var buf = br.ReadBytes(2);
                        var no = buf[0] << 8 | buf[1];
                        var a = (hasTransparent && (_bfntMetadata.TransparentPallets?.Contains(no) ?? false)) ? (byte)0 : (byte)0xff;
                        bgraBytes[i] = new Bgra32(palettes[no][1], palettes[no][2], palettes[no][0], a);
                    }
                }
                else if (_bfntMetadata.ColorBits is >= 16 and <= 23)
                {
                    // 131027 colors ～ 16777216 colors (3bytes / dot)
                    // MSB                                                                   LSB
                    // +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
                    // |                                 X + 0                                 |
                    // +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
                    for (var i = 0; i < _bfntMetadata.Xdots * _bfntMetadata.Ydots; i++)
                    {
                        var buf = br.ReadBytes(3);
                        var no = buf[0] << 16 | buf[1] << 8 | buf[2];
                        var a = (hasTransparent && (_bfntMetadata.TransparentPallets?.Contains(no) ?? false)) ? (byte)0 : (byte)0xff;
                        bgraBytes[i] = new Bgra32(palettes[no][1], palettes[no][2], palettes[no][0], a);
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
                var offsetY = row * _bfntMetadata.Ydots;
                var offsetX = col * _bfntMetadata.Xdots;
                
                tableImage.ProcessPixelRows(accessor =>
                {
                    var index = 0;
                    for (var y = 0; y < _bfntMetadata.Ydots; y++)
                    {
                        var pixelRow = accessor.GetRowSpan(y + offsetY);

                        for (var x = 0; x < _bfntMetadata.Xdots; x++)
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
