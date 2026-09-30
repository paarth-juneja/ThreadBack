using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ThreadBack.App;

internal sealed class OverlayWindow : Window
{
    internal readonly TextBox Input = new() { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, Height = 85, MaxLength = 2000 };
    internal readonly TextBox Minutes = new() { Text = "5", Width = 55 };
    internal readonly TextBox Retention = new() { Text = "10", Width = 55 };
    internal readonly CheckBox Eye = new() { Content = "Eye: capture primary screen", Margin = new Thickness(0, 8, 0, 8) };
    internal readonly TextBlock Status = new() { Text = "Eye off", Margin = new Thickness(0, 8, 0, 0) };
    internal readonly TextBlock TaskName = new() { FontWeight = FontWeights.Bold };
    private readonly StackPanel panel = new() { Width = 330, Margin = new Thickness(10) };
    private readonly string settingsPath;
    private readonly Button logo;
    private readonly Border eyeBadge = new() { Width = 12, Height = 12, CornerRadius = new CornerRadius(6), Background = Brushes.LimeGreen, BorderBrush = Brushes.White, BorderThickness = new Thickness(2), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom, Visibility = Visibility.Collapsed, IsHitTestVisible = false };
    private bool expanded;

    internal OverlayWindow(string directory, Action open, Action save, Func<Task> capture, Action<bool> eye)
    {
        settingsPath = Path.Combine(directory, "overlay-position.json");
        Title = "ThreadBack overlay"; WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true; Background = Brushes.Transparent; Topmost = true; ShowInTaskbar = false;
        SizeToContent = SizeToContent.WidthAndHeight;
        var stack = new StackPanel();
        var logoContent = new Grid { Width = 48, Height = 48, IsHitTestVisible = false };
        logoContent.Children.Add(new Image { Source = new System.Windows.Media.Imaging.BitmapImage(new Uri("pack://application:,,,/Assets/ThreadBack.png")), Stretch = Stretch.Uniform });
        logoContent.Children.Add(eyeBadge);
        logo = new Button { Content = logoContent, ToolTip = "Click to open · drag to move", Padding = new Thickness(0), Background = Brushes.Transparent, Width = 56, Height = 56, Margin = new Thickness(0), HorizontalAlignment = HorizontalAlignment.Left };
        System.Windows.Automation.AutomationProperties.SetName(logo, "ThreadBack · Eye off");
        // Own mouse capture: Button's default capture prevents native DragMove from
        // reliably receiving the drag. Screen coordinates remain stable as we move.
        Point start = default, origin = default; bool pressed = false, dragged = false;
        logo.PreviewMouseLeftButtonDown += (_, e) =>
        {
            start = PointToScreen(e.GetPosition(this)); origin = new Point(Left, Top);
            pressed = true; dragged = false; logo.CaptureMouse(); e.Handled = true;
        };
        logo.PreviewMouseMove += (_, e) =>
        {
            if (!pressed || e.LeftButton != MouseButtonState.Pressed) return;
            var delta = PointToScreen(e.GetPosition(this)) - start;
            if (!dragged && delta.Length < 5) return;
            dragged = true;
            var scale = VisualTreeHelper.GetDpi(this);
            Left = origin.X + delta.X / scale.DpiScaleX; Top = origin.Y + delta.Y / scale.DpiScaleY;
            e.Handled = true;
        };
        void TogglePanel() { expanded = !expanded; panel.Visibility = expanded ? Visibility.Visible : Visibility.Collapsed; UpdateLayout(); Clamp(); }
        logo.PreviewMouseLeftButtonUp += (_, e) =>
        {
            if (!pressed) return;
            pressed = false; logo.ReleaseMouseCapture();
            if (dragged) SavePosition(); else TogglePanel();
            e.Handled = true;
        };
        logo.LostMouseCapture += (_, _) => pressed = false;
        logo.Click += (_, _) => TogglePanel(); // Keyboard/accessibility activation.
        var header = new DockPanel();
        var close = new Button { Content = "×", ToolTip = "Hide overlay and pause Eye", Width = 30, Height = 30, FontSize = 20, Padding = new Thickness(0), Margin = new Thickness(4, 0, 0, 0), VerticalAlignment = VerticalAlignment.Top };
        System.Windows.Automation.AutomationProperties.SetName(close, "Hide overlay and pause Eye");
        close.Click += (_, _) => { eye(false); Hide(); };
        DockPanel.SetDock(close, Dock.Right); header.Children.Add(close); header.Children.Add(logo);
        stack.Children.Add(header); stack.Children.Add(panel);
        panel.Children.Add(TaskName);
        panel.Children.Add(new TextBlock { Text = "What changed?", Margin = new Thickness(0, 8, 0, 5) });
        panel.Children.Add(Input);
        var actions = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) };
        void Add(string label, Action action) { var b = new Button { Content = label, Padding = new Thickness(9, 7, 9, 7) }; b.Click += (_, _) => action(); actions.Children.Add(b); }
        Add("Save update", save); Add("Snapshot now", async () => await capture()); Add("Open app", open);
        panel.Children.Add(actions); panel.Children.Add(Eye);
        var settings = new StackPanel { Orientation = Orientation.Horizontal };
        settings.Children.Add(new TextBlock { Text = "Every (min) ", VerticalAlignment = VerticalAlignment.Center }); settings.Children.Add(Minutes);
        settings.Children.Add(new TextBlock { Text = " Keep last ", VerticalAlignment = VerticalAlignment.Center }); settings.Children.Add(Retention);
        panel.Children.Add(settings);
        panel.Children.Add(new TextBlock { Text = "1–120 minutes · 1–30 Eye snapshots. Older Eye snapshots are deleted. Manual snapshots stay until removed.", FontSize = 11, Margin = new Thickness(0, 6, 0, 0) });
        panel.Children.Add(Status); panel.Visibility = Visibility.Collapsed;
        Eye.Checked += (_, _) => eye(true); Eye.Unchecked += (_, _) => eye(false);
        Content = new Border { Background = new SolidColorBrush(Color.FromRgb(247,245,250)), CornerRadius = new CornerRadius(12), Padding = new Thickness(4), Child = stack };
        Left = SystemParameters.WorkArea.Left + 16; Top = SystemParameters.WorkArea.Bottom - 80;
        try { var p = JsonSerializer.Deserialize<double[]>(File.ReadAllText(settingsPath)); if (p is { Length: 2 } && p.All(double.IsFinite)) { Left = p[0]; Top = p[1]; } } catch { }
        Loaded += (_, _) => Clamp();
    }
    internal void Indicate(bool active) { eyeBadge.Visibility = active ? Visibility.Visible : Visibility.Collapsed; logo.ToolTip = active ? "Eye on · click for Pause" : "Eye off · click to open · drag to move"; System.Windows.Automation.AutomationProperties.SetName(logo, active ? "ThreadBack · Eye on" : "ThreadBack · Eye off"); }
    private void Clamp() { var r = SystemParameters.WorkArea; Left = Math.Clamp(Left, r.Left, Math.Max(r.Left, r.Right - ActualWidth)); Top = Math.Clamp(Top, r.Top, Math.Max(r.Top, r.Bottom - ActualHeight)); }
    private void SavePosition() { try { File.WriteAllText(settingsPath, JsonSerializer.Serialize(new[] { Left, Top })); } catch { Status.Text = "Position could not be saved."; } }
}
