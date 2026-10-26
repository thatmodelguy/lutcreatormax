using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace LutCreatorMax.Core;

/// <summary>Color grade parameters applied when baking the working LUT.</summary>
public sealed class GradeModel : INotifyPropertyChanged
{
    private bool _suppress;
    private float _exposure;
    private float _contrast = 1f;
    private float _saturation = 1f;
    private float _temperature;
    private float _tint;
    private float _liftR, _liftG, _liftB;
    private float _gammaR = 1f, _gammaG = 1f, _gammaB = 1f;
    private float _gainR = 1f, _gainG = 1f, _gainB = 1f;
    private float _shadows;
    private float _midtones;
    private float _highlights;
    private float _sCurve;
    private float _curveR, _curveG, _curveB;
    private float _blackCrush;
    private float _highlightRolloff;
    private float _fade;
    private float _splitShadowHue;
    private float _splitShadowSat;
    private float _splitHighlightHue;
    private float _splitHighlightSat;
    private float _splitBalance;
    private float _lookStrength = 1f;
    private bool _invert;
    private float _vignette;
    private float _importStrength = 1f;
    private string _displayName = "My LUT";
    private string _fileName = "z_mylut";

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler? Changed;

    public float Exposure { get => _exposure; set => Set(ref _exposure, value); }
    public float Contrast { get => _contrast; set => Set(ref _contrast, value); }
    public float Saturation { get => _saturation; set => Set(ref _saturation, value); }
    public float Temperature { get => _temperature; set => Set(ref _temperature, value); }
    public float Tint { get => _tint; set => Set(ref _tint, value); }

    public float LiftR { get => _liftR; set => Set(ref _liftR, value); }
    public float LiftG { get => _liftG; set => Set(ref _liftG, value); }
    public float LiftB { get => _liftB; set => Set(ref _liftB, value); }

    public float GammaR { get => _gammaR; set => Set(ref _gammaR, value); }
    public float GammaG { get => _gammaG; set => Set(ref _gammaG, value); }
    public float GammaB { get => _gammaB; set => Set(ref _gammaB, value); }

    public float GainR { get => _gainR; set => Set(ref _gainR, value); }
    public float GainG { get => _gainG; set => Set(ref _gainG, value); }
    public float GainB { get => _gainB; set => Set(ref _gainB, value); }

    public float Shadows { get => _shadows; set => Set(ref _shadows, value); }
    public float Midtones { get => _midtones; set => Set(ref _midtones, value); }
    public float Highlights { get => _highlights; set => Set(ref _highlights, value); }

    /// <summary>Master S-curve amount (-1 soft .. +1 contrasty).</summary>
    public float SCurve { get => _sCurve; set => Set(ref _sCurve, value); }
    public float CurveR { get => _curveR; set => Set(ref _curveR, value); }
    public float CurveG { get => _curveG; set => Set(ref _curveG, value); }
    public float CurveB { get => _curveB; set => Set(ref _curveB, value); }

    /// <summary>Soft crush near black (0..1).</summary>
    public float BlackCrush { get => _blackCrush; set => Set(ref _blackCrush, value); }
    /// <summary>Highlight roll-off / soft knee (0..1).</summary>
    public float HighlightRolloff { get => _highlightRolloff; set => Set(ref _highlightRolloff, value); }
    /// <summary>Lift blacks toward mid gray for a faded look (0..1).</summary>
    public float Fade { get => _fade; set => Set(ref _fade, value); }

    public float SplitShadowHue { get => _splitShadowHue; set => Set(ref _splitShadowHue, value); }
    public float SplitShadowSat { get => _splitShadowSat; set => Set(ref _splitShadowSat, value); }
    public float SplitHighlightHue { get => _splitHighlightHue; set => Set(ref _splitHighlightHue, value); }
    public float SplitHighlightSat { get => _splitHighlightSat; set => Set(ref _splitHighlightSat, value); }
    /// <summary>-1 bias shadows, +1 bias highlights for split balance.</summary>
    public float SplitBalance { get => _splitBalance; set => Set(ref _splitBalance, value); }

    /// <summary>0..1 blend of graded look vs identity.</summary>
    public float LookStrength { get => _lookStrength; set => Set(ref _lookStrength, value); }
    public bool Invert { get => _invert; set => Set(ref _invert, value); }

    /// <summary>Preview-only vignette (not baked into Max LUT).</summary>
    public float Vignette { get => _vignette; set => Set(ref _vignette, value); }

    public float ImportStrength { get => _importStrength; set => Set(ref _importStrength, value); }

    public string DisplayName { get => _displayName; set => Set(ref _displayName, value); }
    public string FileName { get => _fileName; set => Set(ref _fileName, value); }

