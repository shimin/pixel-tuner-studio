using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PixelTuner.Core.Editing;
using PixelTuner.Core.Imaging;
using PixelTuner.Core.Model;
using PixelTuner.Package;
using PixelTuner.UI.ViewModels;

namespace PixelTuner.UI.LibraryPanel;

/// <summary>The steps of the import, as PixelTuner 2's window had them.</summary>
public enum ImportStep
{
    /// <summary>Cut the part of the picture that is wanted.</summary>
    Cut = 1,

    /// <summary>Squeeze the picture in perspective (optional).</summary>
    Perspective = 2,

    /// <summary>Trim what the squeeze left, scale to the height of the device, correct the colour.</summary>
    Finish = 3,
}

/// <summary>
/// Bringing a picture into the library the way PixelTuner 2's import window did: cut a part, squeeze it in perspective
/// (optional), trim it and scale it to the height of the device, correct the colour. A picture that is already right goes
/// in as the file it is.
/// </summary>
public sealed partial class ImportViewModel : ObservableObject
{
    private readonly ProjectDocument _document;
    private readonly string _sourcePath;
    private readonly PixelImage _source;
    private readonly Action<LibraryEntry?> _done;

    // The stages of the pipeline are kept until something before them changes.
    private PixelImage? _cut;
    private PixelImage? _warped;
    private PixelImage? _result;
    private Bitmap? _sourceBitmap, _warpedBitmap, _resultBitmap;

    /// <param name="targetHeight">The LEDs of the timeline or device the picture is for; 0 when it can have any height.</param>
    /// <param name="heightIsFixed">The height belongs to a timeline that already plays pictures: it cannot be changed here.</param>
    /// <param name="done">Called with the new entry when the picture was brought in, or null when the user gave up.</param>
    public ImportViewModel(ProjectDocument document, string sourcePath, PixelImage source, int targetHeight, bool heightIsFixed, Action<LibraryEntry?> done)
    {
        _document = document;
        _sourcePath = sourcePath;
        _source = source;
        _done = done;
        Cut = new CropRect(CutChanged, source.Width, source.Height);
        Trim = new CropRect(TrimChanged, source.Width, source.Height);
        ScaleToHeight = targetHeight > 0;
        TargetHeight = targetHeight > 0 ? targetHeight : source.Height;
        HeightIsFixed = heightIsFixed && targetHeight > 0;
    }

    public string SourceName => Path.GetFileName(_sourcePath.Replace('\\', '/'));
    public int SourceWidth => _source.Width;
    public int SourceHeight => _source.Height;
    public bool HeightIsFixed { get; }

    // ---- steps

    [ObservableProperty] private ImportStep _step = ImportStep.Cut;

    partial void OnStepChanged(ImportStep value)
    {
        OnPropertyChanged(nameof(IsCutStep));
        OnPropertyChanged(nameof(IsPerspectiveStep));
        OnPropertyChanged(nameof(IsFinishStep));
        OnPropertyChanged(nameof(IsTrimStep));
        OnPropertyChanged(nameof(IsResultStep));
        OnPropertyChanged(nameof(WarpedBitmap));
    }

    public bool IsCutStep => Step == ImportStep.Cut;
    public bool IsPerspectiveStep => Step == ImportStep.Perspective;
    public bool IsFinishStep => Step == ImportStep.Finish;

    /// <summary>The trim rectangle is drawn on the squeezed picture, in the last step.</summary>
    public bool IsTrimStep => Step == ImportStep.Finish && WarpEnabled;

    /// <summary>The last step without a perspective squeeze shows the result itself, as there is nothing to trim.</summary>
    public bool IsResultStep => Step == ImportStep.Finish && !WarpEnabled;

    [RelayCommand] private void ShowCut() => Step = ImportStep.Cut;
    [RelayCommand] private void ShowPerspective() => Step = ImportStep.Perspective;
    [RelayCommand] private void ShowFinish() => Step = ImportStep.Finish;

