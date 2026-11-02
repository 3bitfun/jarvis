using System.Text;

namespace JARVIS;

/// <summary>Streams ordinary text while withholding structured JARVIS tool blocks until processed.</summary>
internal sealed class SafeStreamingText(Action<string> output)
{
    private readonly StringBuilder _pending = new();
    private bool _withholding;

    public void Append(string part)
    {
        if (_withholding || string.IsNullOrEmpty(part)) return;
        _pending.Append(part);
        while (_pending.Length > 0)
        {
            var value = _pending.ToString();
            var fence = value.IndexOf("```", StringComparison.Ordinal);
            if (fence < 0)
            {
                var keep = value.EndsWith("``", StringComparison.Ordinal) ? 2 : value.EndsWith('`') ? 1 : 0;
                Emit(value.Length - keep); return;
            }
            if (fence > 0) { Emit(fence); continue; }
            var ticks = 0; while (ticks < value.Length && value[ticks] == '`') ticks++;
            if (ticks > 3) { Emit(ticks - 3); continue; }
            if (value.Length == 3) return;
            var remainder = value[3..];
            const string prefix = "jarvis-";
            if (remainder.Length < prefix.Length && prefix.StartsWith(remainder, StringComparison.OrdinalIgnoreCase)) return;
            if (remainder.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) { _withholding = true; _pending.Clear(); return; }
            Emit(3);
        }
    }

    private void Emit(int length)
    {
        if (length <= 0) return;
        var text = _pending.ToString(0, length); _pending.Remove(0, length); output(text);
    }
}