    public void Reset()
    {
        _suppress = true;
        try
        {
            Exposure = 0f;
            Contrast = 1f;
            Saturation = 1f;
            Temperature = 0f;
            Tint = 0f;
            LiftR = LiftG = LiftB = 0f;
            GammaR = GammaG = GammaB = 1f;
            GainR = GainG = GainB = 1f;
            Shadows = Midtones = Highlights = 0f;
            SCurve = CurveR = CurveG = CurveB = 0f;
            BlackCrush = HighlightRolloff = Fade = 0f;
            SplitShadowHue = SplitShadowSat = 0f;
            SplitHighlightHue = SplitHighlightSat = 0f;
            SplitBalance = 0f;
            LookStrength = 1f;
            Invert = false;
            Vignette = 0f;
            ImportStrength = 1f;
        }
        finally
        {
            _suppress = false;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public void ApplyFrom(GradeSnapshot s, bool includeNames = false)
    {
        _suppress = true;
        try
        {
            Exposure = s.Exposure;
            Contrast = s.Contrast;
            Saturation = s.Saturation;
            Temperature = s.Temperature;
            Tint = s.Tint;
            LiftR = s.LiftR; LiftG = s.LiftG; LiftB = s.LiftB;
            GammaR = s.GammaR; GammaG = s.GammaG; GammaB = s.GammaB;
            GainR = s.GainR; GainG = s.GainG; GainB = s.GainB;
            Shadows = s.Shadows; Midtones = s.Midtones; Highlights = s.Highlights;
            SCurve = s.SCurve; CurveR = s.CurveR; CurveG = s.CurveG; CurveB = s.CurveB;
            BlackCrush = s.BlackCrush; HighlightRolloff = s.HighlightRolloff; Fade = s.Fade;
            SplitShadowHue = s.SplitShadowHue; SplitShadowSat = s.SplitShadowSat;
            SplitHighlightHue = s.SplitHighlightHue; SplitHighlightSat = s.SplitHighlightSat;
            SplitBalance = s.SplitBalance;
            LookStrength = s.LookStrength;
            Invert = s.Invert;
            Vignette = s.Vignette;
            ImportStrength = s.ImportStrength;
            if (includeNames)
            {
                if (!string.IsNullOrWhiteSpace(s.DisplayName)) DisplayName = s.DisplayName;
                if (!string.IsNullOrWhiteSpace(s.FileName)) FileName = s.FileName;
            }
        }
        finally
        {
            _suppress = false;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public GradeSnapshot ToSnapshot() => new()
    {
        Exposure = Exposure,
        Contrast = Contrast,
        Saturation = Saturation,
        Temperature = Temperature,
        Tint = Tint,
        LiftR = LiftR, LiftG = LiftG, LiftB = LiftB,
        GammaR = GammaR, GammaG = GammaG, GammaB = GammaB,
        GainR = GainR, GainG = GainG, GainB = GainB,
        Shadows = Shadows, Midtones = Midtones, Highlights = Highlights,
        SCurve = SCurve, CurveR = CurveR, CurveG = CurveG, CurveB = CurveB,
        BlackCrush = BlackCrush, HighlightRolloff = HighlightRolloff, Fade = Fade,
        SplitShadowHue = SplitShadowHue, SplitShadowSat = SplitShadowSat,
        SplitHighlightHue = SplitHighlightHue, SplitHighlightSat = SplitHighlightSat,
        SplitBalance = SplitBalance,
        LookStrength = LookStrength,
        Invert = Invert,
        Vignette = Vignette,
        ImportStrength = ImportStrength,
        DisplayName = DisplayName,
        FileName = FileName
    };

    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        if (!_suppress)
            Changed?.Invoke(this, EventArgs.Empty);
    }
}

/// <summary>Serializable grade state for presets / settings.</summary>
public sealed class GradeSnapshot
{
    public float Exposure { get; set; }
    public float Contrast { get; set; } = 1f;
    public float Saturation { get; set; } = 1f;
    public float Temperature { get; set; }
    public float Tint { get; set; }
    public float LiftR { get; set; }
    public float LiftG { get; set; }
    public float LiftB { get; set; }
    public float GammaR { get; set; } = 1f;
    public float GammaG { get; set; } = 1f;
    public float GammaB { get; set; } = 1f;
    public float GainR { get; set; } = 1f;
    public float GainG { get; set; } = 1f;
    public float GainB { get; set; } = 1f;
    public float Shadows { get; set; }
    public float Midtones { get; set; }
    public float Highlights { get; set; }
    public float SCurve { get; set; }
    public float CurveR { get; set; }
    public float CurveG { get; set; }
    public float CurveB { get; set; }
    public float BlackCrush { get; set; }
    public float HighlightRolloff { get; set; }
    public float Fade { get; set; }
    public float SplitShadowHue { get; set; }
    public float SplitShadowSat { get; set; }
    public float SplitHighlightHue { get; set; }
    public float SplitHighlightSat { get; set; }
    public float SplitBalance { get; set; }
    public float LookStrength { get; set; } = 1f;
    public bool Invert { get; set; }
    public float Vignette { get; set; }
    public float ImportStrength { get; set; } = 1f;
    public string? DisplayName { get; set; }
    public string? FileName { get; set; }
}

public sealed class LutPreset
{
    public string Name { get; set; } = "Preset";
    public bool BuiltIn { get; set; }
    public GradeSnapshot Grade { get; set; } = new();

    [JsonIgnore]
    public string DisplayLabel => BuiltIn ? $"{Name} (built-in)" : Name;
}
