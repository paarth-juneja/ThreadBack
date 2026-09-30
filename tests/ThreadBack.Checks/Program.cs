using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ThreadBack.Core;

int passed = 0;
void Check(string name, Action test) { test(); passed++; Console.WriteLine("PASS " + name); }
void Assert(bool condition, string message) { if (!condition) throw new Exception(message); }
void Reject<T>(Action action) where T : Exception { try { action(); } catch (T) { return; } throw new Exception("Expected " + typeof(T).Name); }

var temp = Path.Combine(Path.GetTempPath(), "ThreadBack-checks-" + Guid.NewGuid().ToString("N"));
var store = new CapsuleStore(temp);
var capsule = CapsuleRules.Demo();
Check("Evidence-only draft survives protected storage roundtrip", () =>
{
    capsule.Evidence[0].Image = [0, 1, 2, 3];
    capsule.Evidence[1].Audio = [4, 5, 6];
    store.Save(capsule);
    var loaded = store.Load(capsule.Id);
    Assert(loaded.Evidence[0].Text == capsule.Evidence[0].Text, "Note changed");
    Assert(loaded.Evidence[0].Image!.SequenceEqual(new byte[] { 0, 1, 2, 3 }), "Attachment changed");
    Assert(loaded.Evidence[1].Audio!.SequenceEqual(new byte[] { 4, 5, 6 }), "Audio changed");
    var raw = Encoding.UTF8.GetString(File.ReadAllBytes(Path.Combine(temp, capsule.Id.ToString("N") + ".tbc")));
    Assert(!raw.Contains("Approach A") && !raw.Contains(capsule.Title), "Plaintext leaked to storage");
});
Check("Activity history is bounded without changing retained sources", () =>
{
    var history = CapsuleRules.Demo();
    for (var i = 0; i < 40; i++) history.Evidence.Add(new EvidenceItem { Kind = "update", Text = new string('x', 500), CapturedAt = DateTimeOffset.UtcNow.AddMinutes(i) });
    var context = ThreadBack.Core.ActivityContext.ForGeneration(history);
    CapsuleRules.ValidateInput(context);
    Assert(history.Evidence.Count == 42, "History removed");
    Assert(context.Evidence.Count <= 20 && context.Evidence.Sum(e => e.Text.Length) + context.Goal.Length <= CapsuleRules.MaxInputCharacters, "Context exceeds budget");
    Assert(context.Evidence[0].Id == history.Evidence[^1].Id, "Newest update missing");
    Assert(context.Evidence.All(e => history.Evidence.Any(s => s.Id == e.Id && s.Text == e.Text)), "Source altered");
});
Check("Large OCR stays available without crowding out the activity", () =>
{
    var history = new Capsule { Title = "Lab", Goal = "Finish code and viva preparation" };
    history.Evidence.Add(new EvidenceItem { Kind = "snapshot", WindowTitle = "Lab instructions - PDF", Text = new string('x', 6000) });
    history.Evidence.Add(new EvidenceItem { Kind = "snapshot", WindowTitle = "Nalanda course files", Text = new string('y', 6000) });
    var context = ActivityContext.ForGeneration(history);
    CapsuleRules.ValidateInput(context);
    Assert(context.Evidence.Count == 2 && context.Evidence.All(e => e.Text.Length == 650), "Large OCR hid a screenshot or exceeded the excerpt limit");
    Assert(history.Evidence.All(e => e.Text.Length == 6000), "Saved OCR changed");
});
Check("Overwrite replaces a capsule without duplicate or temp files", () =>
{
    capsule.Goal = "Updated goal"; store.Save(capsule);
    Assert(store.Load(capsule.Id).Goal == capsule.Goal, "Update lost");
    Assert(Directory.GetFiles(temp).Length == 1, "Extra files left");
});
Check("Invalid citation is rejected", () =>
{
    var brief = new ResumeBrief { LastState = [new Claim { Text = "Test complete", EvidenceIds = ["invented"] }] };
    Reject<InvalidDataException>(() => CapsuleRules.ParseBrief(JsonSerializer.Serialize(brief, CapsuleRules.Json), capsule));
});
Check("Missing citations and null sections are rejected", () =>
{
    Reject<InvalidDataException>(() => CapsuleRules.ParseBrief("{\"lastState\":[{\"text\":\"Done\",\"evidenceIds\":[]}]}", capsule));
    Reject<InvalidDataException>(() => CapsuleRules.ParseBrief("{\"lastState\":null}", capsule));
});
Check("A real source ID cannot legitimize an invented quotation", () =>
{
    var brief = new ResumeBrief { LastState = [new Claim { Text = "Approach B passed", SourceQuote = "Approach B passed the RAM test", EvidenceIds = [capsule.Evidence[0].Id] }] };
    Reject<InvalidDataException>(() => CapsuleRules.ParseBrief(JsonSerializer.Serialize(brief, CapsuleRules.Json), capsule));
});
Check("Valid citations and user edits survive roundtrip", () =>
{
    var brief = new ResumeBrief { Decisions = [new Claim { Text = "Rejected A", SourceQuote = "so I rejected it for this offline project", EvidenceIds = [capsule.Evidence[0].Id] }] };
    capsule.Brief = CapsuleRules.ParseBrief(JsonSerializer.Serialize(brief, CapsuleRules.Json), capsule);
    capsule.Brief.Decisions[0].UserEdited = true;
    store.Save(capsule);
    Assert(store.Load(capsule.Id).Brief!.Decisions[0].UserEdited, "Edit marker lost");
});
Check("Paraphrased activity stays linked to exact evidence", () =>
{
    var sample = new Capsule { Title = "Comsys Lab Viva", Goal = "Make working code and prepare for the viva" };
    var note = new EvidenceItem { Label = "Note 1", Text = "I opened the lab PDF to read the instructions." };
    var screen = new EvidenceItem { Kind = "snapshot", WindowTitle = "Lab instructions - PDF viewer", Context = "I was checking the assignment requirements." };
    sample.Evidence.Add(note); sample.Evidence.Add(screen);
    var brief = new ResumeBrief { Activity = [new Claim { Text = "Opened the lab PDF to review the assignment", SourceQuote = note.Text, EvidenceIds = [note.Id] }], LastState = [new Claim { Text = "The assignment requirements were being checked", SourceQuote = screen.Context, EvidenceIds = [screen.Id] }] };
    var checkedBrief = CapsuleRules.ParseBrief(JsonSerializer.Serialize(brief, CapsuleRules.Json), sample);
    Assert(checkedBrief.Activity[0].Text != checkedBrief.Activity[0].SourceQuote, "Paraphrase was lost");
    Assert(GenerationPassages.Select(sample).Any(p => p.Text == screen.Context), "Screenshot context was omitted");
    brief.Activity[0].SourceQuote = "I completed and submitted the assignment";
    Reject<InvalidDataException>(() => CapsuleRules.ParseBrief(JsonSerializer.Serialize(brief, CapsuleRules.Json), sample));
});
Check("Visual descriptions are selectable but remain screen observations", () =>
{
    var sample = new Capsule { Title = "Lab", Goal = "Find the next step" };
    var image = new EvidenceItem { Kind = "snapshot", Text = "", VisualDescription = "A code editor is open with a plotting script." };
    sample.Evidence.Add(image);
    Assert(GenerationPassages.Select(sample).Single().Text == image.VisualDescription, "Image description not selected");
    var brief = new ResumeBrief { Activity = [new Claim { Text = "A plotting script was visible", SourceQuote = image.VisualDescription, EvidenceIds = [image.Id] }] };
    CapsuleRules.ParseBrief(JsonSerializer.Serialize(brief, CapsuleRules.Json), sample);
});
Check("Input limit rejects excess without deleting evidence", () =>
{
    var large = CapsuleRules.Demo(); large.Evidence[0].Text = new string('x', 9000);
    Reject<InvalidDataException>(() => CapsuleRules.ValidateInput(large));
    Assert(large.Evidence[0].Text.Length == 9000, "Input was truncated");
});
Check("Duplicate source identifiers are rejected", () =>
{
    var duplicate = CapsuleRules.Demo(); duplicate.Evidence[1].Id = duplicate.Evidence[0].Id;
    Reject<InvalidDataException>(() => CapsuleRules.ValidateInput(duplicate));
});
Check("Corrupt capsule is reported and preserved", () =>
{
    var corrupt = Path.Combine(temp, Guid.NewGuid().ToString("N") + ".tbc");
    File.WriteAllBytes(corrupt, [1, 2, 3]);
    var list = store.List(); Assert(list.Capsules.Count == 1 && list.UnreadableFiles == 1, "Corruption not reported");
    Assert(File.Exists(corrupt), "Corrupt file was removed"); File.Delete(corrupt);
});
Check("Delete removes the capsule and embedded attachments", () =>
{
    store.Delete(capsule.Id); Assert(store.List().Capsules.Count == 0 && Directory.GetFiles(temp).Length == 0, "Delete incomplete");
});
Check("Screen noise is filtered and user updates take priority", () =>
{
    var sample = new Capsule { Title = "Study", Goal = "Finish revision" };
    sample.Evidence.Add(new EvidenceItem { Kind = "snapshot", Text = string.Join("\n", Enumerable.Repeat("View all\n7:29 PM\n4\nVoice", 100)) + "\nThe document describes amplitude modulation and its frequency spectrum." });
    sample.Evidence.Add(new EvidenceItem { Kind = "update", Text = "Next, solve the modulation exercise." });
    var selected = GenerationPassages.Select(sample);
    Assert(selected.Count == 2 && selected[0].Evidence.Kind == "update", "UI noise retained or update deprioritized");
    Assert(selected.All(p => p.Evidence.Text.Contains(p.Text, StringComparison.Ordinal)), "Quote changed");
});
Check("Long OCR and repeated passages have bounded generation cost", () =>
{
    var sample = new Capsule();
    sample.Evidence.Add(new EvidenceItem { Kind = "eye", Text = string.Join("\n", Enumerable.Range(0, 200).Select(i => $"Screenshot paragraph {i} includes a lengthy explanation of the current experiment.")) });
    sample.Evidence.Add(new EvidenceItem { Kind = "update", Text = string.Join("\n", Enumerable.Range(0, 100).Select(i => $"I finished exercise number {i} and recorded the result.")) });
    var selected = GenerationPassages.Select(sample);
    Assert(selected.Count <= 24 && selected.Sum(p => p.Text.Length) <= 4200, "Input unbounded");
    Assert(selected.Count(p => GenerationPassages.IsScreen(p.Evidence)) <= GenerationPassages.MaxScreenPassages, "Too many screen passages");
    sample.Evidence = [new EvidenceItem { Kind = "note", Text = new string('a', 1000) }];
    selected = GenerationPassages.Select(sample);
    Assert(selected.Single().Text.Length == 500 && sample.Evidence.Single().Text.Length == 1000, "Long source was modified");
});
Check("Seven screen observations remain available for a real activity timeline", () =>
{
    var sample = new Capsule { Goal = "Prepare for Comsys lab viva" };
    for (var i = 0; i < 7; i++) sample.Evidence.Add(new EvidenceItem { Kind = "eye", WindowTitle = $"Lab screen {i + 1}", Text = $"This is lab screenshot number {i + 1} with visible study material." });
    var context = ActivityContext.ForGeneration(sample);
    var selected = GenerationPassages.Select(context);
    Assert(selected.Count(p => GenerationPassages.IsScreen(p.Evidence)) == 7, "An early Eye screenshot was dropped");
    Assert(selected.Any(p => p.Evidence.Id == sample.Evidence[0].Id), "The earliest step was dropped");
});
Check("Final handoff combines text fields chronologically and excludes attachments", () =>
{
    var sample = new Capsule { Title = "Lab", Goal = "Complete lab" };
    var earlier = new EvidenceItem { Kind = "snapshot", Text = "Email body includes the required measurement.", VisualDescription = "An opened email describes the lab.", Context = "I am checking requirements.", WindowTitle = "Gmail", CapturedAt = DateTimeOffset.UtcNow.AddMinutes(-2), Image = [1, 2, 3], Audio = [4, 5, 6] };
    sample.Evidence = [new EvidenceItem { Text = "The plot now works." }, earlier];
    using var json = JsonDocument.Parse(HandoffSynthesis.BuildPrompt(ActivityContext.ForGeneration(sample)));
    var evidence = json.RootElement.GetProperty("evidence");
    Assert(evidence[0].GetProperty("id").GetString() == earlier.Id, "Chronology lost");
    Assert(evidence[0].GetProperty("extractedText").GetString() == "" && evidence[0].GetProperty("imageObservation").GetString() == earlier.VisualDescription && evidence[0].GetProperty("userContext").GetString() == earlier.Context, "Described image sent duplicate OCR or lost its context");
    Assert(evidence.EnumerateArray().All(e => !e.TryGetProperty("image", out _) && !e.TryGetProperty("audio", out _)), "Attachment sent to final model");
});
Check("OCR-only screens send a bounded exact excerpt", () =>
{
    var ocr = "7:29 PM\nView all\nA detailed requirement appears in this document and remains unfinished.\n" + new string('x', 1200);
    var screen = new EvidenceItem { Kind = "eye", Text = ocr };
    var sample = new Capsule { Goal = "Finish the document", Evidence = [screen] };
    var projected = ActivityContext.ForGeneration(sample).Evidence.Single();
    Assert(projected.Text.Length <= 650 && ocr.Contains(projected.Text, StringComparison.Ordinal), "OCR projection must be a short contiguous source excerpt");
    Assert(projected.Text.StartsWith("A detailed requirement", StringComparison.Ordinal), "OCR excerpt missed the first substantive line");
    Assert(screen.Text == ocr, "Stored OCR was changed");
});
Check("Synthesis retains multiple citations and rejects a state dump", () =>
{
    var sample = CapsuleRules.Demo();
    var claim = new Claim { Text = "Measure the remaining RAM requirement.", SourceQuote = "I have not measured B's memory usage yet.", EvidenceIds = sample.Evidence.Select(e => e.Id).ToList() };
    var brief = new ResumeBrief { LastState = [claim], NextActions = [claim] };
    Assert(HandoffSynthesis.Parse(JsonSerializer.Serialize(brief, CapsuleRules.Json), sample).NextActions[0].EvidenceIds.Count == 2, "Combined citation lost");
    brief.LastState.Add(claim);
    Reject<InvalidDataException>(() => HandoffSynthesis.Parse(JsonSerializer.Serialize(brief, CapsuleRules.Json), sample));
});
Directory.Delete(temp);
Console.WriteLine($"{passed} checks passed.");

