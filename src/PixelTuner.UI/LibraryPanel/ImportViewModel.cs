using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PixelTuner.Core.Editing;
using PixelTuner.Core.Imaging;
using PixelTuner.Core.Model;
using PixelTuner.Package;
using PixelTuner.UI.ViewModels;

namespace PixelTuner.UI.LibraryPanel;

/// <summary>
/// Bringing a picture into the library the way PixelTuner 2's import window did: cut a part, scale it to the height of the
/// device, correct the colour. A picture that is already right goes in as the file it is.
/// </summary>
public sealed partial class ImportViewModel : ObservableObject
{
    private readonly ProjectDocument _document;
    private readonly string _sourcePath;
    private readonly PixelImage _source;
    private readonly Action<LibraryEntry?> _done;
    private bool _dirty = true;
    private PixelImage? _result;
    private Bitmap? _bitmap;

    /// <param name="targetHeight">The LEDs of the timeline or device the picture is for; 0 when it can have any height.</param>
    /// <param name="heightIsFixed">The height belongs to a timeline that already plays pictures: it cannot be changed here.</param>
    /// <param name="done">Called with the new entry when the picture was brought in, or null when the user gave up.</param>
    public ImportViewModel(ProjectDocument document, string sourcePath, PixelImage source, int targetHeight, bool heightIsFixed, Action<LibraryEntry?> done)
    {
        _document = document;
        _sourcePath = sourcePath;
        _source = source;
        _done = done;
        CropWidth = source.Width;
        CropHeight = source.Height;
        ScaleToHeight = targetHeight > 0;
        TargetHeight = targetHeight > 0 ? targetHeight : source.Height;
        HeightIsFixed = heightIsFixed && targetHeight > 0;
    }

    public string SourceName => Path.GetFileName(_sourcePath.Replace('\\', '/'));
    public int SourceWidth => _source.Width;
    public int SourceHeight => _source.Height;
    public bool HeightIsFixed { get; }

    /// <summary>The picture as it is, for the crop to be drawn over.</summary>
    public Bitmap? SourceBitmap => _sourceBitmap ??= PixelBitmap.From(_source);
    private Bitmap? _sourceBitmap;

    // ---- crop

    [ObservableProperty] private int _cropX;
    [ObservableProperty] private int _cropY;
    [ObservableProperty] private int _cropWidth;
    [ObservableProperty] private int _cropHeight;

    partial void OnCropXChanged(int value) => CropChanged(nameof(CropX));
    partial void OnCropYChanged(int value) => CropChanged(nameof(CropY));
    partial void OnCropWidthChanged(int value) => CropChanged(nameof(CropWidth));
    partial void OnCropHeightChanged(int value) => CropChanged(nameof(CropHeight));

    private bool _clamping;

    // The rectangle always lies inside the picture and is at least one pixel; typing too much pulls it back.
    private void CropChanged(string changed)
    {
        if (!_clamping)
        {
            _clamping = true;
            try
            {
                int w = Math.Clamp(CropWidth, 1, _source.Width), h = Math.Clamp(CropHeight, 1, _source.Height);
                int x = Math.Clamp(CropX, 0, _source.Width - w), y = Math.Clamp(CropY, 0, _source.Height - h);
                if (w != CropWidth) CropWidth = w;
                if (h != CropHeight) CropHeight = h;
                if (x != CropX) CropX = x;
                if (y != CropY) CropY = y;
            }
            finally
            {
                _clamping = false;
            }
        }
        Changed();
    }

    public PixelRect Crop => new(CropX, CropY, CropWidth, CropHeight);

    public bool IsCropped => Crop != PixelRect.Whole(_source);

    [RelayCommand]
    private void ResetCrop()
    {
        CropX = 0;
        CropY = 0;
        CropWidth = _source.Width;
        CropHeight = _source.Height;
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
        _dirty = true;
        OnPropertyChanged(nameof(ResultWidth));
        OnPropertyChanged(nameof(ResultHeight));
        OnPropertyChanged(nameof(ResultBitmap));
        OnPropertyChanged(nameof(IsCropped));
        OnPropertyChanged(nameof(IsUntouched));
    }

    /// <summary>The picture as it will be stored.</summary>
    public PixelImage Result
    {
        get
        {
            if (_dirty || _result is null)
            {
                var cut = Crop == PixelRect.Whole(_source) ? _source : ImageImport.Crop(_source, Crop);
                var scaled = ScaleToHeight ? ImageImport.Scale(cut, ImageImport.ScaledWidth(cut.Width, cut.Height, TargetHeight), TargetHeight) : cut;
                _result = ImageImport.Adjust(scaled, Adjustments);
                _dirty = false;
                _bitmap = null;
            }
            return _result;
        }
    }

    public int ResultWidth => Result.Width;
    public int ResultHeight => Result.Height;

    public Bitmap? ResultBitmap => _bitmap ??= PixelBitmap.From(Result);

    /// <summary>Nothing is cut, scaled or corrected: the file goes in as it is.</summary>
    public bool IsUntouched => !IsCropped && !ScaleToHeight && Adjustments.IsNone;

    /// <summary>Whether the result has the height the timeline asks for (always when no height is asked).</summary>
    public bool FitsTarget => !HeightIsFixed || Result.Height == TargetHeight;

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
