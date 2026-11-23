using AutoCapture.Wpf.Models;
using System.IO;
using System.Windows.Media.Imaging;
using Bitmap = System.Drawing.Bitmap;
using Graphics = System.Drawing.Graphics;
using ImageFormat = System.Drawing.Imaging.ImageFormat;
using InterpolationMode = System.Drawing.Drawing2D.InterpolationMode;

namespace AutoCapture.Wpf.Utilities
{
    /// <summary>화면의 지정 영역을 작은 미리보기 이미지로 만든다.</summary>
    internal static class ScreenSnapshot
    {
        public static BitmapSource? Capture(CaptureArea area, int maxWidth = 320)
        {
            if (!area.IsValid) return null;

            using var full = new Bitmap(area.Width, area.Height);
            using (var graphics = Graphics.FromImage(full))
            {
                graphics.CopyFromScreen(area.X, area.Y, 0, 0, full.Size);
            }

            var scale = Math.Min(1.0, (double)maxWidth / area.Width);
            var width = Math.Max(1, (int)Math.Round(area.Width * scale));
            var height = Math.Max(1, (int)Math.Round(area.Height * scale));
            using var small = new Bitmap(width, height);
            using (var graphics = Graphics.FromImage(small))
            {
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.DrawImage(full, 0, 0, width, height);
            }

            using var stream = new MemoryStream();
            small.Save(stream, ImageFormat.Png);
            stream.Position = 0;
            return Load(stream);
        }

        /// <summary>파일을 잠그지 않고 축소해 읽는다. 실패하면 null.</summary>
        public static BitmapSource? LoadThumbnail(string path, int decodeWidth = 96)
        {
            try
            {
                using var stream = File.OpenRead(path);
                return Load(stream, decodeWidth);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
            {
                return null;
            }
        }

        private static BitmapSource Load(Stream stream, int decodeWidth = 0)
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            if (decodeWidth > 0) image.DecodePixelWidth = decodeWidth;
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();
            return image;
        }
    }
}