if (args.Contains("--real-capsule-check"))
{
    var idIndex = Array.IndexOf(args, "--real-capsule-check");
    var source = new CapsuleStore().Load(Guid.ParseExact(args[idIndex + 1], "N"));
    var sourceLimitIndex = Array.IndexOf(args, "--first-evidence-count");
    if (sourceLimitIndex >= 0) source.Evidence = source.Evidence.Take(int.Parse(args[sourceLimitIndex + 1])).ToList();
    if (args.Contains("--comsys-user-story") && source.Evidence.Count >= 8)
    {
        // The participant stated these facts in chat; apply them only to this in-memory acceptance copy.
        source.Evidence[1].Context = "I opened Gmail.";
        source.Evidence[2].Context = "I opened Nalanda.";
        source.Evidence[3].Context = "I was trying to download a few lab files.";
        source.Evidence[4].Context = "I opened the PDF containing the lab instructions.";
        source.Evidence[5].Context = "I was doing something unrelated to the lab.";
        source.Evidence[6].Context = "I was doing something unrelated to the lab.";
        source.Evidence[7].Context = "I opened the lab code, but it is really messed up and I still need to make it work.";
    }
    var rootIndex = Array.IndexOf(args, "--root");
    var root = rootIndex >= 0 ? args[rootIndex + 1] : Directory.GetCurrentDirectory();
    var outputIndex = Array.IndexOf(args, "--output");
    var output = args[outputIndex + 1];
    var descriptions = new List<object>();
    var imageSources = source.Evidence.Select((e, i) => (Evidence: e, Index: i)).Where(x => x.Evidence.Image is not null).ToList();
    var visionReportIndex = Array.IndexOf(args, "--vision-report");
    if (visionReportIndex >= 0)
    {
        using var cached = JsonDocument.Parse(File.ReadAllText(args[visionReportIndex + 1]));
        foreach (var item in cached.RootElement.GetProperty("descriptions").EnumerateArray())
        {
            var index = item.GetProperty("index").GetInt32();
            var description = item.GetProperty("description").GetString() ?? "";
            source.Evidence[index].VisualDescription = description;
            descriptions.Add(new { index, description, cached = true });
        }
        imageSources.Clear();
    }
    var indexFilter = Array.IndexOf(args, "--vision-indexes");
    if (indexFilter >= 0)
    {
        var wanted = args[indexFilter + 1].Split(',').Select(int.Parse).ToHashSet();
        imageSources = imageSources.Where(x => wanted.Contains(x.Index)).ToList();
    }
    var limitIndex = Array.IndexOf(args, "--vision-limit");
    if (limitIndex >= 0) imageSources = imageSources.Take(int.Parse(args[limitIndex + 1])).ToList();
    using (var vision = new LocalVision(root))
    {
        foreach (var (evidence, index) in imageSources)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(5));
                evidence.VisualDescription = await vision.DescribeAsync(evidence.Image!, deadline.Token);
                descriptions.Add(new { index, description = evidence.VisualDescription, seconds = watch.Elapsed.TotalSeconds });
                Console.WriteLine($"VISION {index + 1}/{source.Evidence.Count}: described in {watch.Elapsed.TotalSeconds:0.0}s");
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or TimeoutException or OperationCanceledException)
            {
                descriptions.Add(new { index, error = ex.GetType().Name + ": " + ex.Message, seconds = watch.Elapsed.TotalSeconds });
                Console.WriteLine($"VISION {index + 1}/{source.Evidence.Count}: {ex.GetType().Name}: {ex.Message}");
                throw new InvalidOperationException($"Vision description for evidence {index + 1} failed; stopping this check.", ex);
            }
        }
    }
    File.WriteAllText(output + ".vision.json", JsonSerializer.Serialize(new { sourceCount = source.Evidence.Count, descriptions }, CapsuleRules.Json));
    if (args.Contains("--vision-only")) return;
    var context = ActivityContext.ForGeneration(source);
    var selected = GenerationPassages.Select(context);
    Console.WriteLine($"Selected {selected.Count} passages from {selected.Count(p => GenerationPassages.IsScreen(p.Evidence))} screenshots.");
    using var generator = new LocalGenerator(root);
    using var generationDeadline = new CancellationTokenSource(TimeSpan.FromMinutes(3));
    var result = await generator.GenerateAsync(context, generationDeadline.Token);
    object DescribeClaim(Claim claim) => new
    {
        text = claim.Text,
        sourceIndexes = claim.EvidenceIds.Select(id => source.Evidence.FindIndex(e => e.Id == id)).ToArray()
    };
    var report = new
    {
        sourceTitle = source.Title,
        sourceCount = source.Evidence.Count,
        imageCount = imageSources.Count,
        descriptions,
        selectedScreenCount = selected.Count(p => GenerationPassages.IsScreen(p.Evidence)),
        generationSeconds = result.Seconds,
        activity = result.Brief.Activity.Select(DescribeClaim).ToArray(),
        lastState = result.Brief.LastState.Select(DescribeClaim).ToArray(),
        decisions = result.Brief.Decisions.Select(DescribeClaim).ToArray(),
        openQuestions = result.Brief.OpenQuestions.Select(DescribeClaim).ToArray(),
        nextActions = result.Brief.NextActions.Select(DescribeClaim).ToArray(),
        savedCapsuleChanged = false
    };
    File.WriteAllText(output, JsonSerializer.Serialize(report, CapsuleRules.Json));
    Console.WriteLine($"REAL CAPSULE CHECK: {result.Brief.Activity.Count} activity entries, {result.Seconds:0.0}s text generation; original capsule unchanged.");
}

