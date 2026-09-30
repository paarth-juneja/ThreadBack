using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ThreadBack.App;

public sealed class ModelSettingsWindow : Window
{
    private static readonly int[] IdleValues = [-1, 2, 5, 0];
    private static readonly int[] MemoryValues = [0, 6, 8, 10];
    private readonly ComboBox visionIdle = new();
    private readonly ComboBox textIdle = new();
    private readonly ComboBox memory = new();
    private readonly CheckBox gpuSwitch = new();
    private readonly CheckBox npuSwitch = new();
    public ModelSettings Selected { get; private set; }

    public ModelSettingsWindow(ModelSettings current, HardwareProfile hardware)
    {
        Selected = new ModelSettings { VisionIdleMinutes = current.VisionIdleMinutes, TextIdleMinutes = current.TextIdleMinutes, MemoryLimitGb = current.MemoryLimitGb, GpuEnabled = current.GpuEnabled, NpuEnabled = current.NpuEnabled };
        Title = "Model settings";
        Width = 510; Height = 610; MinWidth = 470; MinHeight = 540;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new SolidColorBrush(Color.FromRgb(247, 245, 249));
        FontFamily = new FontFamily("Segoe UI");
        var panel = new StackPanel { Margin = new Thickness(28) };
        Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        panel.Children.Add(new TextBlock { Text = "Model settings", FontSize = 25, FontWeight = FontWeights.SemiBold });
        panel.Children.Add(Description($"Detected RAM: {hardware.RamGb:0.0} GB · GPU: {(hardware.GpuName.Length == 0 ? "not detected" : hardware.GpuName)} · NPU: {(hardware.NpuName.Length == 0 ? "not detected" : hardware.NpuName)}"));
        panel.Children.Add(Description("Choose when each model leaves memory after its last job. Running work is never interrupted by an idle timer."));
        AddChoice(panel, "Image understanding", visionIdle, ["Keep loaded until you close the app", "Unload after 2 minutes", "Unload after 5 minutes", "Unload as soon as work finishes"], Array.IndexOf(IdleValues, current.VisionIdleMinutes));
        AddChoice(panel, "Resume capsule model", textIdle, ["Keep loaded until you close the app", "Unload after 2 minutes", "Unload after 5 minutes", "Unload as soon as work finishes"], Array.IndexOf(IdleValues, current.TextIdleMinutes));
        AddChoice(panel, "Memory limit per model", memory, ["No limit", "6 GB", "8 GB", "10 GB"], Array.IndexOf(MemoryValues, current.MemoryLimitGb));
        panel.Children.Add(Description("A limit stops a model if it needs more memory and may slow model loading. The models run one at a time. The installed models need more than 2–3 GB, so the first limit is 6 GB."));
        panel.Children.Add(new TextBlock { Text = "Hardware acceleration", FontSize = 16, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 14, 0, 8) });
        gpuSwitch.Content = "GPU on · Vulkan"; gpuSwitch.IsEnabled = hardware.GpuAvailable; gpuSwitch.IsChecked = hardware.GpuAvailable && current.GpuEnabled;
        gpuSwitch.ToolTip = hardware.GpuAvailable ? hardware.GpuRuntimeDevice : "A working Vulkan model backend and GPU are required.";
        gpuSwitch.Margin = new Thickness(0, 3, 0, 6); panel.Children.Add(gpuSwitch);
        npuSwitch.Content = "NPU on · OpenVINO (capsule model)"; npuSwitch.IsEnabled = hardware.NpuAvailable; npuSwitch.IsChecked = hardware.NpuAvailable && current.NpuEnabled;
        npuSwitch.ToolTip = hardware.NpuAvailable ? hardware.NpuRuntimeDevice : hardware.NpuName.Length > 0 && !hardware.NpuModelValidated ? "NPU detected. The capsule model must pass the OpenVINO test before this switch can be enabled." : "A working OpenVINO model backend and NPU are required.";
        npuSwitch.Margin = new Thickness(0, 3, 0, 4); panel.Children.Add(npuSwitch);
        gpuSwitch.Checked += (_, _) => npuSwitch.IsChecked = false;
        npuSwitch.Checked += (_, _) => gpuSwitch.IsChecked = false;
        panel.Children.Add(Description("GPU runs both models when supported. NPU applies to the capsule model; image understanding stays on CPU. Unavailable switches cannot be enabled."));
        var save = new Button { Content = "Save settings", Background = new SolidColorBrush(Color.FromRgb(113, 81, 191)), Foreground = Brushes.White, Padding = new Thickness(15, 8, 15, 8), Margin = new Thickness(0, 16, 0, 0), HorizontalAlignment = HorizontalAlignment.Right };
        save.Click += (_, _) => { Selected.VisionIdleMinutes = IdleValues[visionIdle.SelectedIndex]; Selected.TextIdleMinutes = IdleValues[textIdle.SelectedIndex]; Selected.MemoryLimitGb = MemoryValues[memory.SelectedIndex]; Selected.GpuEnabled = gpuSwitch.IsChecked == true && hardware.GpuAvailable; Selected.NpuEnabled = npuSwitch.IsChecked == true && hardware.NpuAvailable; DialogResult = true; };
        panel.Children.Add(save);
    }
    private static TextBlock Description(string text) => new() { Text = text, TextWrapping = TextWrapping.Wrap, Foreground = new SolidColorBrush(Color.FromRgb(98, 89, 111)), FontSize = 12, Margin = new Thickness(0, 7, 0, 9) };
    private static void AddChoice(Panel panel, string label, ComboBox box, string[] choices, int selected)
    {
        panel.Children.Add(new TextBlock { Text = label, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 9, 0, 6) });
        foreach (var choice in choices) box.Items.Add(choice);
        box.SelectedIndex = Math.Max(0, selected);
        panel.Children.Add(box);
    }
}