    // ---- cut

    /// <summary>The first crop, on the picture as it is.</summary>
    public CropRect Cut { get; }

    /// <summary>The picture as it is, for the cut to be drawn over.</summary>
    public Bitmap? SourceBitmap => _sourceBitmap ??= PixelBitmap.From(_source);

    public bool IsCropped => !Cut.IsWhole;

    private void CutChanged()
    {
        _cut = _warped = _result = null;
        _warpedBitmap = _resultBitmap = null;
        // What the squeeze makes of a new cut is another picture: the trim starts over on it.
        if (WarpEnabled) Trim.Reset(Warped.Width, Warped.Height);
        Changed();
    }

    // ---- perspective

    [ObservableProperty] private bool _warpEnabled;
    [ObservableProperty] private double _warpTop = PerspectiveWarp.DefaultTop;
    [ObservableProperty] private double _warpMiddle = PerspectiveWarp.DefaultMiddle;
    [ObservableProperty] private double _warpBottom = PerspectiveWarp.DefaultBottom;

    public double WarpMin => PerspectiveWarp.MinOffset;
    public double WarpMax => PerspectiveWarp.MaxOffset;

    partial void OnWarpEnabledChanged(bool value) => WarpSettingsChanged();
    partial void OnWarpTopChanged(double value) => WarpSettingsChanged();
    partial void OnWarpMiddleChanged(double value) => WarpSettingsChanged();
    partial void OnWarpBottomChanged(double value) => WarpSettingsChanged();

    private void WarpSettingsChanged()
    {
        _warped = _result = null;
        _warpedBitmap = _resultBitmap = null;
        if (WarpEnabled) Trim.Reset(Warped.Width, Warped.Height);
        OnPropertyChanged(nameof(IsTrimStep));
        OnPropertyChanged(nameof(IsResultStep));
        OnPropertyChanged(nameof(WarpedBitmap));
        Changed();
    }

    /// <summary>After the cut, the part of the picture that goes on.</summary>
    private PixelImage CutImage => _cut ??= Cut.IsWhole ? _source : ImageImport.Crop(_source, Cut.Value);

    /// <summary>The cut picture, squeezed in perspective when that is on.</summary>
    public PixelImage Warped => _warped ??= WarpEnabled ? PerspectiveWarp.Apply(CutImage, WarpTop, WarpMiddle, WarpBottom) : CutImage;

    public Bitmap? WarpedBitmap => _warpedBitmap ??= PixelBitmap.From(Warped);

    // ---- trim (the second crop, on the squeezed picture)

    public CropRect Trim { get; }

    private void TrimChanged()
    {
        _result = null;
        _resultBitmap = null;
        Changed();
    }

    // ---- scale

    [ObservableProperty] private bool _scaleToHeight;
    [ObservableProperty] private int _targetHeight;

    partial void OnScaleToHeightChanged(bool value) => Changed();

    partial void OnTargetHeightChanged(int value)
    {
        if (value < 1 || value > ushort.MaxValue)
        {
            TargetHeight = Math.Clamp(value, 1, ushort.MaxValue);
            return;
        }
        Changed();
    }

    public decimal? TargetHeightBox { get => TargetHeight; set { if (value is { } v) TargetHeight = (int)v; } }

    // ---- colour

    [ObservableProperty] private int _brightness;
    [ObservableProperty] private bool _midtonesOnly;
    [ObservableProperty] private double _contrast;
    [ObservableProperty] private int _hue;
    [ObservableProperty] private double _saturation;
    [ObservableProperty] private double _value;
    [ObservableProperty] private double _sharpen;

    partial void OnBrightnessChanged(int value) => Changed();
    partial void OnMidtonesOnlyChanged(bool value) => Changed();
    partial void OnContrastChanged(double value) => Changed();
    partial void OnHueChanged(int value) => Changed();
    partial void OnSaturationChanged(double value) => Changed();
    partial void OnValueChanged(double value) => Changed();
    partial void OnSharpenChanged(double value) => Changed();

