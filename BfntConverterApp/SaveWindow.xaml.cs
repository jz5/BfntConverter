using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup.Localizer;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Microsoft.Win32;
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
using SixLabors.ImageSharp.PixelFormats;
using Configuration = SixLabors.ImageSharp.Configuration;
using Image = SixLabors.ImageSharp.Image;

namespace BfntConverterApp
{
    /// <summary>
    /// SaveWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class SaveWindow : Window
    {
        internal class ViewModel : BindableBase
        {
            public int Width { get; set; }
            public int Height { get; set; }

            public enum Format
            {
                BFNT,
                PNG,
                WebP
            }

            public ViewModel(int width, int height)
            {
                foreach (Format e in Enum.GetValues(typeof(Format)))
                    Formats.Add(e, e.ToString());

                Width = width;
                Height = height;
                Calc();
            }

            public Dictionary<Format, string> Formats { get; } = new();

            public Format SelectedFormat
            {
                get => _selectedFormat;
                set => SetProperty(ref _selectedFormat, value);
            }
            private Format _selectedFormat = Format.BFNT;

            public ushort Xdots
            {
                get => _xdots;
                set
                {
                    if (SetProperty(ref _xdots, value))
                        Calc();
                }
            }
            private ushort _xdots = 16;

            public ushort Ydots
            {
                get => _ydots;
                set
                {
                    if (SetProperty(ref _ydots, value))
                        Calc();
                }
            }
            private ushort _ydots = 16;

            public ushort Start
            {
                get => _start;
                set
                {
                    if (SetProperty(ref _start, value))
                        Calc();
                }
            }
            private ushort _start;

            public ushort End
            {
                get => _end;
                set => SetProperty(ref _end, value);
            }
            private ushort _end;

            private void Calc()
            {
                var count = (int)Math.Ceiling((double)Width / Xdots) * (int)Math.Ceiling((double)Height / Ydots);
                End = (ushort)(Start + count - 1);
            }

            public bool IncludesPalette
            {
                get => _includesPalette;
                set => SetProperty(ref _includesPalette, value);
            }
            private bool _includesPalette;

            public bool IsDividedOutput
            {
                get => _isDividedOutput;
                set => SetProperty(ref _isDividedOutput, value);
            }
            private bool _isDividedOutput;
        }

        private readonly ViewModel _viewModel;

        private readonly Image _image;
        private readonly string? _filePath;
        private readonly BfntMetadata? _bfntMetadata;

        public SaveWindow()
        {
            WPFUI.Background.Manager.Apply(this);
            InitializeComponent();
        }

        public SaveWindow(Image image, string? filePath, BfntMetadata? bfntMetadata) : this()
        {
            _image = image;
            _filePath = filePath;
            _bfntMetadata = bfntMetadata;

            _viewModel = new ViewModel(_image.Width, _image.Height)
            {
                Xdots = _bfntMetadata?.Xdots ?? 16,
                Ydots = _bfntMetadata?.Ydots ?? 16,
                Start = _bfntMetadata?.Start ?? 0,
                IncludesPalette = _bfntMetadata?.HasPalette ?? false
            };
            DataContext = _viewModel;
        }

        private void SaveFileDialog(object sender, RoutedEventArgs e)
        {
            var index = (int)_viewModel.SelectedFormat;
            var extension = new[] { ".bft", ".png", ".webp" }[index];
            var filter = new[]
                { "BFNT (*.BFT; *.FNT)|*.BFT;*.FNT", "PNG (*.png)|*.png", "WebP (*.webp)|*.webp" }[index];

            var saveFileDialog = new SaveFileDialog
            {
                Filter = filter,
                CheckPathExists = true,
                OverwritePrompt = true,
                AddExtension = true,
                FileName = System.IO.Path.GetFileNameWithoutExtension(_filePath) + extension
            };

            if (saveFileDialog.ShowDialog() != true)
                return;

            Save(saveFileDialog.FileName);
        }

        private void Save(string file)
        {
            try
            {
                var extension = System.IO.Path.GetExtension(file);

                switch (_viewModel.SelectedFormat)
                {
                    case ViewModel.Format.BFNT:
                        _image.Save(file, new BfntEncoder
                        {
                            Xdots = _viewModel.Xdots,
                            Ydots = _viewModel.Ydots,
                            Start = _viewModel.Start,
                            IncludesPalette = _viewModel.IncludesPalette
                        });
                        break;

                    case ViewModel.Format.PNG:
                        _image.SaveAsPng(file);
                        break;
                    case ViewModel.Format.WebP:
                        _image.SaveAsWebp(file);
                        break;
                    default:
                        break;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }


        private void CompleteButton_OnClick(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

        private void CancelButton_OnClick(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

    }
}
