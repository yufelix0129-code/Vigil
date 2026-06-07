using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using Forms = System.Windows.Forms;
using DrawingEncoder = System.Drawing.Imaging.Encoder;

namespace VigilWin.Services;

public sealed class ScreenCaptureService
{
    private const int MaxWidth = 1280;
    private const long JpegQuality = 70L;

    public Task<byte[]> CapturePrimaryScreenJpegAsync()
    {
        return Task.Run(() =>
        {
            try
            {
                var screen = Forms.Screen.PrimaryScreen
                    ?? throw new InvalidOperationException("无法获取主屏幕信息。");
                var bounds = screen.Bounds;

                using var fullSizeBitmap = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format24bppRgb);
                using (var graphics = Graphics.FromImage(fullSizeBitmap))
                {
                    graphics.CopyFromScreen(bounds.Left, bounds.Top, 0, 0, bounds.Size);
                }

                using var outputBitmap = ResizeIfNeeded(fullSizeBitmap);
                using var stream = new MemoryStream();
                var jpegCodec = ImageCodecInfo.GetImageEncoders()
                    .FirstOrDefault(codec => codec.FormatID == ImageFormat.Jpeg.Guid)
                    ?? throw new InvalidOperationException("当前系统未找到 JPEG 编码器。");

                using var encoderParameters = new EncoderParameters(1);
                encoderParameters.Param[0] = new EncoderParameter(DrawingEncoder.Quality, JpegQuality);
                outputBitmap.Save(stream, jpegCodec, encoderParameters);

                return stream.ToArray();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"截屏失败：{ex.Message}", ex);
            }
        });
    }

    private static Bitmap ResizeIfNeeded(Bitmap source)
    {
        if (source.Width <= MaxWidth)
        {
            return new Bitmap(source);
        }

        var newWidth = MaxWidth;
        var newHeight = (int)Math.Round(source.Height * (MaxWidth / (double)source.Width));
        var resized = new Bitmap(newWidth, newHeight, PixelFormat.Format24bppRgb);

        using var graphics = Graphics.FromImage(resized);
        graphics.CompositingQuality = CompositingQuality.HighQuality;
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.SmoothingMode = SmoothingMode.HighQuality;
        graphics.DrawImage(source, 0, 0, newWidth, newHeight);

        return resized;
    }
}
