using Keynest.Windows.ViewModels;
using Keynest.Windows.Views;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;

namespace Keynest.Windows;

public sealed partial class MainWindow : Window
{
    private readonly MainViewModel _vm;

    public MainWindow(MainViewModel vm)
    {
        _vm = vm;
        InitializeComponent();
        SetupWindow();
        SystemBackdrop = new MicaBackdrop();
        MainFrame.Navigate(typeof(EntriesPage), _vm);
    }

    private void SetupWindow()
    {
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Standard;
        AppWindow.TitleBar.ButtonBackgroundColor = Colors.Transparent;
        AppWindow.TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
        AppWindow.TitleBar.ButtonHoverBackgroundColor = Colors.Transparent;
        AppWindow.TitleBar.ButtonPressedBackgroundColor = Colors.Transparent;

        // 400×620 logical pixels, DPI-aware physical sizing
        var dpi = (double)GetDpiForWindow(WinRT.Interop.WindowNative.GetWindowHandle(this));
        var scale = dpi / 96.0;
        AppWindow.Resize(new SizeInt32((int)(400 * scale), (int)(620 * scale)));

        CenterOnWorkArea();

        AppWindow.Changed += (_, e) => { if (e.DidSizeChange) UpdateDragRegion(); };
        UpdateDragRegion();
    }

    private void CenterOnWorkArea()
    {
        var area = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Nearest).WorkArea;
        AppWindow.Move(new PointInt32(
            area.X + (area.Width  - AppWindow.Size.Width)  / 2,
            area.Y + (area.Height - AppWindow.Size.Height) / 2));
    }

    private void UpdateDragRegion()
    {
        var h = AppWindow.TitleBar.Height;
        var rightInset = AppWindow.TitleBar.RightInset;
        AppWindow.TitleBar.SetDragRectangles(
            [new RectInt32(0, 0, AppWindow.Size.Width - rightInset, h)]);
    }

    public OverlappedPresenter? OverlappedPresenter =>
        AppWindow.Presenter as OverlappedPresenter;

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hwnd);
}