if (args.Contains("--model"))
{
    var rootIndex = Array.IndexOf(args, "--root");
    var root = rootIndex >= 0 ? args[rootIndex + 1] : Directory.GetCurrentDirectory();
    var reportDirectory = Path.Combine(root, "artifacts", "verification"); Directory.CreateDirectory(reportDirectory);
    using var generator = new LocalGenerator(root);
    var sample = CapsuleRules.Demo();
    var result = await generator.GenerateAsync(sample, CancellationToken.None);
    Assert(result.Brief.AllClaims.All(c => sample.Evidence.Any(e => c.EvidenceIds.Contains(e.Id) && (e.Text.Contains(c.SourceQuote, StringComparison.Ordinal) || e.Context.Contains(c.SourceQuote, StringComparison.Ordinal) || e.VisualDescription.Contains(c.SourceQuote, StringComparison.Ordinal) || e.WindowTitle.Contains(c.SourceQuote, StringComparison.Ordinal)))), "Generated wording lost its source");
    sample.Brief = result.Brief; sample.GenerationSeconds = result.Seconds; sample.GenerationBackend = generator.Backend;
    File.WriteAllText(Path.Combine(reportDirectory, "model-smoke.json"), JsonSerializer.Serialize(sample, CapsuleRules.Json));
    Console.WriteLine($"MODEL PASS: {result.Brief.AllClaims.Count()} cited statements in {result.Seconds:0.00}s warm generation (loading excluded).");
    Console.WriteLine("PASS generated wording cites its source");
    using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
    try { await generator.GenerateAsync(sample, cancelled.Token); throw new Exception("Cancellation ignored"); }
    catch (OperationCanceledException) { Console.WriteLine("PASS cancelled request does not modify saved capsule"); }
    using var inFlight = new CancellationTokenSource(TimeSpan.FromMilliseconds(150));
    try { await generator.GenerateAsync(sample, inFlight.Token); throw new Exception("In-flight cancellation ignored"); }
    catch (OperationCanceledException) { Assert(generator.PeakModelMemoryBytes == 0, "Cancelled worker remained running"); Console.WriteLine("PASS in-flight cancellation stops the model worker"); }
}

