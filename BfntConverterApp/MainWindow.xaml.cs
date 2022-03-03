using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
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
using Image = SixLabors.ImageSharp.Image;

namespace BfntConverterApp
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow
    {
        internal class ViewModel : INotifyPropertyChanged
        {
            public event PropertyChangedEventHandler? PropertyChanged;

            protected void OnPropertyChanged(string name)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
            }

            private string _statusText = "";
            public string StatusText
            {
                get => _statusText;
                set
                {
                    if (value == _statusText) return;
                    _statusText = value;
                    OnPropertyChanged(nameof(StatusText));
                }
            }
        }

        private readonly ViewModel _viewModel = new();
        private Image? _image;
        private string? _filePath;

        public MainWindow()
        {
            InitializeComponent();
            DataContext = _viewModel;
            SetTitle();
        }

        public MainWindow(IReadOnlyList<string?> args) : this()
        {
            if (args.Count > 0 && System.IO.File.Exists(args[0]))
                Open(args[0]);
        }

        protected override void OnContentRendered(EventArgs e)
        {
            base.OnContentRendered(e);
            ApplyBackgroundEffect();
        }

        private void ApplyBackgroundEffect()
        {
            var windowHandle = new WindowInteropHelper(this).Handle;

            WPFUI.Theme.Manager.Switch(WPFUI.Theme.Style.Dark);
            Background = Brushes.Transparent;
            WPFUI.Background.Manager.Apply(WPFUI.Background.BackgroundType.Mica, windowHandle);
        }

        private void Window_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetData(DataFormats.FileDrop) is not string[] files)
                return;

            Open(files[0]);
        }

        private void MenuItem_OnClick(object sender, RoutedEventArgs e)
        {
            if (sender is not MenuItem item) return;

            var tag = item.Tag as string;
            switch (tag)
            {
                case "close":
                    Close();
                    break;
            }
        }


        private void SetTitle(string? file = null)
        {
            var title = file == null ?
                "BFNT Converter" :
                $"BFNT Converter - {System.IO.Path.GetFileName(file)}";

            TitleBar.Title = title;
            Title = title;
        }

        private void Open(string? file)
        {
            // clear
            SetTitle();
            _viewModel.StatusText = "";
            ZoomImage.Source = null;

            try
            {
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

                // Workaround: Image.Load の format の Metadata は使えないので、Identify の format を使用
                {
                    var imageInfo = Image.Identify(configuration, file, out var format);
                    if (imageInfo == null)
                        return;

                    if (format.Name == "BFNT")
                    {
                        var metadata = imageInfo.Metadata.GetBfntMetadata();
                        _viewModel.StatusText = $"{metadata.ColorCount}色 {metadata.Xdots}x{metadata.Ydots} {metadata.Start}-{metadata.End} パレットデータ{(metadata.HasPallet ? "あり" : "なし")}";
                    }
                    else 
                    {
                        _viewModel.StatusText = $"{imageInfo.Width}x{imageInfo.Height}";
                    }
                }

                var image = Image.Load<Bgra32>(configuration, file, out _);
                if (image == null)
                    return;

                _image = image;
                SetImage(image);

                SetTitle(file);
                _filePath = file;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void SetImage(Image<Bgra32> image)
        {
            // Image to BitmapSource
            if (!image.DangerousTryGetSinglePixelMemory(out var pixel))
                return;

            var bytes = MemoryMarshal.AsBytes(pixel.Span).ToArray();
            var bmp = BitmapSource.Create(image.Width, image.Height, 96, 96, PixelFormats.Bgra32, null,
                bytes, image.Width * 4);

            ZoomBorder.Reset();

            ZoomImage.Source = bmp;
            ZoomImage.Width = bmp.Width;
            ZoomImage.Height = bmp.Height;
        }

        private void Open(object sender, ExecutedRoutedEventArgs e) => OpenFileDialog();

        private void OpenFileDialog()
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "BFNT (*.BFT; *.FNT)|*.BFT;*.FNT|画像タイプ (*.png; *.jpeg)|*.png;*.jpeg|すべてのファイル (*.*)|*.*",
                CheckFileExists = true,
                Multiselect = false
            };

            if (openFileDialog.ShowDialog() != true)
                return;

            Open(openFileDialog.FileName);
        }

        private void Save(object sender, ExecutedRoutedEventArgs e) => SaveFileDialog();

        private void SaveFileDialog()
        {
            var saveFileDialog = new SaveFileDialog
            {
                Filter = "PNG(*.png)|*.png",
                CheckPathExists = true,
                OverwritePrompt = true,
                AddExtension = true,
                FileName = System.IO.Path.GetFileNameWithoutExtension(_filePath) + ".png"
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

                switch (extension.ToUpperInvariant())
                {
                    case ".PNG":
                        _image.SaveAsPng(file);
                        break;
                    default:
                        MessageBox.Show("サポートしていない形式です。");
                        break;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }


        private void Copy(object sender, ExecutedRoutedEventArgs e) => CopyImageToClipboard();
        private void CopyImageToClipboard()
        {
            if (ZoomImage.Source == null)
                return;

            IDataObject data = new DataObject();
            data.SetData(DataFormats.Bitmap, ZoomImage.Source, true);
            Clipboard.SetDataObject(data, true);
        }

        private void CanSave(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = _image != null;
        }

        private void CanCopy(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = _image != null;
        }
    }
}
