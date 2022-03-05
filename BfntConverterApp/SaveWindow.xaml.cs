using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Windows;
using Microsoft.Win32;
using Microsoft.WindowsAPICodePack.Dialogs;
using Pronama.ImageSharp.Formats.Bfnt;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.PixelFormats;

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
                if (Xdots == 0 || Ydots == 0)
                {
                    Indivisible = true;
                    return;
                }

                var count = (int)Math.Ceiling((double)Width / Xdots) * (int)Math.Ceiling((double)Height / Ydots);
                End = (ushort)(Start + count - 1);

                Indivisible = Width % Xdots != 0 || Height % Ydots != 0;
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

            public bool Indivisible
            {
                get => _indivisible;
                set => SetProperty(ref _indivisible, value);
            }
            private bool _indivisible;

        }

        private readonly ViewModel _viewModel;
        private readonly Image<Bgra32> _image;
        private readonly string? _filePath;

        //public SaveWindow()
        //{
        //}

        public SaveWindow(Image<Bgra32> image, string? filePath, BfntMetadata? bfntMetadata)
        {
            _image = image;
            _filePath = filePath;

            _viewModel = new ViewModel(_image.Width, _image.Height)
            {
                Xdots = bfntMetadata?.Xdots ?? 16,
                Ydots = bfntMetadata?.Ydots ?? 16,
                Start = bfntMetadata?.Start ?? 0,
                IncludesPalette = bfntMetadata?.HasPalette ?? false
            };
            DataContext = _viewModel;

            WPFUI.Background.Manager.Apply(this);
            InitializeComponent();
        }

        private string? SelectFile()
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

            return saveFileDialog.ShowDialog() != true ? null : saveFileDialog.FileName;
        }

        private static string? SelectFolder()
        {
            var dialog = new CommonOpenFileDialog("フォルダーの選択")
            {
                IsFolderPicker = true
            };
            return dialog.ShowDialog() == CommonFileDialogResult.Ok ? dialog.FileName : null;
        }

        private void SaveImage(string file)
        {
            //try
            //{
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
            //}
            //catch (Exception ex)
            //{
            //    MessageBox.Show(ex.Message);
            //}
        }

        private void SaveImages(string folder)
        {
            try
            {
                var index = (int)_viewModel.SelectedFormat - 1;
                var extension = new[] { ".png", ".webp" }[index];

                IImageEncoder encoder;
                switch (_viewModel.SelectedFormat)
                {
                    case ViewModel.Format.PNG:
                        encoder = new PngEncoder();
                        break;
                    case ViewModel.Format.WebP:
                        encoder = new WebpEncoder();
                        break;

                    case ViewModel.Format.BFNT:
                    default:
                        return;
                }

                var pixelBytes = new byte[_image.Width * _image.Height * Unsafe.SizeOf<Bgra32>()];
                _image.CopyPixelDataTo(pixelBytes);

                var columns = _image.Width / _viewModel.Xdots;

                for (var code = _viewModel.Start; code <= _viewModel.End; code++)
                {
                    var col = code % columns;
                    var row = code / columns;
                    var offset = row * _viewModel.Ydots * _image.Width * 4;

                    // create image
                    var image = new Image<Bgra32>(_viewModel.Xdots, _viewModel.Ydots);
                    image.ProcessPixelRows(accessor =>
                    {
                        for (var y = 0; y < _viewModel.Ydots; y++)
                        {
                            var offsetY = offset + y * _image.Width * 4;
                            var pixelRow = accessor.GetRowSpan(y);

                            for (var x = 0; x < _viewModel.Xdots; x++)
                            {
                                var offsetX = col * _viewModel.Xdots * 4;
                                var i = x * 4 + offsetX + offsetY;

                                pixelRow[x].FromBgra32(new Bgra32(pixelBytes[i + 2], pixelBytes[i + 1], pixelBytes[i], pixelBytes[i + 3]));
                            }
                        }
                    });
                    image.Save(System.IO.Path.Combine(folder, $"{code}{extension}"), encoder);
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



        private void Save(object sender, System.Windows.Input.ExecutedRoutedEventArgs e)
        {
            if (_viewModel.SelectedFormat != ViewModel.Format.BFNT && _viewModel.IsDividedOutput)
            {
                var folder = SelectFolder();
                if (folder != null)
                    SaveImages(folder);
            }
            else
            {
                var file = SelectFile();
                if (file != null)
                    SaveImage(file);
            }
        }

        private void CanSave(object sender, System.Windows.Input.CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = _viewModel.SelectedFormat == ViewModel.Format.BFNT && !_viewModel.Indivisible ||
                 _viewModel.SelectedFormat != ViewModel.Format.BFNT && _viewModel.Indivisible && !_viewModel.IsDividedOutput ||
                 _viewModel.SelectedFormat != ViewModel.Format.BFNT && !_viewModel.Indivisible;
        }
    }
}
