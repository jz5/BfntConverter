using SixLabors.ImageSharp;

namespace Pronama.ImageSharp.Formats.Bfnt
{
    public class BfntMetadata : IDeepCloneable
    {
        public BfntMetadata()
        {
        }

        public BfntMetadata(BfntMetadata other)
        {
            Col = other.Col;
            Ver = other.Ver;
            Xdots = other.Xdots;
            Ydots = other.Ydots;
            Start = other.Start;
            End = other.End;
            FontName = other.FontName;
            Time = other.Time;

            ExtFontName = other.ExtFontName;
            Author = other.Author;
            Comment = other.Comment;
        }

        public IDeepCloneable DeepClone() => new BfntMetadata(this);


        #region 基本ヘッダ

        public byte Col { get; set; }
        public byte Ver { get; set; }
        public int Xdots { get; set; }
        public int Ydots { get; set; }
        public int Start { get; set; }
        public int End { get; set; }
        public string? FontName { get; set; }
        public DateTimeOffset? Time { get; set; }
        //public int ExtSize { get; set; }
        //public int HdrSize { get; set; }

        #endregion

        #region 拡張ヘッダ

        /// <summary>
        /// ID = x00: 拡張フォント名(可変長)
        /// </summary>
        public string? ExtFontName;

        /// <summary>
        /// ID = x03: 作成者(可変長)
        /// </summary>
        public string? Author;

        /// <summary>
        /// ID = x10: 透明色パレット指定(3バイト固定)
        /// </summary>
        public HashSet<int> TransparentPallets = new();

        /// <summary>
        /// ID = x3f: 拡張フォント名(可変長)
        /// </summary>
        public string? Comment;

        #endregion

        #region Helper properties

        public string Version => $"{Ver >> 4}.{Ver & 0b1111}";
        public bool HasPallet => Col >> 7 == 1;
        public int ColorBits => Col & 0b111_1111;
        public int ColorCount => 1 << (ColorBits + 1);
        #endregion
    }
}
