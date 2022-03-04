using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
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
using Configuration = SixLabors.ImageSharp.Configuration;
using Image = SixLabors.ImageSharp.Image;

namespace BfntConverterApp
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        internal class ViewModel : BindableBase
        {
            public string StatusText
            {
                get => _statusText;
                set => SetProperty(ref _statusText, value);
            }
            private string _statusText = "";
        }

        private readonly ViewModel _viewModel = new();
        private Image<Bgra32>? _image;
        private string? _filePath;
        private BfntMetadata? _bfntMetadata;

        public MainWindow()
        {
            InitializeComponent();
            DataContext = _viewModel;
            SetTitle();

#if DEBUG
            Open(@"\\Mac\Home\Downloads\GAJETICN.png");
#endif
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
                case "help":
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "https://pronama.jp/bfnt-converter",
                        UseShellExecute = true
                    });
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

            //try
            //{
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
                        _bfntMetadata = imageInfo.Metadata.GetBfntMetadata();
                        _viewModel.StatusText = $"{_bfntMetadata.ColorCount:#,0}色 ({_bfntMetadata.ColorBits + 1}bit)   {_bfntMetadata.Xdots}x{_bfntMetadata.Ydots}   {_bfntMetadata.Start}-{_bfntMetadata.End}   パレットデータ{(_bfntMetadata.HasPalette ? "あり" : "なし")}";
                    }
                    else
                    {
                        _bfntMetadata = null;
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
            //}
            //catch (Exception ex)
            //{
            //    MessageBox.Show(ex.Message);
            //}
        }

        private void SetImage(Image<Bgra32> image)
        {
            // Image to BitmapSource
            var pixelBytes = new byte[image.Width * image.Height * Unsafe.SizeOf<Bgra32>()];
            image.CopyPixelDataTo(pixelBytes);
            var bmp = BitmapSource.Create(image.Width, image.Height, 96, 96, PixelFormats.Bgra32, null,
                pixelBytes, image.Width * 4);

            ZoomBorder.Reset();
            ZoomImage.Source = bmp;
            ZoomCanvas.Width = bmp.Width;
            ZoomCanvas.Height = bmp.Height;
        }

        private void Open(object sender, ExecutedRoutedEventArgs e) => OpenFileDialog();

        private void OpenFileDialog()
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "BFNT (*.BFT; *.FNT)|*.BFT;*.FNT|画像タイプ (*.png; *.jpg; *.jpeg; *.jpe; *.jfif; *.exif; *.bmp; *.dib; *.rle; *tiff; *.tif; *.gif; *webp)|*.png;*.jpg;*.jpeg;*.jpe;*.jfif;*.exif;*.bmp;*.dib;*.rle;*tiff;*.tif;*.gif;*webp|すべてのファイル (*.*)|*.*",
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
            if (_image == null) return;

            var saveWindow = new SaveWindow(_image, _filePath, _bfntMetadata)
            {
                Owner = this
            };
            saveWindow.ShowDialog();
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

        private void Paste(object sender, ExecutedRoutedEventArgs e)
        {
            try
            {
                var bitmapSource = Clipboard.GetImage();
                if (bitmapSource == null)
                {
                    return;
                }
                var pixels = new byte[(int)bitmapSource.Width * (int)bitmapSource.Height * 4];

                // BitmapSource から配列にコピー
                var stride = ((int)bitmapSource.Width * bitmapSource.Format.BitsPerPixel + 7) / 8;
                bitmapSource.CopyPixels(pixels, stride, 0);
                var image = Image.LoadPixelData<Bgra32>(pixels, (int)bitmapSource.Width, (int)bitmapSource.Height);


                _viewModel.StatusText = $"{(int)bitmapSource.Width}x{(int)bitmapSource.Height}";

                _image = image;
                SetImage(image);

                SetTitle();
                _filePath = null;
                _bfntMetadata = null;
            }
            catch (Exception ex)
            {
                MessageBox.Show(messageBoxText: ex.Message);
            }

        }
    }
}
