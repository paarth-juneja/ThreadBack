using System.Text.Json;
using System.Text.Json.Serialization;

namespace ThreadBack.Core;

public sealed class EvidenceItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Kind { get; set; } = "note";
    public string Label { get; set; } = "Note";
    public string Text { get; set; } = "";
    public string Context { get; set; } = "";
    public string VisualDescription { get; set; } = "";
    public string WindowTitle { get; set; } = "";
    public string? SourceUrl { get; set; }
    public byte[]? Image { get; set; }
    public byte[]? Audio { get; set; }
    public DateTimeOffset CapturedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class Claim
{
    public string Text { get; set; } = "";
    public string SourceQuote { get; set; } = "";
    public List<string> EvidenceIds { get; set; } = [];
    public bool UserEdited { get; set; }
}

public sealed class ResumeBrief
{
    public List<Claim> Activity { get; set; } = [];
    public List<Claim> LastState { get; set; } = [];
    public List<Claim> Decisions { get; set; } = [];
    public List<Claim> OpenQuestions { get; set; } = [];
    public List<Claim> NextActions { get; set; } = [];
    [JsonIgnore] public IEnumerable<Claim> AllClaims => Activity.Concat(LastState).Concat(Decisions).Concat(OpenQuestions).Concat(NextActions);
}

public sealed class Capsule
{
    public int SchemaVersion { get; set; } = 1;
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = "Untitled task";
    public string Goal { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<EvidenceItem> Evidence { get; set; } = [];
    public ResumeBrief? Brief { get; set; }
    public string GenerationBackend { get; set; } = "Not generated";
    public double? GenerationSeconds { get; set; }
}

public static class CapsuleRules
{
    public const int MaxInputCharacters = 8500;
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public static void ValidateInput(Capsule capsule)
    {
        if (string.IsNullOrWhiteSpace(capsule.Title) || capsule.Title.Length > 120)
            throw new InvalidDataException("Give the task a title of 1–120 characters.");
        if (string.IsNullOrWhiteSpace(capsule.Goal) || capsule.Goal.Length > 600)
            throw new InvalidDataException("Describe your goal in 1–600 characters.");
        if (capsule.Evidence.Count == 0 || !capsule.Evidence.Any(e => !string.IsNullOrWhiteSpace(e.Text) || !string.IsNullOrWhiteSpace(e.Context) || !string.IsNullOrWhiteSpace(e.VisualDescription) || !string.IsNullOrWhiteSpace(e.WindowTitle)))
            throw new InvalidDataException("Add a note or readable screenshot before generating a capsule.");
        if (capsule.Evidence.Count(e => e.Kind == "image") > 3 || capsule.Evidence.Count(e => e.Kind == "note") > 3 || capsule.Evidence.Count(e => e.Kind == "voice") > 1)
            throw new InvalidDataException("Use at most three notes, three screenshots, and one voice handoff.");
        if (capsule.Evidence.Select(e => e.Id).Distinct().Count() != capsule.Evidence.Count)
            throw new InvalidDataException("Evidence identifiers must be unique.");
        if (capsule.Goal.Length + capsule.Evidence.Sum(e => e.Text.Length + e.Context.Length + e.VisualDescription.Length + e.WindowTitle.Length) > MaxInputCharacters)
            throw new InvalidDataException($"This capsule exceeds {MaxInputCharacters:N0} input characters. Shorten the evidence; nothing has been discarded.");
    }

    public static ResumeBrief ParseBrief(string json, Capsule capsule)
    {
        var brief = JsonSerializer.Deserialize<ResumeBrief>(json, Json) ?? throw new InvalidDataException("The model returned no capsule.");
        if (brief.Activity is null || brief.LastState is null || brief.Decisions is null || brief.OpenQuestions is null || brief.NextActions is null)
            throw new InvalidDataException("The model returned an incomplete capsule.");
        var ids = capsule.Evidence.Select(e => e.Id).ToHashSet();
        if (brief.AllClaims.Count() > 36) throw new InvalidDataException("The model returned too many statements.");
        foreach (var claim in brief.AllClaims)
        {
            if (claim is null || string.IsNullOrWhiteSpace(claim.Text) || claim.Text.Length > 600 || claim.EvidenceIds is null || claim.EvidenceIds.Count == 0 || claim.EvidenceIds.Any(id => !ids.Contains(id)))
                throw new InvalidDataException("The model returned an unsupported evidence reference. Your notes are safe; retry or edit them.");
            if (string.IsNullOrWhiteSpace(claim.SourceQuote) || !capsule.Evidence.Any(e => claim.EvidenceIds.Contains(e.Id) &&
                (e.Text.Contains(claim.SourceQuote, StringComparison.Ordinal) || e.Context.Contains(claim.SourceQuote, StringComparison.Ordinal) || e.VisualDescription.Contains(claim.SourceQuote, StringComparison.Ordinal) || e.WindowTitle.Contains(claim.SourceQuote, StringComparison.Ordinal))))
                throw new InvalidDataException("A supporting quote was not present in its cited source. Copy an exact, contiguous quote from the evidence.");
            claim.UserEdited = false;
        }
        if (!brief.AllClaims.Any()) throw new InvalidDataException("No supported statements were generated. Add a clearer handoff note.");
        return brief;
    }

    public static Capsule Demo()
    {
        var capsule = new Capsule { Title = "Choose an offline search engine", Goal = "Choose a search approach for my college project that runs offline and stays below 2 GB of RAM." };
        capsule.Evidence.Add(new EvidenceItem { Label = "Experiment notes", Text = "I tested Approach A. It requires an internet connection to query its index, so I rejected it for this offline project. Approach B can query a local index. I have not measured B's memory usage yet." });
        capsule.Evidence.Add(new EvidenceItem { Label = "Handoff to tomorrow", Text = "When I return, run Approach B on the 100-document sample and measure peak RAM. If it stays below 2 GB, compare its search accuracy next. I have not selected a final approach." });
        return capsule;
    }
}
