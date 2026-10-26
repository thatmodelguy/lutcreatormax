namespace LutCreatorMax.Core;

/// <summary>Bakes grade controls (and optional imported LUT) into a 16^3 working LUT.</summary>
public static class GradePipeline
{
    public static Lut3D Bake(GradeModel grade, Lut3D? importedBase)
    {
        Lut3D baseLut = importedBase?.BlendWithIdentity(grade.ImportStrength) ?? new Lut3D();
        var result = new Lut3D();
        float scale = 1f / (Lut3D.Size - 1);
        float strength = Math.Clamp(grade.LookStrength, 0f, 1f);

        for (int b = 0; b < Lut3D.Size; b++)
        for (int g = 0; g < Lut3D.Size; g++)
        for (int r = 0; r < Lut3D.Size; r++)
        {
            var identity = new Rgb(r * scale, g * scale, b * scale);
            Rgb c = baseLut.Sample(identity.R, identity.G, identity.B);
            c = ApplyGrade(c, grade);
            if (strength < 1f)
                c = Rgb.Lerp(identity, c, strength);
            result[r, g, b] = c.Clamp01();
        }

        return result;
    }

    public static Rgb ApplyGrade(Rgb c, GradeModel g)
    {
        float exp = MathF.Pow(2f, g.Exposure);
        c = new Rgb(c.R * exp, c.G * exp, c.B * exp);

        c = new Rgb(
            (c.R - 0.5f) * g.Contrast + 0.5f,
            (c.G - 0.5f) * g.Contrast + 0.5f,
            (c.B - 0.5f) * g.Contrast + 0.5f);

        c = new Rgb(
            c.R + g.Temperature * 0.15f,
            c.G + g.Tint * 0.1f,
            c.B - g.Temperature * 0.15f);

        c = new Rgb(
            ApplyCdl(c.R, g.LiftR, g.GammaR, g.GainR),
            ApplyCdl(c.G, g.LiftG, g.GammaG, g.GainG),
            ApplyCdl(c.B, g.LiftB, g.GammaB, g.GainB));

        float luma = Luma(c);
        float curve = ApplyToneCurve(luma, g.Shadows, g.Midtones, g.Highlights);
        if (luma > 1e-6f)
        {
            float ratio = curve / luma;
            c = new Rgb(c.R * ratio, c.G * ratio, c.B * ratio);
        }

        c = new Rgb(
            ApplySCurve(c.R, g.SCurve + g.CurveR),
            ApplySCurve(c.G, g.SCurve + g.CurveG),
            ApplySCurve(c.B, g.SCurve + g.CurveB));

        c = ApplyBlackCrush(c, g.BlackCrush);
        c = ApplyHighlightRolloff(c, g.HighlightRolloff);
        c = ApplyFade(c, g.Fade);
        c = ApplySplitTone(c, g);

        luma = Luma(c);
        c = new Rgb(
            luma + (c.R - luma) * g.Saturation,
            luma + (c.G - luma) * g.Saturation,
            luma + (c.B - luma) * g.Saturation);

        if (g.Invert)
            c = new Rgb(1f - c.R, 1f - c.G, 1f - c.B);

        return c.Clamp01();
    }

    private static float Luma(Rgb c) => 0.2126f * c.R + 0.7152f * c.G + 0.0722f * c.B;

    private static float ApplyCdl(float x, float lift, float gamma, float gain)
    {
        x = (x + lift) * gain;
        x = Math.Max(x, 0f);
        if (MathF.Abs(gamma - 1f) > 1e-5f)
            x = MathF.Pow(x, 1f / Math.Max(gamma, 0.01f));
        return x;
    }

    private static float ApplyToneCurve(float x, float shadows, float midtones, float highlights)
    {
        float sMask = 1f - SmoothStep(0f, 0.5f, x);
        float mMask = Math.Clamp(1f - MathF.Abs(x - 0.5f) * 2f, 0f, 1f);
        float hMask = SmoothStep(0.5f, 1f, x);
        float delta = shadows * 0.35f * sMask + midtones * 0.25f * mMask + highlights * 0.35f * hMask;
        return Math.Clamp(x + delta, 0f, 1f);
    }

