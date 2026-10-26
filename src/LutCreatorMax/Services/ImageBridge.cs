using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace LutCreatorMax.Services;

public static class ImageBridge
{
    public static Bitmap Load(string path) => new(path);

    public static Bitmap DownscaleForPreview(Bitmap source, int maxEdge = 1280)
    {
        int w = source.Width;
        int h = source.Height;
        int max = Math.Max(w, h);
        if (max <= maxEdge)
            return (Bitmap)source.Clone();

        float scale = maxEdge / (float)max;
        int nw = Math.Max(1, (int)MathF.Round(w * scale));
        int nh = Math.Max(1, (int)MathF.Round(h * scale));
        var result = new Bitmap(nw, nh, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(result);
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.DrawImage(source, 0, 0, nw, nh);
        return result;
    }

    public static WriteableBitmap ToWriteableBitmap(Bitmap image)
    {
        using var ms = new MemoryStream();
        image.Save(ms, ImageFormat.Png);
        ms.Position = 0;
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.StreamSource = ms;
        bitmap.EndInit();
        bitmap.Freeze();

        var converted = new FormatConvertedBitmap(bitmap, PixelFormats.Bgra32, null, 0);
        converted.Freeze();
        var writeable = new WriteableBitmap(converted);
        writeable.Freeze();
        return writeable;
    }

    public static Bitmap ResizeCrop(Bitmap source, int width, int height)
    {
        var result = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
        float scale = Math.Max(width / (float)source.Width, height / (float)source.Height);
        int sw = (int)MathF.Round(source.Width * scale);
        int sh = (int)MathF.Round(source.Height * scale);
        int ox = (width - sw) / 2;
        int oy = (height - sh) / 2;
        using var g = Graphics.FromImage(result);
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.Clear(System.Drawing.Color.Black);
        g.DrawImage(source, ox, oy, sw, sh);
        return result;
    }
}
