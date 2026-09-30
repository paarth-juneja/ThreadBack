namespace ThreadBack.Core;

public static class ActivityContext
{
    // Keep source IDs and exact text excerpts for citations. The full sources remain in the encrypted store.
    public static Capsule ForGeneration(Capsule source)
    {
        if (!source.Evidence.Any(e => e.Kind is "eye" or "snapshot" or "image" or "update")) return source;
        var result = new Capsule { Id = source.Id, Title = source.Title, Goal = source.Goal };
        // Keep the whole sequence, but bound OCR sent to the text model so a
        // screen-heavy task remains practical on the supported CPU runtime.
        var remaining = Math.Min(CapsuleRules.MaxInputCharacters, 6000) - source.Goal.Length;
        var screensRemaining = source.Evidence.Count(GenerationPassages.IsScreen);
        foreach (var e in source.Evidence.OrderBy(e => GenerationPassages.IsScreen(e)).ThenByDescending(e => e.CapturedAt))
        {
            EvidenceItem selected;
            if (GenerationPassages.IsScreen(e))
            {
                var observation = e.VisualDescription[..Math.Min(450, e.VisualDescription.Length)];
                var ocrLimit = Math.Clamp(remaining / Math.Max(1, screensRemaining--) - e.Context.Length - observation.Length - e.WindowTitle.Length, 0, 650);
                // A visual description already incorporates the screenshot. Retain OCR
                // only as a short, exact excerpt when no description is available.
                var ocr = string.IsNullOrWhiteSpace(observation) ? OcrExcerpt(e.Text, ocrLimit) : "";
                selected = new EvidenceItem { Id = e.Id, Kind = e.Kind, Label = e.Label, Text = ocr, Context = e.Context, VisualDescription = observation, WindowTitle = e.WindowTitle, CapturedAt = e.CapturedAt };
            }
            else selected = e;
            if (string.IsNullOrWhiteSpace(selected.Text) && string.IsNullOrWhiteSpace(selected.Context) && string.IsNullOrWhiteSpace(selected.VisualDescription) && string.IsNullOrWhiteSpace(selected.WindowTitle) || selected.Text.Length + selected.Context.Length + selected.VisualDescription.Length + selected.WindowTitle.Length > remaining) continue;
            if (e.Kind is "note" or "image" or "voice")
            {
                var limit = e.Kind == "voice" ? 1 : 3;
                if (result.Evidence.Count(x => x.Kind == e.Kind) >= limit) continue;
            }
            result.Evidence.Add(selected); remaining -= selected.Text.Length + selected.Context.Length + selected.VisualDescription.Length + selected.WindowTitle.Length;
            if (result.Evidence.Count == 20) break;
        }
        return result;
    }

    private static string OcrExcerpt(string text, int limit)
    {
        if (limit <= 0 || text.Length == 0) return "";
        var start = 0;
        foreach (System.Text.RegularExpressions.Match line in System.Text.RegularExpressions.Regex.Matches(text, @"[^\r\n]+"))
        {
            if (line.Length >= 25 && line.Value.Count(char.IsLetter) >= 12) { start = line.Index; break; }
        }
        return text.Substring(start, Math.Min(limit, text.Length - start));
    }
}