    /// <summary>amount -1..1; positive = contrasty S, negative = opposite.</summary>
    private static float ApplySCurve(float x, float amount)
    {
        amount = Math.Clamp(amount, -1f, 1f);
        if (MathF.Abs(amount) < 1e-5f)
            return x;
        x = Math.Clamp(x, 0f, 1f);
        // Smooth contrast curve around 0.5
        float contrast = 1f + amount * 1.5f;
        float y = (x - 0.5f) * contrast + 0.5f;
        // Soft saturate toward S-shape with power
        float t = Math.Clamp(y, 0f, 1f);
        if (amount > 0)
        {
            float shaped = t * t * (3f - 2f * t);
            return Math.Clamp(Lerp(t, shaped, amount), 0f, 1f);
        }
        // Soften: blend toward linear-from-ends
        float soft = 0.5f + 0.5f * MathF.Sin((t - 0.5f) * MathF.PI);
        return Math.Clamp(Lerp(t, soft, -amount * 0.5f), 0f, 1f);
    }

    private static Rgb ApplyBlackCrush(Rgb c, float amount)
    {
        amount = Math.Clamp(amount, 0f, 1f);
        if (amount < 1e-5f) return c;
        float threshold = amount * 0.25f;
        float Crush(float v)
        {
            if (v >= threshold) return v;
            float t = v / Math.Max(threshold, 1e-5f);
            return v * t; // quadratic pull toward black
        }
        return new Rgb(Crush(c.R), Crush(c.G), Crush(c.B));
    }

    private static Rgb ApplyHighlightRolloff(Rgb c, float amount)
    {
        amount = Math.Clamp(amount, 0f, 1f);
        if (amount < 1e-5f) return c;
        float knee = 1f - amount * 0.45f;
        float Soft(float v)
        {
            if (v <= knee) return v;
            float t = (v - knee) / Math.Max(1f - knee, 1e-5f);
            float rolled = knee + (1f - knee) * (1f - (1f - t) * (1f - t));
            return Lerp(v, Math.Min(rolled, 1f), amount);
        }
        return new Rgb(Soft(c.R), Soft(c.G), Soft(c.B));
    }

    private static Rgb ApplyFade(Rgb c, float amount)
    {
        amount = Math.Clamp(amount, 0f, 1f);
        if (amount < 1e-5f) return c;
        // Lift floor toward gray, compress top slightly
        float floor = amount * 0.18f;
        float ceil = 1f - amount * 0.08f;
        return new Rgb(
            floor + c.R * (ceil - floor),
            floor + c.G * (ceil - floor),
            floor + c.B * (ceil - floor));
    }

    private static Rgb ApplySplitTone(Rgb c, GradeModel g)
    {
        if (MathF.Abs(g.SplitShadowSat) < 1e-5f && MathF.Abs(g.SplitHighlightSat) < 1e-5f)
            return c;

        float luma = Luma(c);
        float bal = Math.Clamp(g.SplitBalance, -1f, 1f);
        float shadowMask = 1f - SmoothStep(0.15f + bal * 0.1f, 0.55f + bal * 0.1f, luma);
        float highlightMask = SmoothStep(0.45f + bal * 0.1f, 0.85f + bal * 0.1f, luma);

        Rgb shadowTint = HueToRgb(g.SplitShadowHue);
        Rgb highlightTint = HueToRgb(g.SplitHighlightHue);

        float ss = Math.Clamp(g.SplitShadowSat, 0f, 1f) * shadowMask;
        float hs = Math.Clamp(g.SplitHighlightSat, 0f, 1f) * highlightMask;

        c = new Rgb(
            c.R + (shadowTint.R - 0.5f) * 2f * ss * 0.35f,
            c.G + (shadowTint.G - 0.5f) * 2f * ss * 0.35f,
            c.B + (shadowTint.B - 0.5f) * 2f * ss * 0.35f);
        c = new Rgb(
            c.R + (highlightTint.R - 0.5f) * 2f * hs * 0.35f,
            c.G + (highlightTint.G - 0.5f) * 2f * hs * 0.35f,
            c.B + (highlightTint.B - 0.5f) * 2f * hs * 0.35f);
        return c;
    }

    /// <summary>Hue in degrees 0..360 → vivid RGB tint.</summary>
    private static Rgb HueToRgb(float hueDegrees)
    {
        float h = ((hueDegrees % 360f) + 360f) % 360f / 60f;
        float x = 1f - MathF.Abs(h % 2f - 1f);
        float r = 0, g = 0, b = 0;
        if (h < 1) { r = 1; g = x; }
        else if (h < 2) { r = x; g = 1; }
        else if (h < 3) { g = 1; b = x; }
        else if (h < 4) { g = x; b = 1; }
        else if (h < 5) { r = x; b = 1; }
        else { r = 1; b = x; }
        return new Rgb(r, g, b);
    }

    private static float SmoothStep(float edge0, float edge1, float x)
    {
        float t = Math.Clamp((x - edge0) / (edge1 - edge0), 0f, 1f);
        return t * t * (3f - 2f * t);
    }

    private static float Lerp(float a, float b, float t) => a + (b - a) * t;
}
