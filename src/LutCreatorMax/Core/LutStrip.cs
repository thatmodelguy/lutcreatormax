using System.Drawing;
using System.Drawing.Imaging;

namespace LutCreatorMax.Core;

/// <summary>
/// Unreal / GameGuru MAX horizontal strip: 256x16.
/// For blue slice b in 0..15, the 16x16 RG tile is at x = b*16; R on X, G on Y within the tile.
/// </summary>
public static class LutStrip
{
    public const int Width = 256;
    public const int Height = 16;
    public const int TileSize = 16;

    public static Lut3D FromBitmap(Bitmap image)
    {
        if (image.Width != Width || image.Height != Height)
            throw new ArgumentException($"LUT strip must be {Width}x{Height}, got {image.Width}x{image.Height}.");

        var lut = new Lut3D();
        for (int b = 0; b < TileSize; b++)
        for (int g = 0; g < TileSize; g++)
        for (int r = 0; r < TileSize; r++)
        {
            int x = b * TileSize + r;
            int y = g;
            Color px = image.GetPixel(x, y);
            lut[r, g, b] = new Rgb(px.R / 255f, px.G / 255f, px.B / 255f);
        }
        return lut;
    }

    public static Lut3D FromFile(string path)
    {
        using var image = new Bitmap(path);
        return FromBitmap(image);
    }

    public static Bitmap ToBitmap(Lut3D lut)
    {
        var image = new Bitmap(Width, Height, PixelFormat.Format24bppRgb);
        for (int b = 0; b < TileSize; b++)
        for (int g = 0; g < TileSize; g++)
        for (int r = 0; r < TileSize; r++)
        {
            var c = lut[r, g, b].Clamp01();
            int x = b * TileSize + r;
            int y = g;
            image.SetPixel(x, y, Color.FromArgb(
                (byte)Math.Clamp((int)MathF.Round(c.R * 255f), 0, 255),
                (byte)Math.Clamp((int)MathF.Round(c.G * 255f), 0, 255),
                (byte)Math.Clamp((int)MathF.Round(c.B * 255f), 0, 255)));
        }
        return image;
    }

    public static void SavePng(Lut3D lut, string path)
    {
        using var image = ToBitmap(lut);
        image.Save(path, ImageFormat.Png);
    }
}
