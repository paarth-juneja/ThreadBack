using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using ThreadBack.Core;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;

namespace ThreadBack.App;

public partial class MainWindow : Window
{
    private readonly CapsuleStore store;
    private readonly LocalGenerator generator = new(LocalGenerator.FindRoot());
    private readonly LocalVision vision = new(LocalGenerator.FindRoot());
    private readonly ModelSettingsStore settingsStore;
    private ModelSettings modelSettings = new();
    private readonly System.Windows.Threading.DispatcherTimer visionIdleTimer = new();
    private readonly System.Windows.Threading.DispatcherTimer textIdleTimer = new();
    private Capsule current = new();
    private bool loading, dirty, busy;
    private CancellationTokenSource? work;
    private readonly string[] arguments;
    private VoiceRecorder? recorder;
    private readonly System.Windows.Threading.DispatcherTimer recordTimer = new() { Interval = TimeSpan.FromSeconds(60) };

    public MainWindow(string[] args)
    {
        arguments = args;
        var testIndex = Array.IndexOf(args, "--test-data");
        store = new CapsuleStore(testIndex >= 0 ? args[testIndex + 1] : null);
        settingsStore = new ModelSettingsStore(testIndex >= 0 ? Path.Combine(args[testIndex + 1], "model-settings.json") : null);
        modelSettings = settingsStore.Load();
        generator.MemoryLimitGb = modelSettings.MemoryLimitGb;
        vision.MemoryLimitGb = modelSettings.MemoryLimitGb;
        ApplyModelBackend();
        InitializeComponent();
        visionIdleTimer.Tick += (_, _) => { visionIdleTimer.Stop(); if (!busy) { vision.Unload(); UpdateUnloadButton(); } };
        textIdleTimer.Tick += (_, _) => { textIdleTimer.Stop(); if (!busy) { generator.Unload(); UpdateUnloadButton(); } };
        Loaded += Window_Loaded;
        Closing += Window_Closing;
        Closed += (_, _) => { visionIdleTimer.Stop(); textIdleTimer.Stop(); work?.Cancel(); recorder?.Dispose(); generator.Dispose(); vision.Dispose(); };
        recordTimer.Tick += async (_, _) => await StopRecordingAsync();
        InputBindings.Add(new KeyBinding(new UiCommand(() => SaveCurrent()), new KeyGesture(Key.S, ModifierKeys.Control)));
        InputBindings.Add(new KeyBinding(new UiCommand(() => NewTask_Click(this, new RoutedEventArgs())), new KeyGesture(Key.N, ModifierKeys.Control)));
        InitializeCompanion();
        LoadCapsule(new Capsule());
        RefreshTasks();
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        if (arguments.Contains("--overlay-review") && arguments.Contains("--test-data"))
        {
            LoadCapsule(CapsuleRules.Demo());
            overlay!.ShowInTaskbar = true;
            overlay.Show();
            return;
        }
        if (arguments.Contains("--companion-check")) { await CheckCompanionAsync(); return; }
        var preview = Array.IndexOf(arguments, "--preview");
        var placementPreview = Array.IndexOf(arguments, "--placement-preview");
        var smoke = Array.IndexOf(arguments, "--smoke");
        var resumePreview = Array.IndexOf(arguments, "--resume-preview");
        var capabilities = Array.IndexOf(arguments, "--capabilities");
        var settingsPreview = Array.IndexOf(arguments, "--settings-preview");
        if (preview < 0 && placementPreview < 0 && smoke < 0 && capabilities < 0 && resumePreview < 0 && settingsPreview < 0) return;
        try
        {
            if (settingsPreview >= 0)
            {
                if (!arguments.Contains("--test-data")) throw new InvalidOperationException("Settings preview requires an isolated test store.");
                var dialog = new ModelSettingsWindow(modelSettings, HardwareProfile.Probe(LocalGenerator.FindRoot())) { Owner = this };
                dialog.Show(); dialog.UpdateLayout();
                var bitmap = new RenderTargetBitmap((int)dialog.ActualWidth, (int)dialog.ActualHeight, 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(dialog);
                var png = new PngBitmapEncoder(); png.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
                using (var output = File.Create(arguments[settingsPreview + 1])) png.Save(output);
                dialog.Close();
                Application.Current.Shutdown(0);
                return;
            }
            LoadCapsule(CapsuleRules.Demo());
            if (placementPreview >= 0) LoadCapsule(new Capsule { Title = "Screenshot task", Goal = "Continue from the screenshots" });
            if (resumePreview >= 0)
            {
                if (!arguments.Contains("--test-data")) throw new InvalidOperationException("Preview requires an isolated test store.");
                var sample = JsonSerializer.Deserialize<Capsule>(File.ReadAllText(arguments[resumePreview + 1]), CapsuleRules.Json) ?? throw new InvalidDataException("Missing sample.");
                CapsuleRules.ValidateInput(sample);
                sample.Brief = CapsuleRules.ParseBrief(JsonSerializer.Serialize(sample.Brief, CapsuleRules.Json), sample);
                store.Save(sample);
                LoadCapsule(store.Load(sample.Id));
                RefreshTasks();
                Status("Recorded local model output · saved and reopened for preview.");
            }
            if (capabilities >= 0)
            {
                var folder = arguments[capabilities + 1];
                var report = new Dictionary<string, object>();
                try { report["ocrText"] = await ExtractTextAsync(await File.ReadAllBytesAsync(Path.Combine(folder, "capture.png"))); }
                catch (Exception ex) { report["ocrError"] = ex.GetType().Name + ": " + ex.Message; }
                try
                {
                    var watch = Stopwatch.StartNew();
                    report["transcript"] = await VoiceTranscriber.TranscribeAsync(await File.ReadAllBytesAsync(Path.Combine(folder, "voice-fixture.wav")), LocalGenerator.FindRoot(), CancellationToken.None);
                    report["transcriptionSeconds"] = watch.Elapsed.TotalSeconds;
                }
                catch (Exception ex) { report["voiceError"] = ex.GetType().Name + ": " + ex.Message; }
                File.WriteAllText(Path.Combine(folder, "capabilities.json"), JsonSerializer.Serialize(report, CapsuleRules.Json));
            }
            if (smoke >= 0)
            {
                var result = await generator.GenerateAsync(current, CancellationToken.None);
                current.Brief = result.Brief; current.GenerationSeconds = result.Seconds; current.GenerationBackend = generator.Backend;
                store.Save(current);
                LoadCapsule(store.Load(current.Id));
                ShowResume();
                File.WriteAllText(arguments[smoke + 1], JsonSerializer.Serialize(current, CapsuleRules.Json));
                Status("Verified: generated locally, encrypted, saved, reopened, and linked to evidence.");
            }
            if (preview >= 0 || placementPreview >= 0)
            {
                UpdateLayout();
                if (placementPreview >= 0) { GenerateButton.BringIntoView(); UpdateLayout(); }
                await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                var bitmap = new RenderTargetBitmap((int)ActualWidth, (int)ActualHeight, 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(this);
                var png = new PngBitmapEncoder(); png.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
                using var output = File.Create(arguments[(placementPreview >= 0 ? placementPreview : preview) + 1]); png.Save(output);
            }
            dirty = false;
            Application.Current.Shutdown(0);
        }
        catch (Exception ex)
        {
            if (smoke >= 0) File.WriteAllText(arguments[smoke + 1] + ".error", ex.ToString());
            dirty = false; Application.Current.Shutdown(1);
        }
    }

    private void Status(string message) => StatusLabel.Text = message;
    private void Draft_Changed(object sender, TextChangedEventArgs e) { if (!loading) { dirty = true; if (sender == GoalBox) current.Brief = null; } }
    private void SyncFields() { current.Title = TitleBox.Text.Trim(); current.Goal = GoalBox.Text.Trim(); }

    private void LoadCapsule(Capsule capsule)
    {
        ToggleEye(false);
        if (overlay is not null) overlay.TaskName.Text = capsule.Title;
        loading = true;
        current = capsule; TitleBox.Text = current.Title == "Untitled task" ? "" : current.Title; GoalBox.Text = current.Goal; NoteBox.Clear();
        RenderEvidence();
        if (current.Brief is not null) ShowResume(); else ShowEdit();
        dirty = false; loading = false;
    }

    private bool CanLeave()
    {
        if (busy || recorder is not null) { Status("Finish or cancel the current operation first."); return false; }
        if (overlay is not null && !string.IsNullOrWhiteSpace(overlay.Input.Text)) { SaveQuickUpdate(); if (!string.IsNullOrWhiteSpace(overlay.Input.Text)) { RestoreMain(); return false; } }
        if (!dirty) return true;
        var choice = MessageBox.Show(this, "Save this handoff before leaving?", "Unsaved changes", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
        return choice == MessageBoxResult.No || (choice == MessageBoxResult.Yes && SaveCurrent());
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (arguments.Contains("--preview") || arguments.Contains("--placement-preview") || arguments.Contains("--settings-preview") || arguments.Contains("--smoke") || arguments.Contains("--capabilities")) return;
        if (tray is not null && !quitting) { e.Cancel = true; Hide(); overlay?.Show(); return; }
        if (busy) { work?.Cancel(); Status("Cancelling. Close the window again when the operation finishes."); e.Cancel = true; return; }
        e.Cancel = !CanLeave();
    }

    private void RefreshTasks()
    {
        var listing = store.List();
        loading = true;
        TaskList.ItemsSource = listing.Capsules.Where(c => c.Title.Contains(SearchBox.Text, StringComparison.OrdinalIgnoreCase)).ToList();
        TaskList.SelectedItem = TaskList.Items.Cast<Capsule>().FirstOrDefault(c => c.Id == current.Id);
        loading = false;
        if (listing.UnreadableFiles > 0) Status($"{listing.UnreadableFiles} saved capsule(s) could not be read. Their files have been preserved.");
    }

    private void Search_Changed(object sender, TextChangedEventArgs e) { if (IsLoaded) RefreshTasks(); }
    private void TaskList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (loading || TaskList.SelectedItem is not Capsule selected || selected.Id == current.Id) return;
        if (!CanLeave()) { RefreshTasks(); return; }
        try { LoadCapsule(store.Load(selected.Id)); Status("Saved capsule reopened. Select a source to check the handoff."); }
        catch (Exception ex) { Status("Could not open capsule: " + ex.Message); }
    }
    private void NewTask_Click(object sender, RoutedEventArgs e) { if (CanLeave()) { LoadCapsule(new Capsule()); RefreshTasks(); TitleBox.Focus(); Status("A fresh thread. Start with your goal."); } }
    private void Demo_Click(object sender, RoutedEventArgs e) { if (CanLeave()) { LoadCapsule(CapsuleRules.Demo()); dirty = true; Status("Example evidence loaded. Click Create my resume capsule to run the real local model."); } }

    private bool SaveCurrent()
    {
        if (busy || recorder is not null) { Status("Finish or cancel the current operation before saving."); return false; }
        try
        {
            SyncFields();
            if (!string.IsNullOrWhiteSpace(NoteBox.Text) && !AddNote()) return false;
            if (string.IsNullOrWhiteSpace(current.Title)) throw new InvalidDataException("Give this task a title before saving.");
            store.Save(current); dirty = false; RefreshTasks(); Status("Saved on this PC, encrypted for your Windows account."); return true;
        }
        catch (Exception ex) { Status(ex.Message); return false; }
    }
    private void Save_Click(object sender, RoutedEventArgs e) => SaveCurrent();
    private void UnloadAi_Click(object sender, RoutedEventArgs e)
    {
        if (busy) return;
        visionIdleTimer.Stop(); textIdleTimer.Stop();
        if (!generator.IsLoaded && !vision.IsLoaded) { Status("The local AI is already unloaded."); UpdateUnloadButton(); return; }
        generator.Unload(); vision.Unload();
        UpdateUnloadButton();
        Status("AI unloaded from RAM. Your capsule and evidence are still saved.");
    }
    private void UpdateUnloadButton() => UnloadAiButton.IsEnabled = !busy && (generator.IsLoaded || vision.IsLoaded);
    private async void Settings_Click(object sender, RoutedEventArgs e)
    {
        if (busy) return;
        SetBusy(true, "Checking this PC's RAM and model devices…");
        HardwareProfile hardware;
        try { hardware = await Task.Run(() => HardwareProfile.Probe(LocalGenerator.FindRoot())); }
        catch (Exception ex) { SetBusy(false); Status("Could not check model devices: " + ex.Message); return; }
        SetBusy(false);
        var dialog = new ModelSettingsWindow(modelSettings, hardware) { Owner = this };
        if (dialog.ShowDialog() != true) return;
        try
        {
            settingsStore.Save(dialog.Selected);
            var memoryChanged = modelSettings.MemoryLimitGb != dialog.Selected.MemoryLimitGb;
            var backendChanged = modelSettings.GpuEnabled != dialog.Selected.GpuEnabled || modelSettings.NpuEnabled != dialog.Selected.NpuEnabled;
            modelSettings = dialog.Selected;
            visionIdleTimer.Stop(); textIdleTimer.Stop();
            if (memoryChanged || backendChanged) { generator.Unload(); vision.Unload(); }
            generator.MemoryLimitGb = modelSettings.MemoryLimitGb;
            vision.MemoryLimitGb = modelSettings.MemoryLimitGb;
            ApplyModelBackend();
            ScheduleModelIdle(vision, visionIdleTimer, modelSettings.VisionIdleMinutes);
            ScheduleModelIdle(generator, textIdleTimer, modelSettings.TextIdleMinutes);
            UpdateUnloadButton();
            Status("Model settings saved on this PC.");
        }
        catch (Exception ex) { Status("Could not save model settings: " + ex.Message); }
    }
    private void ApplyModelBackend()
    {
        var root = LocalGenerator.FindRoot();
        var gpuReady = modelSettings.GpuEnabled && File.Exists(Path.Combine(root, ".tools", "llama-vulkan", "llama-server.exe"));
        var npuReady = modelSettings.NpuEnabled && File.Exists(Path.Combine(root, ".tools", "llama-openvino", "llama-server.exe")) && File.Exists(Path.Combine(root, "artifacts", "verification", "npu-model-verified.json"));
        generator.BackendMode = npuReady ? ModelBackend.OpenVinoNpu : gpuReady ? ModelBackend.VulkanGpu : ModelBackend.Cpu;
        vision.BackendMode = gpuReady ? ModelBackend.VulkanGpu : ModelBackend.Cpu;
    }
    private void ScheduleModelIdle(object model, System.Windows.Threading.DispatcherTimer timer, int minutes)
    {
        timer.Stop();
        var loaded = model is LocalVision image ? image.IsLoaded : ((LocalGenerator)model).IsLoaded;
        if (!loaded) return;
        if (minutes == 0) { if (model is LocalVision visionModel) visionModel.Unload(); else ((LocalGenerator)model).Unload(); return; }
        if (minutes > 0) { timer.Interval = TimeSpan.FromMinutes(minutes); timer.Start(); }
    }
    private void EditTab_Click(object sender, RoutedEventArgs e) => ShowEdit();
    private void ResumeTab_Click(object sender, RoutedEventArgs e) { SyncFields(); ShowResume(); }
    private void ShowEdit() { EditPanel.Visibility = Visibility.Visible; ResumePanel.Visibility = Visibility.Collapsed; PageHeading.Text = "Pause with a clear next step."; }
    private void ShowResume()
    {
        EditPanel.Visibility = Visibility.Collapsed; ResumePanel.Visibility = Visibility.Visible;
        PageHeading.Text = "Pick up the thought."; ResumeGoal.Text = string.IsNullOrWhiteSpace(current.Goal) ? "Your task is waiting for a goal." : current.Goal;
        ResumeFocus.Text = current.Brief is null ? "" : string.Join("  ·  ", new[]
        {
            current.Brief.LastState.FirstOrDefault()?.Text is { } state ? "At pause: " + state : null,
            current.Brief.NextActions.FirstOrDefault()?.Text is { } action ? "Next: " + action : null
        }.Where(x => x is not null));
        RuntimeLabel.Text = current.Brief is null ? "Evidence saved · create a capsule when you are ready" : current.Brief.Activity.Count == 0 ? "Older handoff · regenerate from saved sources for the activity timeline" : $"{current.GenerationBackend} · {current.GenerationSeconds:0.0}s generation · review required";
        BriefCards.Children.Clear(); ActivityTimeline.Children.Clear();
        if (current.Brief is null) { BriefCards.Children.Add(new TextBlock { Text = "Your sources are ready. Return to Capture a handoff to generate the resume view.", Margin = new Thickness(0, 10, 0, 25) }); return; }
        if (current.Brief.Activity.Count == 0) ActivityTimeline.Children.Add(new TextBlock { Text = "No activity description yet. Add a short note about what you were doing.", Foreground = Brushes.Gray });
        foreach (var activity in current.Brief.Activity)
        {
            var source = current.Evidence.FirstOrDefault(x => activity.EvidenceIds.Contains(x.Id));
            var row = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };
            row.Children.Add(new TextBlock { Text = source is null ? "Source unavailable" : $"{source.Label} · {source.CapturedAt.ToLocalTime():g}", FontSize = 11, Foreground = Brush("#78678F") });
            var wording = new TextBox { Text = activity.Text, TextWrapping = TextWrapping.Wrap, AcceptsReturn = true, MaxLength = 600, BorderThickness = new Thickness(0), Background = Brushes.Transparent, Padding = new Thickness(0, 4, 0, 3) };
            wording.TextChanged += (_, _) => { activity.Text = wording.Text; activity.UserEdited = true; dirty = true; };
            row.Children.Add(wording);
            if (source is not null)
            {
                var inspect = new Button { Content = "↗ Check " + source.Label, FontSize = 11, HorizontalAlignment = HorizontalAlignment.Left, Padding = new Thickness(8, 4, 8, 4) };
                inspect.Click += (_, _) => InspectEvidence(source);
                row.Children.Add(inspect);
            }
            ActivityTimeline.Children.Add(row);
        }
        AddBriefSection("01", "Where you left off", current.Brief.LastState, false);
        AddBriefSection("02", "Your next small step", current.Brief.NextActions, true);
        if (current.Brief.Decisions.Count > 0) AddBriefSection("03", "Decisions worth keeping", current.Brief.Decisions, false);
        if (current.Brief.OpenQuestions.Count > 0) AddBriefSection("04", "What still needs resolving", current.Brief.OpenQuestions, false);
    }

    private void AddBriefSection(string number, string title, List<Claim> claims, bool suggested)
    {
        var panel = new StackPanel();
        panel.Children.Add(new TextBlock { Text = number + "   " + title + (suggested ? "  ·  SUGGESTED" : ""), FontSize = 17, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 10) });
        if (claims.Count == 0) panel.Children.Add(new TextBlock { Text = "Not established by the evidence.", Foreground = Brushes.Gray, FontSize = 13 });
        foreach (var claim in claims)
        {
            var origin = new TextBlock { Text = claim.UserEdited ? "Edited by you · recheck the source" : claim.Text == claim.SourceQuote ? "Source wording · AI category · review" : "AI draft · check the source", FontSize = 10, Foreground = Brush("#867A94"), Margin = new Thickness(0, 3, 0, 3) };
            panel.Children.Add(origin);
            var edit = new TextBox { Text = claim.Text, TextWrapping = TextWrapping.Wrap, AcceptsReturn = true, MaxLength = 600, Background = Brushes.Transparent, BorderThickness = new Thickness(0), Padding = new Thickness(0, 3, 0, 5) };
            edit.TextChanged += (_, _) => { claim.Text = edit.Text; claim.UserEdited = true; origin.Text = "Edited by you · recheck the source"; dirty = true; };
            panel.Children.Add(edit);
            var links = new WrapPanel { Margin = new Thickness(0, 0, 0, 10) };
            foreach (var id in claim.EvidenceIds)
            {
                var evidence = current.Evidence.FirstOrDefault(x => x.Id == id);
                if (evidence is null) continue;
                var link = new Button { Content = "↗ " + evidence.Label, FontSize = 11, Padding = new Thickness(9, 5, 9, 5), Margin = new Thickness(0, 3, 7, 0) };
                link.Click += (_, _) => InspectEvidence(evidence); links.Children.Add(link);
            }
            var move = new Button { Content = "Move…", FontSize = 11, Padding = new Thickness(9, 5, 9, 5), Margin = new Thickness(0, 3, 7, 0) };
            var menu = new ContextMenu();
            foreach (var destination in new[] { ("Next step", current.Brief!.NextActions), ("Decision", current.Brief.Decisions), ("Last state", current.Brief.LastState), ("Open question", current.Brief.OpenQuestions) })
            {
                var choice = new MenuItem { Header = destination.Item1, IsEnabled = destination.Item2 != claims };
                choice.Click += (_, _) => { claims.Remove(claim); destination.Item2.Add(claim); claim.UserEdited = true; dirty = true; ShowResume(); };
                menu.Items.Add(choice);
            }
            move.Click += (_, _) => { menu.PlacementTarget = move; menu.IsOpen = true; };
            links.Children.Add(move);
            panel.Children.Add(links);
        }
        BriefCards.Children.Add(new Border { Style = (Style)FindResource("Card"), Margin = new Thickness(0, 0, 12, 14), Child = panel });
    }

