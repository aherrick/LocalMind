using System.Text;

namespace LocalMind.Services;

// Reasoning models (DeepSeek-R1) stream their chain of thought inline as <think>...</think> when the
// server has no reasoning parser. Providers that do split it out map it to TextReasoningContent, which
// ChatResponseUpdate.Text already excludes, so only the inline case needs handling here.

// TODO: revisit once Foundry Local's reasoning parsing is complete; remove this filter when installed
// versions consistently return clean answer text during streaming and follow-up turns.
//   - enable_thinking template setting (still open):    https://github.com/microsoft/foundry-local/pull/1009
//   - reasoning isolation when the template opens the block (merged to main after v2.0.1, not in any release yet): https://github.com/microsoft/foundry-local/pull/1083
internal sealed class ThinkingTextFilter
{
    private const string StartTag = "<think>";
    private const string EndTag = "</think>";

    private readonly StringBuilder _buffer = new();
    private bool _passthrough;

    public string Append(string text)
    {
        if (_passthrough)
        {
            return text;
        }

        _buffer.Append(text);
        var raw = _buffer.ToString();

        var end = raw.IndexOf(EndTag, StringComparison.OrdinalIgnoreCase);
        if (end >= 0)
        {
            _passthrough = true;
            _buffer.Clear();
            return raw[(end + EndTag.Length)..].TrimStart();
        }

        // Keep buffering only while the response could still turn out to open with a <think> block.
        if (IsPartialStartTag(raw.TrimStart()))
        {
            return "";
        }

        _passthrough = true;
        _buffer.Clear();
        return raw;
    }

    public static string Remove(string text) => new ThinkingTextFilter().Append(text);

    private static bool IsPartialStartTag(string text)
    {
        var length = Math.Min(text.Length, StartTag.Length);
        return text.AsSpan(0, length)
            .Equals(StartTag.AsSpan(0, length), StringComparison.OrdinalIgnoreCase);
    }
}