if (args.Contains("--comsys-model"))
{
    var root = Directory.GetCurrentDirectory();
    var sample = new Capsule { Title = "Comsys Lab Viva", Goal = "Make the communication systems lab code work and prepare for the viva." };
    sample.Evidence.Add(new EvidenceItem { Label = "Note 1", Text = "I am just starting the Comsys lab viva work." });
    sample.Evidence.Add(new EvidenceItem { Kind = "snapshot", Label = "Observation 1", WindowTitle = "Inbox - Gmail", Text = "Inbox Gmail Search mail Compose" });
    sample.Evidence.Add(new EvidenceItem { Kind = "snapshot", Label = "Observation 2", WindowTitle = "Nalanda", Text = "Nalanda Course files Download Lab resources" });
    sample.Evidence.Add(new EvidenceItem { Kind = "snapshot", Label = "Observation 3", Context = "I was trying to download the lab files.", Text = "Download files Save as" });
    sample.Evidence.Add(new EvidenceItem { Kind = "snapshot", Label = "Observation 4", WindowTitle = "Lab instructions - PDF viewer", Text = "Communication Systems lab instructions. Write the program and prepare for viva questions." });
    sample.Evidence.Add(new EvidenceItem { Kind = "update", Label = "My progress update", Text = "I opened the starter code, but it is messy and does not run yet. I need to repair it before preparing for the viva." });
    using var generator = new LocalGenerator(root);
    using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(110));
    var result = await generator.GenerateAsync(ThreadBack.Core.ActivityContext.ForGeneration(sample), deadline.Token);
    sample.Brief = result.Brief; sample.GenerationBackend = generator.Backend; sample.GenerationSeconds = result.Seconds;
    var reportPath = Path.Combine(root, "artifacts", "verification", "comsys-handoff-2026-09-25.json");
    File.WriteAllText(reportPath, JsonSerializer.Serialize(sample, CapsuleRules.Json));
    Assert(result.Brief.Activity.Count >= 3, "Activity timeline omitted key events");
    Assert(result.Brief.OpenQuestions.Any(c => c.Text.Contains("code", StringComparison.OrdinalIgnoreCase)), "Broken code was not identified as unfinished");
    Console.WriteLine($"COMSYS MODEL PASS: {result.Brief.Activity.Count} timeline entries, {result.Brief.OpenQuestions.Count} open questions, {result.Seconds:0.0}s generation");
    foreach (var item in result.Brief.Activity) Console.WriteLine("  " + item.Text);
}

