using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;
using ThreadBack.Core;
using Forms = System.Windows.Forms;

namespace ThreadBack.App;

public partial class MainWindow
{
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr handle, StringBuilder text, int capacity);
    private static string ForegroundWindowTitle()
    {
        var text = new StringBuilder(256);
        return GetWindowText(GetForegroundWindow(), text, text.Capacity) > 0 ? text.ToString().Trim() : "";
    }
    private Forms.NotifyIcon? tray;
    private OverlayWindow? overlay;
    private EyeCaptureFlash? captureFlash;
    private int captureFlashCount;
    private bool quitting, sessionLocked, capturing;
    private int keepSnapshots = 10;
    private readonly DispatcherTimer eyeTimer = new();

    private void InitializeCompanion()
    {
        if (arguments.Any(a => a is "--preview" or "--smoke" or "--capabilities" or "--resume-preview")) return;
        overlay = new OverlayWindow(store.DirectoryPath, RestoreMain, SaveQuickUpdate, () => CaptureScreenAsync(false), ToggleEye);
        overlay.TaskName.Text = "Open a task in ThreadBack";
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Open ThreadBack", null, (_, _) => Dispatcher.Invoke(RestoreMain));
        menu.Items.Add("Show overlay", null, (_, _) => Dispatcher.Invoke(() => overlay.Show()));
        menu.Items.Add("Pause Eye", null, (_, _) => Dispatcher.Invoke(() => ToggleEye(false)));
        menu.Items.Add("Quit", null, (_, _) => Dispatcher.Invoke(() => { RestoreMain(); CloseForQuit(); }));
        using var iconStream = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/ThreadBack.ico")).Stream;
        using var sourceIcon = new System.Drawing.Icon(iconStream, 32, 32);
        var trayIcon = (System.Drawing.Icon)sourceIcon.Clone();
        tray = new Forms.NotifyIcon { Text = "ThreadBack · Eye off", Icon = trayIcon, ContextMenuStrip = menu, Visible = true };
        tray.DoubleClick += (_, _) => Dispatcher.Invoke(RestoreMain);
        eyeTimer.Tick += async (_, _) => await CaptureScreenAsync(true);
        SystemEvents.SessionSwitch += SessionChanged;
        SystemEvents.PowerModeChanged += PowerChanged;
        Closed += (_, _) => { eyeTimer.Stop(); SystemEvents.SessionSwitch -= SessionChanged; SystemEvents.PowerModeChanged -= PowerChanged; captureFlash?.Close(); tray.Dispose(); trayIcon.Dispose(); overlay.Close(); };
    }
    private void CloseForQuit() { if (CanLeave()) { quitting = true; dirty = false; Close(); } }
    private void RestoreMain() { Show(); WindowState = WindowState.Normal; Activate(); }
    private void Overlay_Click(object sender, RoutedEventArgs e) { overlay?.Show(); }
    private void CompanionStatus(string text) { Status(text); if (overlay is not null) overlay.Status.Text = text; }
    private void SessionChanged(object sender, SessionSwitchEventArgs e) => Dispatcher.Invoke(() => { sessionLocked = e.Reason != SessionSwitchReason.SessionUnlock; if (sessionLocked) ToggleEye(false); });
    private void PowerChanged(object sender, PowerModeChangedEventArgs e) { if (e.Mode == PowerModes.Suspend) Dispatcher.Invoke(() => ToggleEye(false)); }
    private void ToggleEye(bool enabled)
    {
        if (overlay is null) return;
        eyeTimer.Stop();
        if (enabled)
        {
            SyncFields();
            if (sessionLocked || string.IsNullOrWhiteSpace(current.Title) || string.IsNullOrWhiteSpace(current.Goal)) { overlay.Eye.IsChecked = false; CompanionStatus("Open a task with a title and goal first."); return; }
            if (!int.TryParse(overlay.Minutes.Text, out var minutes) || minutes < 1 || minutes > 120 || !int.TryParse(overlay.Retention.Text, out keepSnapshots) || keepSnapshots < 1 || keepSnapshots > 30) { overlay.Eye.IsChecked = false; CompanionStatus("Use 1–120 minutes and keep 1–30 snapshots."); return; }
            eyeTimer.Interval = TimeSpan.FromMinutes(minutes); eyeTimer.Start(); overlay.Show();
        }
        overlay.Minutes.IsEnabled = overlay.Retention.IsEnabled = !enabled;
        overlay.Indicate(enabled); if (tray is not null) tray.Text = enabled ? "ThreadBack · Eye ON" : "ThreadBack · Eye off";
        if (!enabled) overlay.Eye.IsChecked = false;
        CompanionStatus(enabled ? "Eye on · primary screen · stops on lock or task switch" : "Eye off");
    }
    private void SaveQuickUpdate()
    {
        if (overlay is null || busy || capturing || recorder is not null) { CompanionStatus("Wait for the current operation to finish."); return; }
        SyncFields();
        overlay!.TaskName.Text = current.Title;
        if (string.IsNullOrWhiteSpace(current.Title)) { CompanionStatus("Give the task a title in the main app first."); return; }
        if (string.IsNullOrWhiteSpace(overlay.Input.Text)) return;
        var item = new EvidenceItem { Kind = "update", Label = "Your update · " + DateTime.Now.ToString("g"), Text = overlay.Input.Text.Trim() };
        current.Evidence.Add(item); InvalidateBrief();
        try { store.Save(current); overlay.Input.Clear(); RenderEvidence(); ShowEdit(); RefreshTasks(); CompanionStatus("Update saved to " + current.Title); }
        catch (Exception ex) { current.Evidence.Remove(item); CompanionStatus("Update not saved: " + ex.Message); }
    }
    private async Task CaptureScreenAsync(bool automatic)
    {
        if (capturing || busy || recorder is not null || sessionLocked) { if (!automatic) CompanionStatus("Capture unavailable while another operation is active or the screen is locked."); return; }
        SyncFields();
        overlay!.TaskName.Text = current.Title;
        if (string.IsNullOrWhiteSpace(current.Title)) { ToggleEye(false); CompanionStatus("Give the task a title before capturing."); return; }
        var target = current;
        var mainVisible = IsVisible; var overlayVisible = overlay?.IsVisible == true;
        capturing = true; SetBusy(true);
        try
        {
            captureFlash?.Close();
            Hide(); overlay?.Hide();
            await Task.Delay(200);
            if (sessionLocked || (automatic && !eyeTimer.IsEnabled)) return;
            var windowTitle = ForegroundWindowTitle();
            var bounds = Forms.Screen.PrimaryScreen!.Bounds;
            using var bitmap = new System.Drawing.Bitmap(bounds.Width, bounds.Height);
            using (var graphics = System.Drawing.Graphics.FromImage(bitmap)) graphics.CopyFromScreen(bounds.Left, bounds.Top, 0, 0, bounds.Size);
            using var stream = new MemoryStream(); bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
            var bytes = stream.ToArray();
            // Restore controls before OCR, which can take longer than the screen copy.
            if (mainVisible) Show(); if (overlayVisible) overlay?.Show();
            if (automatic && !sessionLocked)
            {
                // The screen copy is complete; an indicator failure must not discard it.
                try
                {
                    var flash = new EyeCaptureFlash();
                    captureFlash = flash;
                    flash.Closed += (_, _) => { if (ReferenceEquals(captureFlash, flash)) captureFlash = null; };
                    flash.Show();
                    captureFlashCount++;
                }
                catch { try { captureFlash?.Close(); } catch { } captureFlash = null; }
            }
            string text; bool ocrFailed = false;
            try { text = await ExtractTextAsync(bytes); } catch { text = ""; ocrFailed = true; }
            var item = new EvidenceItem { Kind = automatic ? "eye" : "snapshot", Label = (automatic ? "Eye observation" : "Your snapshot") + " · " + DateTime.Now.ToString("g"), Image = bytes, Text = text, WindowTitle = windowTitle };
            var removed = automatic ? target.Evidence.Where(x => x.Kind == "eye").OrderBy(x => x.CapturedAt).Take(Math.Max(0, target.Evidence.Count(x => x.Kind == "eye") - keepSnapshots + 1)).ToList() : [];
            foreach (var old in removed) target.Evidence.Remove(old);
            target.Evidence.Add(item); InvalidateBrief();
            try { store.Save(target); } catch { target.Evidence.Remove(item); target.Evidence.AddRange(removed); throw; }
            RenderEvidence(); ShowEdit(); RefreshTasks(); CompanionStatus(ocrFailed ? "Snapshot saved; OCR unavailable. Add its relevant text in the app." : "Snapshot saved · extracted text is an observation; review before using.");
        }
        catch (Exception ex) { ToggleEye(false); CompanionStatus("Capture stopped: " + ex.Message); }
        finally { capturing = false; SetBusy(false); if (mainVisible) Show(); if (overlayVisible) overlay?.Show(); }
    }
}


