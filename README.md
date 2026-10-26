# LUT Creator for GameGuru MAX

Windows WPF (.NET 8) tool to design color-grade LUTs, preview them on any image, and export in the format GameGuru MAX expects. Uses `System.Drawing` for image I/O on Windows.

## Max format

Max loads LUTs from `Files\editors\lut`:

| Asset | Spec |
| --- | --- |
| LUT texture | RGB PNG **256×16** (Unreal horizontal strip) |
| Thumbnail | `thumbnails\FilterNN.jpg` **128×128** |
| Names | `filternames.txt` — line 0 is `None`, then one name per PNG |
| Order | PNG files sorted alphabetically → `Filter01`…`FilterN` |

Default path:

`C:\Program Files (x86)\Steam\steamapps\common\GameGuru MAX\Files\editors\lut`

## Run

```powershell
dotnet run --project src\LutCreatorMax\LutCreatorMax.csproj
```

Requires .NET 8 SDK (or newer with `net8.0-windows` targeting pack).

## Workflow

1. **Open Image** — any PNG/JPG from your project or elsewhere (swap indoor/outdoor refs anytime).
2. Pick a **preset** or adjust grade sliders; or **Import Photoshop / LUT…** — Photoshop/Adobe `.CUBE` (any size, e.g. 32³) is resampled to Max’s 16³ strip, or load an Unreal 256×16 PNG.
3. Preview with After / Before / Split; the live strip shows the baked 256×16 LUT.
4. Set **Display name** and a sort-friendly **File name** (prefer `z_…` so it appends at the end of Max’s alphabetical list).
5. **Export Package…** writes PNG + thumbnail + a `filternames_append.txt` snippet, or **Install to Max…** appends into the Max folder (may need admin rights under Program Files).

## Grade tools

- Basic: exposure, contrast, saturation, temperature/tint, lift/gamma/gain
- Curves: shadows/mids/highs, master S-curve, per-channel curve bias
- Crush / fade / highlight roll-off
- Split toning (shadow/highlight hue + sat + balance)
- Look strength + invert
- Vignette is **preview-only** (spatial; cannot bake into Max’s color LUT)

## Presets & settings

Built-ins: Identity, Warm Sunset, Cold Night, Teal Orange, Bleach Bypass, Faded Film, Dramatic BW. Save your own as JSON under `%AppData%\LutCreatorMax\Presets\`.

Remembers Max path, last image/import/export folders, file-name prefix, and preview mode in `%AppData%\LutCreatorMax\settings.json`.

## Notes

- **Install to Max** auto-picks an append-safe filename (`zz_NNN_name.png`) when needed so new LUTs always sort last and Filter indices stay aligned with `filternames.txt`.
- If Program Files is not writable, use Export Package and copy the files manually (or run elevated).