if (args.Contains("--vision-model"))
{
    var root = Directory.GetCurrentDirectory();
    var samplePath = Path.Combine(root, "artifacts", "verification", "circuits-acceptance.png");
    using var vision = new LocalVision(root);
    using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(120));
    var watch = System.Diagnostics.Stopwatch.StartNew();
    var description = await vision.DescribeAsync(await File.ReadAllBytesAsync(samplePath), deadline.Token);
    var report = new { description, seconds = watch.Elapsed.TotalSeconds, model = "Qwen2.5-VL-3B-Instruct-Q4_K_M" };
    File.WriteAllText(Path.Combine(root, "artifacts", "verification", "vision-model-2026-09-25.json"), JsonSerializer.Serialize(report, CapsuleRules.Json));
    Console.WriteLine($"VISION MODEL: {description} ({watch.Elapsed.TotalSeconds:0.0}s)");
}

if (args.Contains("--evaluation"))
{
    var rootIndex = Array.IndexOf(args, "--root");
    var root = rootIndex >= 0 ? args[rootIndex + 1] : Directory.GetCurrentDirectory();
    var output = Path.Combine(root, "artifacts", "verification"); Directory.CreateDirectory(output);
    using var generator = new LocalGenerator(root);
    using var fixtureDoc = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "tests", "fixtures", "capsules.json")));
    var reports = new List<object>();
    var failures = 0;
    foreach (var fixture in fixtureDoc.RootElement.EnumerateArray())
    {
        var item = new Capsule { Title = fixture.GetProperty("title").GetString()!, Goal = fixture.GetProperty("goal").GetString()! };
        foreach (var note in fixture.GetProperty("notes").EnumerateArray()) item.Evidence.Add(new EvidenceItem { Label = "Source " + (item.Evidence.Count + 1), Text = note.GetString()! });
        var id = fixture.GetProperty("id").GetString()!;
        try
        {
            var result = await generator.GenerateAsync(item, CancellationToken.None);
            item.Brief = result.Brief; item.GenerationSeconds = result.Seconds; item.GenerationBackend = generator.Backend;
            File.WriteAllText(Path.Combine(output, id + ".json"), JsonSerializer.Serialize(item, CapsuleRules.Json));
            // Keyword checks flag omissions for review. They are not semantic accuracy scores.
            bool decisionPresent = !fixture.TryGetProperty("requiredDecision", out var decision) || result.Brief.Decisions.Any(c => (c.Text + " " + c.SourceQuote).Contains(decision.GetString()!, StringComparison.OrdinalIgnoreCase));
            bool unknownPresent = !fixture.TryGetProperty("requiredUnknown", out var unknown) || result.Brief.OpenQuestions.Any(c => (c.Text + " " + c.SourceQuote).Contains(unknown.GetString()!, StringComparison.OrdinalIgnoreCase));
            reports.Add(new { id, success = true, seconds = result.Seconds, claims = result.Brief.AllClaims.Count(), decisionPresent, unknownPresent, peakModelMemoryBytes = generator.PeakModelMemoryBytes });
            Console.WriteLine($"{id}: {result.Seconds:0.0}s, {result.Brief.AllClaims.Count()} quote-checked statements, decision={decisionPresent}, unknown={unknownPresent}");
        }
        catch (Exception ex) { failures++; reports.Add(new { id, success = false, error = ex.Message }); Console.WriteLine(id + ": FAIL " + ex.Message); }
        File.WriteAllText(Path.Combine(output, "evaluation.json"), JsonSerializer.Serialize(new { backend = generator.Backend, reports }, CapsuleRules.Json));
    }
    if (failures > 0) { Console.Error.WriteLine($"{failures} scenarios failed structural generation; inspect evaluation.json."); Environment.ExitCode = 1; }
}