    private bool AddNote()
    {
        if (string.IsNullOrWhiteSpace(NoteBox.Text)) return true;
        if (current.Evidence.Count(x => x.Kind == "note") >= 3) { Status("Three notes are already attached. Edit an existing note or remove one first."); return false; }
        current.Evidence.Add(new EvidenceItem { Label = $"Note {current.Evidence.Count(x => x.Kind == "note") + 1}", Text = NoteBox.Text.Trim() });
        NoteBox.Clear(); InvalidateBrief(); RenderEvidence(); return true;
    }
    private void AddNote_Click(object sender, RoutedEventArgs e) { if (!busy) AddNote(); }
    private void InvalidateBrief() { current.Brief = null; current.GenerationSeconds = null; dirty = true; }

    private void RenderEvidence()
    {
        EvidenceEditor.Children.Clear(); EvidenceCount.Text = $"{current.Evidence.Count} sources";
        DescribeAllButton.IsEnabled = vision.IsInstalled && current.Evidence.Any(e => e.Image is { Length: > 0 } && string.IsNullOrWhiteSpace(e.VisualDescription));
        foreach (var evidence in current.Evidence)
        {
            var panel = new StackPanel();
            var heading = new DockPanel();
            var remove = new Button { Content = "Remove", FontSize = 11, Padding = new Thickness(8, 4, 8, 4), HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(10, 0, 0, 0) };
            DockPanel.SetDock(remove, Dock.Right);
            remove.Click += (_, _) => { current.Evidence.Remove(evidence); InvalidateBrief(); RenderEvidence(); };
            heading.Children.Add(remove);
            heading.Children.Add(new TextBlock { Text = evidence.Label + " · " + evidence.Kind.ToUpperInvariant(), FontSize = 12, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center });
            panel.Children.Add(heading);
            if (evidence.Image is not null)
            {
                var img = new Image { Source = ToBitmap(evidence.Image), MaxHeight = 160, Stretch = Stretch.Uniform, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 10, 0, 0) };
                panel.Children.Add(img);
                if (vision.IsInstalled)
                {
                    var actions = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) };
                    var describe = new Button { Content = "Describe image locally", FontSize = 11 };
                    describe.Click += async (_, _) => await DescribeImageAsync(evidence);
                    actions.Children.Add(describe);
                    panel.Children.Add(actions);
                }
                if (!string.IsNullOrWhiteSpace(evidence.VisualDescription)) panel.Children.Add(new TextBlock { Text = "AI image description · review: " + evidence.VisualDescription, TextWrapping = TextWrapping.Wrap, FontSize = 11, Foreground = Brush("#78678F"), Margin = new Thickness(0, 7, 0, 0) });
                if (!string.IsNullOrWhiteSpace(evidence.WindowTitle)) panel.Children.Add(new TextBlock { Text = "Window: " + evidence.WindowTitle, FontSize = 11, Foreground = Brush("#78678F"), Margin = new Thickness(0, 6, 0, 0) });
                panel.Children.Add(new TextBlock { Text = "What were you doing here? Add context if the image alone cannot show it.", FontSize = 11, Foreground = Brush("#78678F"), Margin = new Thickness(0, 8, 0, 3) });
                var context = new TextBox { Text = evidence.Context, MaxLength = 500, TextWrapping = TextWrapping.Wrap, AcceptsReturn = true, MinHeight = 42, ToolTip = "Your explanation of this screenshot. The AI treats this as your statement, not as OCR." };
                context.TextChanged += (_, _) => { evidence.Context = context.Text; InvalidateBrief(); };
                panel.Children.Add(context);
            }
            if (evidence.Image is not null) panel.Children.Add(new TextBlock { Text = "Text detected in the image (may include unrelated screen details)", FontSize = 11, Foreground = Brush("#78678F"), Margin = new Thickness(0, 8, 0, 0) });
            var text = new TextBox { Text = evidence.Text, TextWrapping = TextWrapping.Wrap, AcceptsReturn = true, MinHeight = 55, MaxHeight = 150, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, MaxLength = 8000, Margin = new Thickness(0, 10, 0, 0) };
            text.TextChanged += (_, _) => { evidence.Text = text.Text; InvalidateBrief(); };
            panel.Children.Add(text);
            panel.Children.Add(new TextBlock { Text = "Optional source link — paste the page URL if you want to reopen it later. Leave blank for Eye screenshots.", FontSize = 11, Foreground = Brush("#78678F"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 3) });
            var source = new TextBox { Text = evidence.SourceUrl ?? "", ToolTip = "Optional web link to the original source. This is not your activity description.", FontSize = 11 };
            source.TextChanged += (_, _) => { evidence.SourceUrl = source.Text.Trim(); dirty = true; };
            panel.Children.Add(source);
            if (evidence.Audio is not null)
            {
                var retry = new Button { Content = "Transcribe attached audio", HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 8, 0, 0), FontSize = 11 };
                retry.Click += async (_, _) => { if (!busy) await TranscribeBytesAsync(evidence.Audio, evidence); };
                panel.Children.Add(retry);
            }
            EvidenceEditor.Children.Add(new Border { Background = Brush("#F7F5FA"), CornerRadius = new CornerRadius(8), Padding = new Thickness(13), Margin = new Thickness(0, 0, 0, 10), Child = panel });
        }
    }

    private async Task DescribeImageAsync(EvidenceItem evidence)
    {
        if (busy || evidence.Image is null) return;
        SyncFields();
        work = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        SetBusy(true, "Looking at the image on this PC… first load may take a few minutes.");
        try
        {
            visionIdleTimer.Stop(); textIdleTimer.Stop();
            await Task.Run(generator.Unload);
            var description = await Task.Run(() => vision.DescribeAsync(evidence.Image, work.Token, current.Goal, evidence.Text, evidence.Context), work.Token);
            evidence.VisualDescription = description;
            InvalidateBrief(); RenderEvidence(); SyncFields();
            if (!string.IsNullOrWhiteSpace(current.Title)) { await Task.Run(() => store.Save(current)); dirty = !string.IsNullOrWhiteSpace(NoteBox.Text); RefreshTasks(); }
            Status("Image described locally. Review the description, then regenerate the handoff.");
        }
        catch (OperationCanceledException) { Status("Image description stopped. The image and your notes are unchanged."); }
        catch (Exception ex) { Status("Could not describe image: " + ex.Message); }
        finally { work.Dispose(); work = null; SetBusy(false); ScheduleModelIdle(vision, visionIdleTimer, modelSettings.VisionIdleMinutes); UpdateUnloadButton(); }
    }

    private async void DescribeAll_Click(object sender, RoutedEventArgs e)
    {
        if (busy) return;
        SyncFields();
        var pending = current.Evidence.Where(item => item.Image is { Length: > 0 } && string.IsNullOrWhiteSpace(item.VisualDescription)).ToList();
        if (pending.Count == 0) { Status("Every screenshot already has an image description."); return; }
        work = new CancellationTokenSource();
        SetBusy(true, $"Describing 1 of {pending.Count} screenshots locally… first load may take a few minutes.");
        var completed = 0;
        try
        {
            visionIdleTimer.Stop(); textIdleTimer.Stop();
            await Task.Run(generator.Unload);
            foreach (var item in pending)
            {
                work.Token.ThrowIfCancellationRequested();
                Status($"Describing screenshot {completed + 1} of {pending.Count} locally… Cancel is available.");
                using var imageDeadline = CancellationTokenSource.CreateLinkedTokenSource(work.Token);
                imageDeadline.CancelAfter(TimeSpan.FromMinutes(5));
                item.VisualDescription = await Task.Run(() => vision.DescribeAsync(item.Image!, imageDeadline.Token, current.Goal, item.Text, item.Context), imageDeadline.Token);
                completed++;
                InvalidateBrief();
                SyncFields();
                if (!string.IsNullOrWhiteSpace(current.Title))
                {
                    await Task.Run(() => store.Save(current));
                    dirty = !string.IsNullOrWhiteSpace(NoteBox.Text);
                    RefreshTasks();
                }
                Status($"Described {completed} of {pending.Count} screenshots locally.");
            }
            Status($"Described {completed} screenshots. Review the descriptions and add your context, then create the capsule.");
        }
        catch (OperationCanceledException)
        {
            Status(work.IsCancellationRequested
                ? $"Stopped after {completed} of {pending.Count} screenshots. Completed descriptions were kept."
                : $"Image {completed + 1} took over five minutes. Stopped; {completed} completed descriptions were kept.");
        }
        catch (Exception ex) { Status($"Stopped at screenshot {completed + 1}: {ex.Message} Completed descriptions were kept."); }
        finally
        {
            work.Dispose(); work = null;
            RenderEvidence();
            SetBusy(false); UpdateUnloadButton();
            ScheduleModelIdle(vision, visionIdleTimer, modelSettings.VisionIdleMinutes);
        }
    }

    private async void AddScreenshot_Click(object sender, RoutedEventArgs e)
    {
        if (busy) return;
        if (current.Evidence.Count(x => x.Kind == "image") >= 3) { Status("Three screenshots are already attached."); return; }
        var dialog = new OpenFileDialog { Filter = "Screenshots|*.png;*.jpg;*.jpeg", Title = "Choose a screenshot to include" };
        if (dialog.ShowDialog(this) != true) return;
        SetBusy(true, "Reading the selected screenshot locally…");
        try
        {
            if (new FileInfo(dialog.FileName).Length > 10 * 1024 * 1024) throw new InvalidDataException("Choose an image smaller than 10 MB.");
            var bytes = await File.ReadAllBytesAsync(dialog.FileName);
            var image = ToBitmap(bytes);
            if ((long)image.PixelWidth * image.PixelHeight > 32_000_000) throw new InvalidDataException("Choose an image smaller than 32 megapixels.");
            var evidence = new EvidenceItem { Kind = "image", Label = Path.GetFileName(dialog.FileName), Image = bytes };
            string message;
            try { evidence.Text = await ExtractTextAsync(bytes); message = "Screenshot added. Review the extracted text before generating."; }
            catch { message = "Screenshot preserved. OCR is unavailable in this installation; enter its relevant text in the box below."; }
            current.Evidence.Add(evidence); InvalidateBrief(); RenderEvidence(); Status(message);
        }
        catch (Exception ex) { Status(ex.Message); }
        finally { SetBusy(false); }
    }

    private static async Task<string> ExtractTextAsync(byte[] bytes)
    {
        using var stream = new InMemoryRandomAccessStream();
        using (var writer = new DataWriter(stream.GetOutputStreamAt(0))) { writer.WriteBytes(bytes); await writer.StoreAsync(); await writer.FlushAsync(); }
        stream.Seek(0);
        var decoder = await Windows.Graphics.Imaging.BitmapDecoder.CreateAsync(stream);
        var max = OcrEngine.MaxImageDimension;
        var scale = Math.Min(1.0, (double)max / Math.Max(decoder.PixelWidth, decoder.PixelHeight));
        var transform = new BitmapTransform { ScaledWidth = (uint)(decoder.PixelWidth * scale), ScaledHeight = (uint)(decoder.PixelHeight * scale) };
        using var bitmap = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied, transform, ExifOrientationMode.IgnoreExifOrientation, ColorManagementMode.DoNotColorManage);
        var engine = OcrEngine.TryCreateFromLanguage(new Windows.Globalization.Language("en-US")) ?? throw new InvalidOperationException("English OCR is unavailable.");
        var result = await engine.RecognizeAsync(bitmap);
        return string.Join(Environment.NewLine, result.Lines.Select(line => line.Text));
    }

    private async void Generate_Click(object sender, RoutedEventArgs e)
    {
        if (busy || recorder is not null) return;
        if (!AddNote()) return;
        SyncFields();
        var generationContext = ThreadBack.Core.ActivityContext.ForGeneration(current);
        try { CapsuleRules.ValidateInput(generationContext); }
        catch (Exception ex) { Status(ex.Message); return; }
        work = new CancellationTokenSource(TimeSpan.FromMinutes(10));
        var elapsed = Stopwatch.StartNew();
        var stage = "Preparing recent evidence";
        var progress = new Progress<string>(message => stage = message);
        var ticker = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        ticker.Tick += (_, _) => Status($"{stage} · {elapsed.Elapsed.TotalSeconds:0}s elapsed · Cancel is available");
        ticker.Start();
        SetBusy(true, "Preparing recent evidence…");
        try
        {
            visionIdleTimer.Stop(); textIdleTimer.Stop();
            await Task.Run(vision.Unload);
            await Task.Run(() => store.Save(current));
            dirty = !string.IsNullOrWhiteSpace(NoteBox.Text); RefreshTasks();
            var result = await Task.Run(() => generator.GenerateAsync(generationContext, work.Token, progress), work.Token);
            current.Brief = result.Brief; current.GenerationSeconds = result.Seconds; current.GenerationBackend = generator.Backend;
            await Task.Run(() => store.Save(current)); dirty = !string.IsNullOrWhiteSpace(NoteBox.Text); RefreshTasks();
            if (string.IsNullOrWhiteSpace(NoteBox.Text)) ShowResume();
            Status(dirty
                ? $"Capsule ready in {elapsed.Elapsed.TotalSeconds:0.0}s. Your new note draft is still here; open Resume your task to review."
                : $"Capsule ready in {elapsed.Elapsed.TotalSeconds:0.0}s. Review the result.");
        }
        catch (OperationCanceledException) { Status(elapsed.Elapsed >= TimeSpan.FromMinutes(10) - TimeSpan.FromSeconds(1) ? "Stopped after ten minutes. Your evidence is saved. Add a short progress note and try again." : "Generation cancelled. Your evidence was saved before starting."); }
        catch (Exception ex) { Status(ex.Message); }
        finally { ticker.Stop(); work.Dispose(); work = null; SetBusy(false); ScheduleModelIdle(generator, textIdleTimer, modelSettings.TextIdleMinutes); UpdateUnloadButton(); }
    }
    private void Cancel_Click(object sender, RoutedEventArgs e) => work?.Cancel();
    private void LockEditingControls(DependencyObject root, bool locked)
    {
        if (root is TextBox textBox && !ReferenceEquals(textBox, NoteBox)) textBox.IsReadOnly = locked;
        if (root is Button button) button.IsEnabled = !locked;
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
            LockEditingControls(VisualTreeHelper.GetChild(root, index), locked);
    }
    private void SetBusy(bool value, string? message = null)
    {
        busy = value;
        LockEditingControls(EditPanel, value);
        LockEditingControls(ResumePanel, value);
        if (!value) DescribeAllButton.IsEnabled = vision.IsInstalled && current.Evidence.Any(e => e.Image is { Length: > 0 } && string.IsNullOrWhiteSpace(e.VisualDescription));
        TaskList.IsEnabled = !value;
        SettingsButton.IsEnabled = !value;
        EditTab.IsEnabled = !value; ResumeTab.IsEnabled = !value;
        UnloadAiButton.IsEnabled = !value && (generator.IsLoaded || vision.IsLoaded);
        CancelButton.Visibility = Visibility.Collapsed;
        FooterCancel.Visibility = value && work is not null ? Visibility.Visible : Visibility.Collapsed;
        BusyBar.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
        if (message is not null) Status(message);
    }

    private void InspectEvidence(EvidenceItem evidence)
    {
        var panel = new StackPanel { Margin = new Thickness(24) };
        panel.Children.Add(new TextBlock { Text = evidence.Label, FontSize = 22, FontWeight = FontWeights.SemiBold });
        panel.Children.Add(new TextBlock { Text = evidence.CapturedAt.ToLocalTime().ToString("f"), Foreground = Brushes.Gray, Margin = new Thickness(0, 8, 0, 16) });
        if (evidence.Image is not null) panel.Children.Add(new Image { Source = ToBitmap(evidence.Image), MaxHeight = 330, Stretch = Stretch.Uniform });
        if (!string.IsNullOrWhiteSpace(evidence.WindowTitle)) panel.Children.Add(new TextBlock { Text = "Window: " + evidence.WindowTitle, Margin = new Thickness(0, 10, 0, 0) });
        if (!string.IsNullOrWhiteSpace(evidence.VisualDescription)) panel.Children.Add(new TextBlock { Text = "AI image description (review): " + evidence.VisualDescription, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 10, 0, 0) });
        if (!string.IsNullOrWhiteSpace(evidence.Context)) panel.Children.Add(new TextBlock { Text = "Your context: " + evidence.Context, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 10, 0, 0) });
        panel.Children.Add(new TextBox { Text = evidence.Text, IsReadOnly = true, TextWrapping = TextWrapping.Wrap, BorderThickness = new Thickness(0), Margin = new Thickness(0, 15, 0, 10) });
        if (Uri.TryCreate(evidence.SourceUrl, UriKind.Absolute, out var uri) && (uri.Scheme == "http" || uri.Scheme == "https"))
        {
            var open = new Button { Content = "Open source: " + uri.Host, HorizontalAlignment = HorizontalAlignment.Left };
            open.Click += (_, _) => { try { Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true }); } catch (Exception ex) { Status(ex.Message); } };
            panel.Children.Add(open);
        }
        new Window { Owner = this, Title = "Source evidence", Width = 720, Height = 610, WindowStartupLocation = WindowStartupLocation.CenterOwner, Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }, Background = Brush("#FAF8FC") }.ShowDialog();
    }
    private void ReviewSources_Click(object sender, RoutedEventArgs e) => ShowEdit();
    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (busy) return;
        if (MessageBox.Show(this, "Permanently delete this capsule and its stored sources? Exported copies are not affected.", "Delete capsule", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        try { store.Delete(current.Id); dirty = false; LoadCapsule(new Capsule()); RefreshTasks(); Status("Capsule and its stored sources deleted."); }
        catch (Exception ex) { Status(ex.Message); }
    }
    private void Export_Click(object sender, RoutedEventArgs e)
    {
        if (current.Brief is null) return;
        if (MessageBox.Show(this, "Export the handoff and source text as an unencrypted Markdown file? Review the capsule first; screenshots are not included.", "Export handoff", MessageBoxButton.OKCancel) != MessageBoxResult.OK) return;
        var dialog = new SaveFileDialog { Filter = "Markdown|*.md", FileName = "ThreadBack-handoff.md" };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            var text = new StringBuilder($"# {current.Title}\n\nGoal: {current.Goal}\n\n");
            text.AppendLine("## What you were doing").AppendLine();
            foreach (var activity in current.Brief.Activity) text.AppendLine("- " + activity.Text + " [Source: " + (current.Evidence.FirstOrDefault(x => activity.EvidenceIds.Contains(x.Id))?.Label ?? "Missing") + "]");
            text.AppendLine();
            foreach (var section in new[] { ("Last state", current.Brief.LastState), ("Decisions", current.Brief.Decisions), ("Open questions", current.Brief.OpenQuestions), ("Suggested next steps", current.Brief.NextActions) })
            { text.AppendLine("## " + section.Item1).AppendLine(); foreach (var claim in section.Item2) text.AppendLine("- " + claim.Text + " [Sources: " + string.Join(", ", claim.EvidenceIds.Select(id => current.Evidence.FirstOrDefault(x => x.Id == id)?.Label ?? "Missing")) + "]"); text.AppendLine(); }
            text.AppendLine("## Sources").AppendLine();
            foreach (var evidence in current.Evidence)
            {
                text.AppendLine("### " + evidence.Label).AppendLine();
                if (!string.IsNullOrWhiteSpace(evidence.Context)) text.AppendLine("Your context: " + evidence.Context).AppendLine();
                if (!string.IsNullOrWhiteSpace(evidence.VisualDescription)) text.AppendLine("AI image description (review): " + evidence.VisualDescription).AppendLine();
                if (!string.IsNullOrWhiteSpace(evidence.WindowTitle)) text.AppendLine("Window: " + evidence.WindowTitle).AppendLine();
                text.AppendLine(evidence.Text).AppendLine();
            }
            File.WriteAllText(dialog.FileName, text.ToString()); Status("Handoff exported. This copy is outside ThreadBack's encrypted storage.");
        }
        catch (Exception ex) { Status(ex.Message); }
    }

    private async void AddVoice_Click(object sender, RoutedEventArgs e)
    {
        if (busy || recorder is not null) return;
        if (current.Evidence.Any(x => x.Kind == "voice")) { Status("Remove the existing voice handoff before adding another."); return; }
        var dialog = new OpenFileDialog { Filter = "Voice recording|*.wav;*.m4a", Title = "Select a voice handoff (60 seconds maximum)" };
        if (dialog.ShowDialog(this) == true) await TranscribeAsync(dialog.FileName);
    }
    private async void Record_Click(object sender, RoutedEventArgs e)
    {
        if (recorder is not null) { await StopRecordingAsync(); return; }
        if (busy) return;
        if (current.Evidence.Any(x => x.Kind == "voice")) { Status("Remove the existing voice handoff before recording another."); return; }
        try { recorder = new VoiceRecorder(); recorder.Start(); RecordButton.Content = "■ Stop recording"; recordTimer.Start(); Status("Recording your handoff. Click Stop; recording ends automatically after 60 seconds."); }
        catch (Exception ex) { recorder?.Dispose(); recorder = null; Status("Microphone unavailable: " + ex.Message); }
    }
    private async Task StopRecordingAsync()
    {
        recordTimer.Stop();
        var active = recorder; if (active is null) return;
        recorder = null; RecordButton.Content = "● Record";
        try
        {
            var bytes = await active.StopAsync();
            await TranscribeBytesAsync(bytes);
        }
        catch (Exception ex) { Status("Recording could not be processed: " + ex.Message); }
        finally { active.Dispose(); }
    }
    private async Task TranscribeAsync(string path)
    {
        try
        {
            if (new FileInfo(path).Length > 24 * 1024 * 1024) throw new InvalidDataException("Choose a voice file smaller than 24 MB.");
            var bytes = Path.GetExtension(path).Equals(".m4a", StringComparison.OrdinalIgnoreCase)
                ? await Task.Run(() => VoiceTranscriber.DecodeM4a(path))
                : await File.ReadAllBytesAsync(path);
            await TranscribeBytesAsync(bytes);
        }
        catch (Exception ex) { Status(ex.Message); }
    }
    private async Task TranscribeBytesAsync(byte[] bytes, EvidenceItem? existing = null)
    {
        try { VoiceTranscriber.Validate(bytes); }
        catch (Exception ex) { Status(ex.Message); return; }
        var evidence = existing ?? new EvidenceItem { Kind = "voice", Label = "Voice handoff", Audio = bytes };
        if (existing is null) current.Evidence.Add(evidence);
        InvalidateBrief(); RenderEvidence();
        work = new CancellationTokenSource(); SetBusy(true, "Transcribing the voice handoff locally…");
        try
        {
            var result = await VoiceTranscriber.TranscribeAsync(bytes, LocalGenerator.FindRoot(), work.Token);
            evidence.Text = result;
            InvalidateBrief(); RenderEvidence(); Status("Voice transcript added. Review it before generating; audio is retained with this capsule.");
        }
        catch (OperationCanceledException) { Status("Transcription cancelled. Audio remains attached; type its handoff text or retry from the source card."); }
        catch (InvalidDataException ex) { Status(ex.Message + " You can enter its text manually."); }
        catch (Exception ex) { Status(ex.Message + " Audio remains attached; you can enter its text manually."); }
        finally { work.Dispose(); work = null; SetBusy(false); }
    }

    private static BitmapImage ToBitmap(byte[] bytes)
    {
        using var headerStream = new MemoryStream(bytes);
        var header = System.Windows.Media.Imaging.BitmapDecoder.Create(headerStream, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None).Frames[0];
        if ((long)header.PixelWidth * header.PixelHeight > 32_000_000) throw new InvalidDataException("Choose an image smaller than 32 megapixels.");
        using var stream = new MemoryStream(bytes); var bitmap = new BitmapImage(); bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad; bitmap.StreamSource = stream; bitmap.EndInit(); bitmap.Freeze(); return bitmap;
    }
    private static SolidColorBrush Brush(string hex) => (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!;
}

internal sealed class UiCommand(Action execute) : ICommand
{
    public event EventHandler? CanExecuteChanged { add { } remove { } }
    public bool CanExecute(object? parameter) => true;
    public void Execute(object? parameter) => execute();
}