    public ImportAdjustments Adjustments => new(Brightness, MidtonesOnly, Contrast, Hue, Saturation, Value, Sharpen);

    // The sliders work with double.
    public double BrightnessSlider { get => Brightness; set => Brightness = (int)Math.Round(value); }
    public double HueSlider { get => Hue; set => Hue = (int)Math.Round(value); }

    [RelayCommand]
    private void ResetColour()
    {
        Brightness = 0;
        MidtonesOnly = false;
        Contrast = 0;
        Hue = 0;
        Saturation = 0;
        Value = 0;
        Sharpen = 0;
    }

    // ---- result

    private void Changed()
    {
        _result = null;
        _resultBitmap = null;
        OnPropertyChanged(nameof(TargetHeightBox));
        OnPropertyChanged(nameof(BrightnessSlider));
        OnPropertyChanged(nameof(HueSlider));
        OnPropertyChanged(nameof(ResultSize));
        OnPropertyChanged(nameof(ResultWidth));
        OnPropertyChanged(nameof(ResultHeight));
        OnPropertyChanged(nameof(ResultBitmap));
        OnPropertyChanged(nameof(IsCropped));
        OnPropertyChanged(nameof(IsUntouched));
        OnPropertyChanged(nameof(FitsTarget));
    }

    /// <summary>The picture as it will be stored.</summary>
    public PixelImage Result
    {
        get
        {
            if (_result is not null) return _result;
            var trimmed = WarpEnabled && !Trim.IsWhole ? ImageImport.Crop(Warped, Trim.Value) : Warped;
            var scaled = ScaleToHeight ? ImageImport.Scale(trimmed, ImageImport.ScaledWidth(trimmed.Width, trimmed.Height, TargetHeight), TargetHeight) : trimmed;
            return _result = ImageImport.Adjust(scaled, Adjustments);
        }
    }

    /// <summary>"72 × 36"</summary>
    public string ResultSize => $"{Result.Width} × {Result.Height}";

    public int ResultWidth => Result.Width;
    public int ResultHeight => Result.Height;

    public Bitmap? ResultBitmap => _resultBitmap ??= PixelBitmap.From(Result);

    /// <summary>Nothing is cut, squeezed, scaled or corrected: the file goes in as it is.</summary>
    public bool IsUntouched => !IsCropped && !WarpEnabled && !ScaleToHeight && Adjustments.IsNone;

    /// <summary>Whether the result has the height the timeline asks for (always when no height is asked).</summary>
    public bool FitsTarget => !HeightIsFixed || Result.Height == TargetHeight;

    [RelayCommand]
    private void ResetCut() => Cut.Reset();

    [RelayCommand]
    private void ResetTrim() => Trim.Reset();

    [RelayCommand]
    private void Apply()
    {
        if (!FitsTarget) return;
        string name = Path.GetFileNameWithoutExtension(SourceName);
        string id;
        int width, height;
        if (IsUntouched)
        {
            id = _document.AddMedia(File.ReadAllBytes(_sourcePath), SourceName);
            (width, height) = (_source.Width, _source.Height);
        }
        else
        {
            id = _document.AddMedia(ImageImport.EncodePng(Result), name + ".png");
            (width, height) = (Result.Width, Result.Height);
        }

        var existing = _document.Project.Library.FirstOrDefault(e => e.ResourceId == id && e.Kind == LibraryKind.Image);
        if (existing is not null)
        {
            _done(existing);   // the same picture is in the library already
            return;
        }
        var entry = new LibraryEntry { Name = name, Kind = LibraryKind.Image, ResourceId = id, SourcePath = _sourcePath, Width = width, Height = height };
        _document.Execute(Edits.AddLibraryEntry(_document.Project, entry));
        _done(entry);
    }

    [RelayCommand]
    private void Cancel() => _done(null);
}