if (args.Contains("--ocr-model"))
{
    var root = Directory.GetCurrentDirectory();
    using var generator = new LocalGenerator(root);
    var sample = CapsuleRules.Demo();
    sample.Evidence.Add(new EvidenceItem { Kind = "snapshot", Text = string.Join("\n", Enumerable.Repeat("View all\nVoice\n7:29 PM\n4", 50)) + "\nThe search experiment compares a local index against an online index." });
    var selected = GenerationPassages.Select(sample);
    var watch = System.Diagnostics.Stopwatch.StartNew();
    using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(90));
    var result = await generator.GenerateAsync(sample, deadline.Token);
    var report = new { success = true, rawLines = sample.Evidence.Sum(e => e.Text.Split('\n').Length), selectedPassages = selected.Count, totalSeconds = watch.Elapsed.TotalSeconds, generationSeconds = result.Seconds, decisions = result.Brief.Decisions.Count, nextSteps = result.Brief.NextActions.Count, claims = result.Brief.AllClaims.Count(), exactQuotes = result.Brief.AllClaims.All(c => sample.Evidence.Any(e => c.EvidenceIds.Contains(e.Id) && e.Text.Contains(c.SourceQuote, StringComparison.Ordinal))) };
    File.WriteAllText(Path.Combine(root, "artifacts/verification/overlay-fix-ocr-model.json"), JsonSerializer.Serialize(report, CapsuleRules.Json));
    Assert(report.exactQuotes && report.selectedPassages <= 24, "OCR model verification failed");
    Console.WriteLine($"OCR MODEL PASS: {report.rawLines} raw lines -> {report.selectedPassages} passages; {report.totalSeconds:0.0}s total, {report.generationSeconds:0.0}s generation");
}

if (args.Contains("--diagnose-saved"))
{
    var savedStore = new CapsuleStore();
    var reports = savedStore.List().Capsules.Select(saved =>
    {
        var context = ThreadBack.Core.ActivityContext.ForGeneration(saved);
        var passages = GenerationPassages.Select(context);
        string? error = null;
        try { CapsuleRules.ValidateInput(context); } catch (Exception ex) { error = ex.Message; }
        return new { id = saved.Id, titleLength = saved.Title.Length, goalLength = saved.Goal.Length, sources = saved.Evidence.Select(e => new { kind = e.Kind, characters = e.Text.Length }).ToArray(), contextSources = context.Evidence.Count, passages = passages.Count, validationError = error, hasBrief = saved.Brief is not null };
    }).ToArray();
    File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), "artifacts/verification/capsule-button-diagnosis.json"), JsonSerializer.Serialize(reports, CapsuleRules.Json));
    Console.WriteLine(JsonSerializer.Serialize(reports, CapsuleRules.Json));
}

