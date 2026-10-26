namespace LutCreatorMax.Core;

/// <summary>
/// 16x16x16 RGB LUT used by GameGuru MAX / Unreal-style strip textures.
/// Indexed as [b * Size * Size + g * Size + r].
/// </summary>
public sealed class Lut3D
{
    public const int Size = 16;
    public const int EntryCount = Size * Size * Size;

    private readonly Rgb[] _data;

    public Lut3D()
    {
        _data = new Rgb[EntryCount];
        FillIdentity();
    }

    public Lut3D(Rgb[] data)
    {
        if (data.Length != EntryCount)
            throw new ArgumentException($"LUT must contain {EntryCount} entries.", nameof(data));
        _data = (Rgb[])data.Clone();
    }

    public Rgb this[int r, int g, int b]
    {
        get => _data[Index(r, g, b)];
        set => _data[Index(r, g, b)] = value;
    }

    public Rgb[] Data => _data;

    public static int Index(int r, int g, int b) => b * Size * Size + g * Size + r;

    public void FillIdentity()
    {
        float scale = 1f / (Size - 1);
        for (int b = 0; b < Size; b++)
        for (int g = 0; g < Size; g++)
        for (int r = 0; r < Size; r++)
            this[r, g, b] = new Rgb(r * scale, g * scale, b * scale);
    }

    public Lut3D Clone() => new(_data);

    /// <summary>Trilinear sample. Input RGB in 0..1.</summary>
    public Rgb Sample(float r, float g, float b)
    {
        r = Math.Clamp(r, 0f, 1f);
        g = Math.Clamp(g, 0f, 1f);
        b = Math.Clamp(b, 0f, 1f);

        float rf = r * (Size - 1);
        float gf = g * (Size - 1);
        float bf = b * (Size - 1);

        int r0 = (int)MathF.Floor(rf);
        int g0 = (int)MathF.Floor(gf);
        int b0 = (int)MathF.Floor(bf);
        int r1 = Math.Min(r0 + 1, Size - 1);
        int g1 = Math.Min(g0 + 1, Size - 1);
        int b1 = Math.Min(b0 + 1, Size - 1);

        float tr = rf - r0;
        float tg = gf - g0;
        float tb = bf - b0;

        Rgb c000 = this[r0, g0, b0];
        Rgb c100 = this[r1, g0, b0];
        Rgb c010 = this[r0, g1, b0];
        Rgb c110 = this[r1, g1, b0];
        Rgb c001 = this[r0, g0, b1];
        Rgb c101 = this[r1, g0, b1];
        Rgb c011 = this[r0, g1, b1];
        Rgb c111 = this[r1, g1, b1];

        Rgb c00 = Rgb.Lerp(c000, c100, tr);
        Rgb c10 = Rgb.Lerp(c010, c110, tr);
        Rgb c01 = Rgb.Lerp(c001, c101, tr);
        Rgb c11 = Rgb.Lerp(c011, c111, tr);

        Rgb c0 = Rgb.Lerp(c00, c10, tg);
        Rgb c1 = Rgb.Lerp(c01, c11, tg);
        return Rgb.Lerp(c0, c1, tb);
    }

    /// <summary>Blend toward identity by (1 - strength). Strength 1 = full LUT.</summary>
    public Lut3D BlendWithIdentity(float strength)
    {
        strength = Math.Clamp(strength, 0f, 1f);
        var result = new Lut3D();
        float scale = 1f / (Size - 1);
        for (int b = 0; b < Size; b++)
        for (int g = 0; g < Size; g++)
        for (int r = 0; r < Size; r++)
        {
            var id = new Rgb(r * scale, g * scale, b * scale);
            result[r, g, b] = Rgb.Lerp(id, this[r, g, b], strength);
        }
        return result;
    }
}

public readonly struct Rgb(float r, float g, float b)
{
    public float R { get; } = r;
    public float G { get; } = g;
    public float B { get; } = b;

    public static Rgb Lerp(Rgb a, Rgb b, float t) =>
        new(
            a.R + (b.R - a.R) * t,
            a.G + (b.G - a.G) * t,
            a.B + (b.B - a.B) * t);

    public Rgb Clamp01() =>
        new(Math.Clamp(R, 0f, 1f), Math.Clamp(G, 0f, 1f), Math.Clamp(B, 0f, 1f));
}
