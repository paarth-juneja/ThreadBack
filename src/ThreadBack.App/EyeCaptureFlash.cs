using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Controls;
using Forms = System.Windows.Forms;

namespace ThreadBack.App;

// A short, click-through confirmation shown only after an Eye snapshot is saved.
internal sealed class EyeCaptureFlash : Window
{
    private const int GwlExStyle = -20;
    private const nint WsExTransparent = 0x20;
    private const nint WsExToolWindow = 0x80;
    private const nint WsExNoActivate = 0x08000000;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpNoZOrder = 0x0004;

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern nint GetWindowLongPtr(nint hwnd, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern nint SetWindowLongPtr(nint hwnd, int index, nint value);
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(nint hwnd, nint insertAfter, int x, int y, int width, int height, uint flags);

    internal EyeCaptureFlash()
    {
        var bounds = Forms.Screen.PrimaryScreen!.Bounds;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        Topmost = true;
        ShowInTaskbar = false;
        ShowActivated = false;
        Focusable = false;
        Left = bounds.Left;
        Top = bounds.Top;
        Width = bounds.Width;
        Height = bounds.Height;
        Opacity = 0;

        var glow = new Grid { IsHitTestVisible = false };
        glow.Children.Add(new Border
        {
            Margin = new Thickness(8),
            BorderThickness = new Thickness(17),
            BorderBrush = new SolidColorBrush(Color.FromArgb(130, 91, 190, 255)),
            Effect = new BlurEffect { Radius = 26 }
        });
        glow.Children.Add(new Border
        {
            Margin = new Thickness(6),
            BorderThickness = new Thickness(7),
            BorderBrush = new SolidColorBrush(Color.FromArgb(160, 123, 213, 255)),
            Effect = new BlurEffect { Radius = 10 }
        });
        glow.Children.Add(new Border
        {
            Margin = new Thickness(4),
            BorderThickness = new Thickness(2),
            BorderBrush = new SolidColorBrush(Color.FromArgb(225, 203, 242, 255))
        });
        Content = glow;

        SourceInitialized += (_, _) =>
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            SetWindowLongPtr(hwnd, GwlExStyle, GetWindowLongPtr(hwnd, GwlExStyle) | WsExTransparent | WsExToolWindow | WsExNoActivate);
            HwndSource.FromHwnd(hwnd)?.AddHook((nint _, int message, nint __, nint ___, ref bool handled) =>
            {
                const int WmNcHitTest = 0x0084;
                const int HtTransparent = -1;
                if (message != WmNcHitTest) return 0;
                handled = true;
                return HtTransparent;
            });
            SetWindowPos(hwnd, 0, bounds.Left, bounds.Top, bounds.Width, bounds.Height, SwpNoActivate | SwpNoZOrder);
        };
        Loaded += (_, _) =>
        {
            var animation = new DoubleAnimationUsingKeyFrames();
            animation.KeyFrames.Add(new DiscreteDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            animation.KeyFrames.Add(new EasingDoubleKeyFrame(0.9, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(140))));
            animation.KeyFrames.Add(new LinearDoubleKeyFrame(0.72, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(470))));
            animation.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(1000))));
            animation.Completed += (_, _) => Close();
            BeginAnimation(OpacityProperty, animation);
        };
    }
}