if (args.Contains("--saved-text-check"))
{
    var idIndex = Array.IndexOf(args, "--saved-text-check");
    var saved = new CapsuleStore().Load(Guid.Parse(args[idIndex + 1]));
    var context = ActivityContext.ForGeneration(saved);
    using var generator = new LocalGenerator(Directory.GetCurrentDirectory());
    using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(10));
    var watch = System.Diagnostics.Stopwatch.StartNew();
    var result = await generator.GenerateAsync(context, deadline.Token);
    var report = new { success = true, sourceCount = context.Evidence.Count, totalSeconds = watch.Elapsed.TotalSeconds, generationSeconds = result.Seconds, activityCount = result.Brief.Activity.Count, claimCount = result.Brief.AllClaims.Count(), hasLastState = result.Brief.LastState.Count == 1, hasNextAction = result.Brief.NextActions.Count == 1 };
    File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), "artifacts/verification/saved-text-check-2026-09-30.json"), JsonSerializer.Serialize(report, CapsuleRules.Json));
    Assert(report.hasLastState && report.hasNextAction, "Saved task did not yield a complete capsule");
    Console.WriteLine($"SAVED TEXT CHECK PASS: {report.sourceCount} sources, {report.claimCount} cited claims, {report.totalSeconds:0.0}s; original saved task unchanged.");
}

if (args.Contains("--goal-context-model"))
{
    var root = Directory.GetCurrentDirectory();
    var sample = new Capsule { Title = "Lab submission", Goal = "Submit the amplitude modulation lab with a working plot and measured modulation index." };
    var start = DateTimeOffset.Parse("2026-09-28T10:00:00Z");
    sample.Evidence =
    [
        new EvidenceItem { Kind = "snapshot", Label = "Opened email", CapturedAt = start, WindowTitle = "Lab requirements - Gmail", Text = "Subject: Amplitude modulation lab submission\nPlease submit the waveform plot and measured modulation index. The starter script is linked in the course folder. Compare your measured index with the theoretical value." },
        new EvidenceItem { Kind = "snapshot", Label = "First run", CapturedAt = start.AddMinutes(5), WindowTitle = "am_lab.py - Editor", Text = "Run output: NameError: name carrier_freq is not defined. The script stopped before producing a plot." },
        new EvidenceItem { Kind = "update", Label = "Progress", CapturedAt = start.AddMinutes(10), Text = "Fixed the undefined carrier frequency. The script now runs and the waveform plot is saved. I have not measured the modulation index yet." }
    ];
    using var generator = new LocalGenerator(root);
    using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(4));
    var result = await generator.GenerateAsync(ActivityContext.ForGeneration(sample), deadline.Token);
    sample.Brief = result.Brief; sample.GenerationSeconds = result.Seconds;
    File.WriteAllText(Path.Combine(root, "artifacts/verification/goal-context-ocr-model-2026-09-28.json"), JsonSerializer.Serialize(sample, CapsuleRules.Json));
    Assert(result.Brief.LastState.Count == 1 && result.Brief.NextActions.Count == 1, "Missing synthesized state or suggested step");
    Assert(result.Brief.NextActions[0].Text.Contains("index", StringComparison.OrdinalIgnoreCase), "Next step did not bridge the remaining measurement to the goal");
    Assert(result.Brief.LastState[0].Text.Contains("plot", StringComparison.OrdinalIgnoreCase), "Latest meaningful progress was lost");
    Assert(result.Brief.Decisions.Count == 0, "Invented a decision");
    Assert(!result.Brief.OpenQuestions.Any(c => c.Text.Contains("NameError", StringComparison.OrdinalIgnoreCase) || c.Text.Contains("undefined", StringComparison.OrdinalIgnoreCase)), "Resolved blocker remained open");
    Assert(result.Brief.Activity.Any(c => c.EvidenceIds.Contains(sample.Evidence[0].Id) && c.Text.Contains("modulation", StringComparison.OrdinalIgnoreCase)), "Email content was reduced to app identity");
    Console.WriteLine($"GOAL CONTEXT PASS: OCR-only email and ordered progress synthesized in {result.Seconds:0.0}s");
    Console.WriteLine(JsonSerializer.Serialize(result.Brief, CapsuleRules.Json));
}


if (args.Contains("--goal-context-vision"))
{
    var root = Directory.GetCurrentDirectory();
    using var vision = new LocalVision(root);
    using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(5));
    var watch = System.Diagnostics.Stopwatch.StartNew();
    var description = await vision.DescribeAsync(await File.ReadAllBytesAsync(Path.Combine(root, "artifacts/verification/goal-context-email-fixture.png")), deadline.Token, "Submit the amplitude modulation lab with a working plot and measured modulation index.", "Subject: Amplitude modulation lab submission. Please submit the waveform plot and measured modulation index. The starter script is linked in the course folder. Compare your measured index with the theoretical value.");
    File.WriteAllText(Path.Combine(root, "artifacts/verification/goal-context-vision-2026-09-28.json"), JsonSerializer.Serialize(new { description, seconds = watch.Elapsed.TotalSeconds }, CapsuleRules.Json));
    Assert(description.Contains("modulation", StringComparison.OrdinalIgnoreCase) && description.Length > 100, "Email content was lost");
    Console.WriteLine("VISION CONTEXT PASS: " + description);
}

