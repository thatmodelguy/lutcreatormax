using System.Drawing;
using System.Drawing.Imaging;
using LutCreatorMax.Core;

namespace LutCreatorMax.Services;

public enum PreviewMode
{
    After,
    Before,
    Split
}

public static class PreviewService
{
    public static Bitmap ApplyLut(
        Bitmap source,
        Lut3D lut,
        PreviewMode mode,
        float vignette = 0f,
        float splitX = 0.5f)
    {
        var result = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(result))
            g.DrawImage(source, 0, 0, source.Width, source.Height);

        var rect = new Rectangle(0, 0, result.Width, result.Height);
        BitmapData data = result.LockBits(rect, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
        try
        {
            unsafe
            {
                byte* scan0 = (byte*)data.Scan0;
                int stride = data.Stride;
                int splitCol = (int)(result.Width * splitX);
                float vig = Math.Clamp(vignette, 0f, 1f);
                float cx = (result.Width - 1) * 0.5f;
                float cy = (result.Height - 1) * 0.5f;
                float maxDist = MathF.Sqrt(cx * cx + cy * cy);

                for (int y = 0; y < result.Height; y++)
                {
                    byte* row = scan0 + y * stride;
                    for (int x = 0; x < result.Width; x++)
                    {
                        bool useLut = mode switch
                        {
                            PreviewMode.Before => false,
                            PreviewMode.After => true,
                            PreviewMode.Split => x < splitCol,
                            _ => true
                        };
                        if (!useLut)
                            continue;

                        int i = x * 4;
                        float b = row[i] / 255f;
                        float gch = row[i + 1] / 255f;
                        float r = row[i + 2] / 255f;
                        var mapped = lut.Sample(r, gch, b);

                        if (vig > 1e-5f)
                        {
                            float dx = x - cx;
                            float dy = y - cy;
                            float dist = MathF.Sqrt(dx * dx + dy * dy) / maxDist;
                            float factor = 1f - vig * SmoothStep(0.35f, 1f, dist);
                            mapped = new Rgb(mapped.R * factor, mapped.G * factor, mapped.B * factor);
                        }

                        row[i] = (byte)Math.Clamp((int)MathF.Round(mapped.B * 255f), 0, 255);
                        row[i + 1] = (byte)Math.Clamp((int)MathF.Round(mapped.G * 255f), 0, 255);
                        row[i + 2] = (byte)Math.Clamp((int)MathF.Round(mapped.R * 255f), 0, 255);
                    }
                }
            }
        }
        finally
        {
            result.UnlockBits(data);
        }

        return result;
    }

    private static float SmoothStep(float edge0, float edge1, float x)
    {
        float t = Math.Clamp((x - edge0) / (edge1 - edge0), 0f, 1f);
        return t * t * (3f - 2f * t);
    }
}
