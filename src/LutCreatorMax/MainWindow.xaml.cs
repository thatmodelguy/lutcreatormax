using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using LutCreatorMax.Core;
using LutCreatorMax.Services;
using Microsoft.Win32;

namespace LutCreatorMax;

public partial class MainWindow : Window
{
    private readonly GradeModel _grade = new();
    private readonly DispatcherTimer _debounce = new() { Interval = TimeSpan.FromMilliseconds(33) };
    private AppSettings _settings = new();
    private bool _loadingUi;
    private bool _applyingPreset;

    private Bitmap? _sourceFull;
    private Bitmap? _sourcePreview;
    private Lut3D? _importedLut;
    private Lut3D _workingLut = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _grade;
        _settings = AppSettingsStore.Load();
        ApplySettingsToUi();

        _grade.Changed += (_, _) => ScheduleRefresh();
        _debounce.Tick += (_, _) =>
        {
            _debounce.Stop();
            RefreshPreview();
        };

        ReloadPresets(_settings.LastPresetName);
        RefreshCatalogStatus();
        RefreshPreview();
    }

    private PreviewMode CurrentMode =>
        ModeBefore.IsChecked == true ? PreviewMode.Before
        : ModeSplit.IsChecked == true ? PreviewMode.Split
        : PreviewMode.After;

    private void ApplySettingsToUi()
    {
        _loadingUi = true;
        try
        {
            MaxPathBox.Text = string.IsNullOrWhiteSpace(_settings.MaxLutPath)
                ? MaxExportService.DefaultMaxLutPath
                : _settings.MaxLutPath;
            FilePrefixBox.Text = string.IsNullOrWhiteSpace(_settings.DefaultFileNamePrefix)
                ? "z_"
                : _settings.DefaultFileNamePrefix;

            if (!_grade.FileName.StartsWith(FilePrefixBox.Text, StringComparison.OrdinalIgnoreCase)
                && _grade.FileName == "z_mylut")
            {
                _grade.FileName = FilePrefixBox.Text.TrimEnd('_') is { Length: > 0 } p
                    ? $"{FilePrefixBox.Text}mylut"
                    : "z_mylut";
            }

            switch (_settings.PreviewMode)
            {
                case "Before":
                    ModeBefore.IsChecked = true;
                    break;
                case "Split":
                    ModeSplit.IsChecked = true;
                    break;
                default:
                    ModeAfter.IsChecked = true;
                    break;
            }
        }
        finally
        {
            _loadingUi = false;
        }
    }

    private void CaptureSettingsFromUi()
    {
        _settings.MaxLutPath = MaxPathBox.Text.Trim();
        _settings.DefaultFileNamePrefix = FilePrefixBox.Text.Trim();
        _settings.PreviewMode = CurrentMode.ToString();
        if (PresetCombo.SelectedItem is LutPreset preset)
            _settings.LastPresetName = preset.Name;
    }

    private void ScheduleRefresh()
    {
        _debounce.Stop();
        _debounce.Start();
    }

    private void PreviewMode_Changed(object sender, RoutedEventArgs e)
    {
        if (_loadingUi) return;
        ScheduleRefresh();
    }

    private void MaxPathBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_loadingUi) return;
        RefreshCatalogStatus();
    }

    private void SettingsField_Changed(object sender, TextChangedEventArgs e)
    {
        // prefix edits are captured on close / export
    }

    private void RefreshCatalogStatus()
    {
        try
        {
            string path = MaxPathBox.Text.Trim();
            if (!Directory.Exists(path))
            {
                CatalogText.Text = "Max path not found";
                return;
            }

            var info = MaxExportService.ReadCatalog(path);
            CatalogText.Text = $"Next Filter{info.NextFilterIndex:D2} · {info.ExistingPngNames.Count} PNGs · {info.NameCount} names";
        }
        catch (Exception ex)
        {
            CatalogText.Text = ex.Message;
        }
    }

    private void RefreshPreview()
    {
        _workingLut = GradePipeline.Bake(_grade, _importedLut);

        using (var strip = LutStrip.ToBitmap(_workingLut))
            StripImage.Source = ImageBridge.ToWriteableBitmap(strip);

        if (_sourcePreview == null)
        {
            PreviewImage.Source = null;
            PreviewPlaceholder.Visibility = Visibility.Visible;
            return;
        }

        PreviewPlaceholder.Visibility = Visibility.Collapsed;
        using var graded = PreviewService.ApplyLut(
            _sourcePreview, _workingLut, CurrentMode, _grade.Vignette);
        PreviewImage.Source = ImageBridge.ToWriteableBitmap(graded);
    }

    private void ReloadPresets(string? selectName = null)
    {
        _loadingUi = true;
        try
        {
            var presets = PresetService.LoadAll().ToList();
            PresetCombo.ItemsSource = presets;
            if (!string.IsNullOrWhiteSpace(selectName))
            {
                var match = presets.FirstOrDefault(p =>
                    string.Equals(p.Name, selectName, StringComparison.OrdinalIgnoreCase));
                if (match != null)
                    PresetCombo.SelectedItem = match;
            }
        }
        finally
        {
            _loadingUi = false;
        }
    }

    private void PresetCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loadingUi || _applyingPreset) return;
        if (PresetCombo.SelectedItem is not LutPreset preset) return;

        _applyingPreset = true;
        try
        {
            _grade.ApplyFrom(preset.Grade, includeNames: true);
            if (string.IsNullOrWhiteSpace(_grade.FileName) || !_grade.FileName.Contains('_'))
            {
                string prefix = FilePrefixBox.Text.Trim();
                if (prefix.Length > 0 && !_grade.FileName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    _grade.FileName = prefix + SanitizeStem(_grade.DisplayName);
            }
            StatusText.Text = $"Loaded preset: {preset.Name}";
            RefreshPreview();
        }
        finally
        {
            _applyingPreset = false;
        }
    }

    private static string SanitizeStem(string name)
    {
        name = name.Trim().ToLowerInvariant().Replace(' ', '_');
        foreach (char c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return string.IsNullOrWhiteSpace(name) ? "mylut" : name;
    }

    private void SavePreset_Click(object sender, RoutedEventArgs e)
    {
        string suggested = _grade.DisplayName;
        var name = PromptName("Save preset", "Preset name:", suggested);
        if (name == null) return;

        var preset = new LutPreset
        {
            Name = name,
            BuiltIn = false,
            Grade = _grade.ToSnapshot()
        };
        PresetService.SaveUserPreset(preset);
        ReloadPresets(name);
        StatusText.Text = $"Saved preset: {name}";
    }

    private void DeletePreset_Click(object sender, RoutedEventArgs e)
    {
        if (PresetCombo.SelectedItem is not LutPreset preset)
            return;
        if (preset.BuiltIn)
        {
            MessageBox.Show(this, "Built-in presets cannot be deleted.", "Presets",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var confirm = MessageBox.Show(this, $"Delete preset '{preset.Name}'?", "Delete preset",
            MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes) return;

        PresetService.DeleteUserPreset(preset.Name);
        ReloadPresets();
        StatusText.Text = $"Deleted preset: {preset.Name}";
    }

    private string? PromptName(string title, string prompt, string defaultValue)
    {
        var dialog = new Window
        {
            Title = title,
            Width = 420,
            Height = 160,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = this,
            ResizeMode = ResizeMode.NoResize,
            Background = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(0x12, 0x12, 0x12))
        };
        var box = new TextBox { Text = defaultValue, Margin = new Thickness(12, 8, 12, 8) };
        var ok = new Button { Content = "OK", IsDefault = true, Width = 80, Margin = new Thickness(0, 0, 8, 0) };
        var cancel = new Button { Content = "Cancel", IsCancel = true, Width = 80 };
        string? result = null;
        ok.Click += (_, _) =>
        {
            result = box.Text.Trim();
            dialog.DialogResult = true;
        };
        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(12)
        };
        buttons.Children.Add(ok);
        buttons.Children.Add(cancel);
        var root = new StackPanel();
        root.Children.Add(new TextBlock
        {
            Text = prompt,
            Margin = new Thickness(12, 12, 12, 0),
            Foreground = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(0x39, 0xFF, 0x14))
        });
        root.Children.Add(box);
        root.Children.Add(buttons);
        dialog.Content = root;
        box.SelectAll();
        box.Focus();
        return dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(result) ? result : null;
    }

    private void OpenImage_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "Open preview image",
            Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp;*.tif;*.tiff|All files|*.*"
        };
        if (!string.IsNullOrWhiteSpace(_settings.LastImageFolder) && Directory.Exists(_settings.LastImageFolder))
            dlg.InitialDirectory = _settings.LastImageFolder;

        if (dlg.ShowDialog() != true)
            return;

        _sourceFull?.Dispose();
        _sourcePreview?.Dispose();
        _sourceFull = ImageBridge.Load(dlg.FileName);
        _sourcePreview = ImageBridge.DownscaleForPreview(_sourceFull);
        _settings.LastImageFolder = Path.GetDirectoryName(dlg.FileName);
        StatusText.Text = $"Loaded {Path.GetFileName(dlg.FileName)} ({_sourceFull.Width}×{_sourceFull.Height})";
        RefreshPreview();
    }

    private void ImportLut_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "Import Photoshop .CUBE or Unreal strip PNG",
            Filter =
                "Photoshop / Adobe Cube (*.cube)|*.cube;*.CUBE|" +
                "Unreal / Max strip PNG|*.png|" +
                "All LUT files|*.cube;*.CUBE;*.png|" +
                "All files|*.*"
        };
        if (!string.IsNullOrWhiteSpace(_settings.LastLutImportFolder) && Directory.Exists(_settings.LastLutImportFolder))
            dlg.InitialDirectory = _settings.LastLutImportFolder;
        else if (Directory.Exists(MaxPathBox.Text))
            dlg.InitialDirectory = MaxPathBox.Text;

        if (dlg.ShowDialog() != true)
            return;

        try
        {
            string ext = Path.GetExtension(dlg.FileName).ToLowerInvariant();
            string label;
            if (ext == ".cube")
            {
                var loaded = CubeLutReader.LoadDetailed(dlg.FileName);
                _importedLut = loaded.Lut;
                string title = !string.IsNullOrWhiteSpace(loaded.Title)
                    ? loaded.Title!
                    : Path.GetFileNameWithoutExtension(dlg.FileName);
                _grade.DisplayName = title;
                string prefix = FilePrefixBox.Text.Trim();
                if (string.IsNullOrWhiteSpace(prefix)) prefix = "z_";
                if (!prefix.EndsWith('_')) prefix += "_";
                _grade.FileName = prefix + SanitizeStem(title);
                _grade.ImportStrength = 1f;
                label = $"{Path.GetFileName(dlg.FileName)} ({loaded.SourceSize}³ → 16³ Max)";
                StatusText.Text = $"Imported Photoshop/Adobe cube → Max strip ready: {title}";
            }
            else if (ext == ".png")
            {
                _importedLut = LutStrip.FromFile(dlg.FileName);
                label = Path.GetFileName(dlg.FileName);
                if (string.IsNullOrWhiteSpace(_grade.DisplayName) || _grade.DisplayName == "My LUT")
                    _grade.DisplayName = Path.GetFileNameWithoutExtension(dlg.FileName);
                StatusText.Text = $"Imported strip LUT: {Path.GetFileName(dlg.FileName)}";
            }
            else
            {
                throw new NotSupportedException("Use a Photoshop/Adobe .CUBE (3D) or a 256×16 Unreal/Max PNG strip.");
            }

            _settings.LastLutImportFolder = Path.GetDirectoryName(dlg.FileName);
            ImportLabel.Text = label;
            RefreshPreview();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Import failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ClearImport_Click(object sender, RoutedEventArgs e)
    {
        _importedLut = null;
        ImportLabel.Text = "No imported LUT";
        StatusText.Text = "Cleared imported LUT";
        RefreshPreview();
    }

    private void ResetGrade_Click(object sender, RoutedEventArgs e)
    {
        _grade.Reset();
        StatusText.Text = "Grade reset";
        RefreshPreview();
    }

    private void BrowseMaxPath_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog
        {
            Title = "Select GameGuru MAX lut folder",
            InitialDirectory = Directory.Exists(MaxPathBox.Text) ? MaxPathBox.Text : MaxExportService.DefaultMaxLutPath
        };
        if (dlg.ShowDialog() == true)
        {
            MaxPathBox.Text = dlg.FolderName;
            RefreshCatalogStatus();
        }
    }

    private Bitmap BuildGradedForExport()
    {
        if (_sourcePreview == null)
            throw new InvalidOperationException("Open a preview image first so a thumbnail can be generated.");
        // Thumbnails should match the exported LUT (no preview-only vignette).
        return PreviewService.ApplyLut(_sourcePreview, _workingLut, PreviewMode.After, vignette: 0f);
    }

    private void ExportPackage_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            EnsureExportReady();
            var dlg = new OpenFolderDialog { Title = "Export package folder" };
            if (!string.IsNullOrWhiteSpace(_settings.LastExportFolder) && Directory.Exists(_settings.LastExportFolder))
                dlg.InitialDirectory = _settings.LastExportFolder;
            if (dlg.ShowDialog() != true)
                return;

            _settings.LastExportFolder = dlg.FolderName;
            using var graded = BuildGradedForExport();
            var result = MaxExportService.ExportPackage(
                _workingLut, graded, dlg.FolderName, _grade.FileName, _grade.DisplayName);
            StatusText.Text = result.Message ?? "Exported.";
            MessageBox.Show(this,
                $"Wrote:\n{result.LutPngPath}\n{result.ThumbnailPath}\n{result.NamesSnippetPath}\n\nCopy the PNG into Max's lut folder, the thumbnail as the next FilterNN.jpg, and append the name line to filternames.txt.",
                "Export package",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Export failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void InstallToMax_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            EnsureExportReady();
            string lutFolder = MaxPathBox.Text.Trim();
            string assigned = MaxExportService.EnsureAppendFriendlyFileName(lutFolder, _grade.FileName);
            bool renamed = !string.Equals(
                MaxExportService.SanitizeFileName(_grade.FileName),
                assigned,
                StringComparison.OrdinalIgnoreCase);

            string confirmMsg = renamed
                ? $"Install '{_grade.DisplayName}' into:\n{lutFolder}\n\n" +
                  $"File will be auto-named:\n{assigned}\n\n" +
                  "(Keeps Max's alphabetical Filter order correct.)"
                : $"Install '{_grade.DisplayName}' into:\n{lutFolder}\n\nas {assigned}?";

            var confirm = MessageBox.Show(this, confirmMsg, "Install to Max",
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirm != MessageBoxResult.Yes)
                return;

            using var graded = BuildGradedForExport();
            var result = MaxExportService.InstallToMax(
                _workingLut, graded, lutFolder, _grade.FileName, _grade.DisplayName);
            if (!string.IsNullOrWhiteSpace(result.AssignedFileName))
                _grade.FileName = Path.GetFileNameWithoutExtension(result.AssignedFileName);
            StatusText.Text = result.Message ?? "Installed.";
            RefreshCatalogStatus();
            MessageBox.Show(this, result.Message, "Installed", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (UnauthorizedAccessException)
        {
            var fallback = MessageBox.Show(this,
                "Cannot write to the Max LUT folder (permission denied).\n\nExport a package folder instead?",
                "Access denied",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (fallback == MessageBoxResult.Yes)
                ExportPackage_Click(sender, e);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Install failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void EnsureExportReady()
    {
        if (string.IsNullOrWhiteSpace(_grade.DisplayName))
            throw new InvalidOperationException("Enter a display name.");
        if (string.IsNullOrWhiteSpace(_grade.FileName))
            throw new InvalidOperationException("Enter a file name.");
        _workingLut = GradePipeline.Bake(_grade, _importedLut);
    }

    protected override void OnClosed(EventArgs e)
    {
        CaptureSettingsFromUi();
        AppSettingsStore.Save(_settings);
        _sourceFull?.Dispose();
        _sourcePreview?.Dispose();
        base.OnClosed(e);
    }
}