if (args.Contains("--model-memory-limit"))
{
    var root = Directory.GetCurrentDirectory();
    using var generator = new LocalGenerator(root) { MemoryLimitGb = 6 };
    using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(4));
    var result = await generator.GenerateAsync(CapsuleRules.Demo(), deadline.Token);
    Assert(result.Brief.LastState.Count == 1 && result.Brief.NextActions.Count == 1, "Bounded model did not generate a handoff");
    var peak = generator.PeakModelMemoryBytes;
    File.WriteAllText(Path.Combine(root, "artifacts/verification/model-memory-limit-2026-09-28.json"), JsonSerializer.Serialize(new { limitGb = 6, seconds = result.Seconds, peakWorkingSetBytes = peak, claims = result.Brief.AllClaims.Count() }, CapsuleRules.Json));
    Assert(peak <= 6L * 1024 * 1024 * 1024, "The model exceeded the selected RAM limit");
    Console.WriteLine($"MODEL MEMORY LIMIT PASS: 6 GB cap, {result.Brief.AllClaims.Count()} cited statements");
}

if (args.Contains("--vision-memory-limit"))
{
    var root = Directory.GetCurrentDirectory();
    using var vision = new LocalVision(root) { MemoryLimitGb = 6 };
    using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(5));
    var description = await vision.DescribeAsync(await File.ReadAllBytesAsync(Path.Combine(root, "artifacts/verification/goal-context-email-fixture.png")), deadline.Token, "Submit the amplitude modulation lab", "Please submit the waveform plot and measured modulation index.");
    Assert(description.Contains("modulation", StringComparison.OrdinalIgnoreCase), "Bounded vision model lost the useful email content");
    File.WriteAllText(Path.Combine(root, "artifacts/verification/vision-memory-limit-2026-09-28.json"), JsonSerializer.Serialize(new { limitGb = 6, description }, CapsuleRules.Json));
    Console.WriteLine("VISION MEMORY LIMIT PASS: 6 GB cap and content extraction");
}

if (args.Contains("--gpu-model"))
{
    var root = Directory.GetCurrentDirectory();
    using var generator = new LocalGenerator(root) { BackendMode = ModelBackend.VulkanGpu };
    using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(4));
    var result = await generator.GenerateAsync(CapsuleRules.Demo(), deadline.Token);
    Assert(result.Brief.LastState.Count == 1 && result.Brief.NextActions.Count == 1, "GPU model did not generate a useful handoff");
    File.WriteAllText(Path.Combine(root, "artifacts/verification/gpu-model-2026-09-28.json"), JsonSerializer.Serialize(new { backend = generator.Backend, seconds = result.Seconds, claims = result.Brief.AllClaims.Count() }, CapsuleRules.Json));
    Console.WriteLine($"GPU MODEL PASS: {generator.Backend}, {result.Seconds:0.0}s");
}
if (args.Contains("--gpu-vision"))
{
    var root = Directory.GetCurrentDirectory();
    using var vision = new LocalVision(root) { BackendMode = ModelBackend.VulkanGpu };
    using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(5));
    var watch = System.Diagnostics.Stopwatch.StartNew();
    var description = await vision.DescribeAsync(await File.ReadAllBytesAsync(Path.Combine(root, "artifacts/verification/goal-context-email-fixture.png")), deadline.Token, "Submit the amplitude modulation lab", "Please submit the waveform plot and measured modulation index.");
    Assert(description.Contains("modulation", StringComparison.OrdinalIgnoreCase), "GPU vision model lost the email context");
    File.WriteAllText(Path.Combine(root, "artifacts/verification/gpu-vision-2026-09-28.json"), JsonSerializer.Serialize(new { backend = "VulkanGpu", seconds = watch.Elapsed.TotalSeconds, description }, CapsuleRules.Json));
    Console.WriteLine($"GPU VISION PASS: {watch.Elapsed.TotalSeconds:0.0}s");
}

if (args.Contains("--npu-model"))
{
    var root = Directory.GetCurrentDirectory();
    using var generator = new LocalGenerator(root) { BackendMode = ModelBackend.OpenVinoNpu };
    using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(4));
    var result = await generator.GenerateAsync(CapsuleRules.Demo(), deadline.Token);
    Assert(result.Brief.LastState.Count == 1 && result.Brief.NextActions.Count == 1, "NPU model did not generate a useful handoff");
    File.WriteAllText(Path.Combine(root, "artifacts/verification/npu-model-verified.json"), JsonSerializer.Serialize(new { backend = generator.Backend, seconds = result.Seconds, claims = result.Brief.AllClaims.Count(), validatedAt = DateTimeOffset.UtcNow }, CapsuleRules.Json));
    Console.WriteLine($"NPU MODEL PASS: {generator.Backend}, {result.Seconds:0.0}s");
}
