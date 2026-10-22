using Avalonia.Controls;
using Avalonia.Threading;

namespace PixelTuner.UI.DevicesPanel;

public partial class DevicesPanelView : UserControl
{
    private DispatcherTimer? _poll;

    public DevicesPanelView() => InitializeComponent();

    // The hub asks the devices for their state once a second; the lines of the list are read again at the same pace.
    protected override void OnAttachedToVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _poll = new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Background, (_, _) => (DataContext as DevicesPanelViewModel)?.Poll());
        _poll.Start();
    }

    protected override void OnDetachedFromVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        _poll?.Stop();
        _poll = null;
        base.OnDetachedFromVisualTree(e);
    }
}
