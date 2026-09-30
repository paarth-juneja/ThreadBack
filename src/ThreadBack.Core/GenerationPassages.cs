using System.Text.RegularExpressions;

namespace ThreadBack.Core;

public sealed record GenerationPassage(string Id, EvidenceItem Evidence, string Text);

public static class GenerationPassages
{
    public const int MaxPassages = 24;
    public const int MaxCharacters = 4200;
    public const int MaxScreenPassages = 8;
    public static bool IsScreen(EvidenceItem item) => item.Kind is "eye" or "snapshot" or "image";

    public static List<GenerationPassage> Select(Capsule capsule)
    {
        var result = new List<GenerationPassage>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int remaining = MaxCharacters, screenCount = 0;
        // User statements take precedence over noisy screen text. Full sources stay stored.
        foreach (var source in capsule.Evidence.OrderBy(e => IsScreen(e)).ThenByDescending(e => e.CapturedAt))
        {
            var textInputs = IsScreen(source) ? new[] { source.Context, source.VisualDescription, source.WindowTitle, source.Text } : new[] { source.Text };
            foreach (var input in textInputs)
            foreach (var line in Regex.Split(input, @"(?<=[.!?][""'”’]?)\s+(?=(?!(?:It|They|This|That|These|Those|Its|Their)\b)[A-Z])|\r?\n"))
            {
                if (IsScreen(source) && result.Any(p => p.Evidence.Id == source.Id)) break;
                var text = line.Trim();
                if (text.Length == 0) continue;
                var metadata = ReferenceEquals(input, source.Context) || ReferenceEquals(input, source.VisualDescription) || ReferenceEquals(input, source.WindowTitle);
                if (IsScreen(source) && (screenCount >= MaxScreenPassages || !metadata && (text.Length < 25 || text.Count(char.IsLetter) < 15 || Regex.Matches(text, @"\S+").Count < 4))) continue;
                // Long passages are quoted as exact contiguous excerpts, never rewritten.
                if (text.Length > 500)
                {
                    int end = text.LastIndexOf(' ', 500);
                    text = text[..(end > 0 ? end : 500)].TrimEnd();
                }
                if (text.Length > remaining || !seen.Add(text)) continue;
                result.Add(new GenerationPassage("S" + (result.Count + 1), source, text));
                remaining -= text.Length;
                if (IsScreen(source)) screenCount++;
                if (result.Count == MaxPassages) return result;
                // Give each screenshot one place in the handoff before spending slots on more OCR from the same image.
                if (IsScreen(source)) break;
            }
        }
        return result;
    }
}
