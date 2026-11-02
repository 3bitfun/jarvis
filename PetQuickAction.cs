using System.Text.RegularExpressions;

namespace JARVIS;

internal enum PetActionKind { OpenApp, Timer, TimerStatus, StopTimer, Note, Invalid }
internal sealed record PetQuickAction(PetActionKind Kind, string Value, bool Desktop = false, int Minutes = 0)
{
    public static PetQuickAction? Parse(string text)
    {
        var input = text.Trim();
        if (Regex.IsMatch(input, @"^(?:stop|cancel)\s+(?:the\s+|my\s+)?(?:focus\s+)?timer[.!]?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)) return new(PetActionKind.StopTimer, "");
        if (Regex.IsMatch(input, @"^(?:timer status|(?:how much time|how long)(?: is)? left(?: on (?:the |my )?(?:focus )?timer)?)[?.!]*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)) return new(PetActionKind.TimerStatus, "");
        var timed = Regex.Match(input, @"^(?:start|set)\s+(?:a\s+)?(?<minutes>\d{1,3})\s*(?:minute|min)(?:s)?\s+(?:focus\s+)?timer[.!]?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (timed.Success)
        {
            if (!int.TryParse(timed.Groups["minutes"].Value, out var duration) || duration is < 1 or > 180) return new(PetActionKind.Invalid, "Choose a timer between 1 and 180 minutes.");
            return new(PetActionKind.Timer, input, Minutes: duration);
        }
        var note = Regex.Match(input, @"^(?:make|create|write)\s+(?:a\s+)?note(?<desktop>\s+on\s+(?:my\s+)?desktop)?\s*:\s*(?<content>.+)$", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant);
        if (note.Success)
        {
            var content = note.Groups["content"].Value.Trim();
            return content.Length == 0 ? null : new(PetActionKind.Note, content, note.Groups["desktop"].Success);
        }
        var command = Regex.Match(input, @"^(?:open|launch|start)\s+(.+?)[.!]?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (!command.Success) return null;
        var target = command.Groups[1].Value.Trim();
        var timer = Regex.Match(target, @"^(?:a\s+)?(?:focus\s+)?timer\s+(?:for\s+)?(\d{1,3})\s*(?:minutes?|mins?)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (timer.Success)
        {
            if (!int.TryParse(timer.Groups[1].Value, out var minutes) || minutes is < 1 or > 180) return new(PetActionKind.Invalid, "Choose a timer between 1 and 180 minutes.");
            return new(PetActionKind.Timer, target, Minutes: minutes);
        }
        return target.Length == 0 ? null : new(PetActionKind.OpenApp, target);
    }
}
