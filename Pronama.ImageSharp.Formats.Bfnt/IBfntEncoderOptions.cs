using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Pronama.ImageSharp.Formats.Bfnt
{
    internal interface IBfntEncoderOptions
    {
        public ushort Xdots { get; set; }
        public ushort Ydots { get; set; }
        public ushort Start { get; set; }
        public bool IncludesPalette { get; set; }
    }
}
