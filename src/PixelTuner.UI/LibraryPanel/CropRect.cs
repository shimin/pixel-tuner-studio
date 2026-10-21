using CommunityToolkit.Mvvm.ComponentModel;
using PixelRect = PixelTuner.Core.Imaging.PixelRect;

namespace PixelTuner.UI.LibraryPanel;

/// <summary>
/// A crop rectangle over a picture of a known size. It always lies inside the picture and is at least a pixel big:
/// typing too much pulls it back. Dragging on the preview and typing in the boxes change the same four numbers.
/// </summary>
public sealed partial class CropRect : ObservableObject
{
    private readonly Action _changed;
    private bool _clamping;

    public CropRect(Action changed, int imageWidth, int imageHeight)
    {
        _changed = changed;
        Reset(imageWidth, imageHeight);
    }

    public int ImageWidth { get; private set; }
    public int ImageHeight { get; private set; }

    [ObservableProperty] private int _x;
    [ObservableProperty] private int _y;
    [ObservableProperty] private int _width;
    [ObservableProperty] private int _height;

    partial void OnXChanged(int value) => Clamp();
    partial void OnYChanged(int value) => Clamp();
    partial void OnWidthChanged(int value) => Clamp();
    partial void OnHeightChanged(int value) => Clamp();

    // The number boxes work with decimal.
    public decimal? XBox { get => X; set { if (value is { } v) X = (int)v; } }
    public decimal? YBox { get => Y; set { if (value is { } v) Y = (int)v; } }
    public decimal? WidthBox { get => Width; set { if (value is { } v) Width = (int)v; } }
    public decimal? HeightBox { get => Height; set { if (value is { } v) Height = (int)v; } }

    public PixelRect Value => new(X, Y, Width, Height);

    public bool IsWhole => X == 0 && Y == 0 && Width == ImageWidth && Height == ImageHeight;

    /// <summary>Starts over on a picture of another size: the whole picture.</summary>
    public void Reset(int imageWidth, int imageHeight)
    {
        ImageWidth = imageWidth;
        ImageHeight = imageHeight;
        Set(0, 0, imageWidth, imageHeight);
    }

    public void Reset() => Reset(ImageWidth, ImageHeight);

    public void Set(int x, int y, int width, int height)
    {
        _clamping = true;
        try
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }
        finally
        {
            _clamping = false;
        }
        Clamp();
    }

    private void Clamp()
    {
        if (!_clamping)
        {
            _clamping = true;
            try
            {
                int w = Math.Clamp(Width, 1, Math.Max(1, ImageWidth)), h = Math.Clamp(Height, 1, Math.Max(1, ImageHeight));
                int x = Math.Clamp(X, 0, Math.Max(0, ImageWidth - w)), y = Math.Clamp(Y, 0, Math.Max(0, ImageHeight - h));
                if (w != Width) Width = w;
                if (h != Height) Height = h;
                if (x != X) X = x;
                if (y != Y) Y = y;
            }
            finally
            {
                _clamping = false;
            }
        }
        OnPropertyChanged(nameof(XBox));
        OnPropertyChanged(nameof(YBox));
        OnPropertyChanged(nameof(WidthBox));
        OnPropertyChanged(nameof(HeightBox));
        OnPropertyChanged(nameof(IsWhole));
        _changed();
    }
}
