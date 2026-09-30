using System.IO;
using System.Text.Json;
using System.Windows;
using ThreadBack.Core;

namespace ThreadBack.App;
public partial class MainWindow
{
    private async Task CheckCompanionAsync()
    {
        var reportPath = arguments[Array.IndexOf(arguments, "--companion-check") + 1];
        var passed = new List<string>();
        void Check(bool condition, string name) { if (!condition) throw new Exception(name); passed.Add(name); }
        try
        {
            if (!arguments.Contains("--test-data")) throw new Exception("Requires isolated storage");
            LoadCapsule(CapsuleRules.Demo());
            overlay!.Show(); overlay.Input.Text = "Finished the example; next compare results.";
            SaveQuickUpdate();
            Check(store.Load(current.Id).Evidence.Any(x => x.Kind == "update"), "Quick update persisted");
            Check(overlay.Input.Text == "", "Saved input cleared");
            overlay.Minutes.Text = "0"; overlay.Eye.IsChecked = true;
            Check(!eyeTimer.IsEnabled && overlay.Eye.IsChecked == false, "Invalid interval rejected");
            overlay.Minutes.Text = "1"; overlay.Retention.Text = "1"; overlay.Eye.IsChecked = true;
            Check(eyeTimer.IsEnabled, "Eye enabled with valid interval");
            await CaptureScreenAsync(true);
            Check(current.Evidence.Count(x => x.Kind == "eye") == 1, "Native screen capture and encrypted save succeeded");
            Check(captureFlashCount == 1, "Eye capture showed a screen-edge confirmation");
            await CaptureScreenAsync(true);
            Check(current.Evidence.Count(x => x.Kind == "eye") == 1, "Eye retention enforced");
            Check(captureFlashCount == 2, "Each Eye capture shows a confirmation");
            await CaptureScreenAsync(false);
            Check(current.Evidence.Count(x => x.Kind == "snapshot") == 1, "Manual capture retained separately");
            Check(captureFlashCount == 2 && captureFlash is null, "Manual capture does not show Eye confirmation");
            var context = ThreadBack.Core.ActivityContext.ForGeneration(current);
            CapsuleRules.ValidateInput(context);
            Check(context.Evidence.All(e => current.Evidence.Any(x => x.Id == e.Id && x.Text.Contains(e.Text, StringComparison.Ordinal))), "Generation context preserves source IDs and exact excerpts");
            Close(); Check(!IsVisible && tray!.Visible, "Close hides main window and keeps tray alive"); RestoreMain();
            var priorId = current.Id;
            LoadCapsule(new Capsule());
            Check(!eyeTimer.IsEnabled && overlay.Eye.IsChecked == false, "Task switch stops Eye");
            store.Delete(priorId);
            File.WriteAllText(reportPath, JsonSerializer.Serialize(new { passed, success = true }, CapsuleRules.Json));
        }
        catch (Exception ex) { File.WriteAllText(reportPath, JsonSerializer.Serialize(new { passed, success = false, error = ex.ToString() }, CapsuleRules.Json)); }
        finally { dirty = false; quitting = true; Application.Current.Shutdown(); }
    }
}
