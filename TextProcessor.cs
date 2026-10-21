using System.Text.RegularExpressions;

namespace Transcriber;

/// <summary>
/// Очистка текста. Порт функций clean_text и final_text_cleanup из Python-версии.
/// </summary>
public static class TextProcessor
{
    private static readonly Regex Spaces = new(@"\s+", RegexOptions.Compiled);
    private static readonly Regex SpaceBeforePunct = new(@"\s+([,.!?;:])", RegexOptions.Compiled);
    private static readonly Regex NoSpaceAfterPunct = new(@"([,.!?;:])(?=[А-Яа-яЁёA-Za-z])", RegexOptions.Compiled);
    private static readonly Regex LowerAfterEnd = new(@"([.!?])\s+([а-яё])", RegexOptions.Compiled);
    private static readonly Regex SentenceSplit = new(@"(?<=[.!?…])\s+", RegexOptions.Compiled);
    private static readonly Regex TerminalPunctuation = new(@"[.!?…][""'»”’\)\]]*$", RegexOptions.Compiled);

    // Сегменты вида [музыка], (тишина), [BLANK_AUDIO], ♪ — это не речь.
    private static readonly Regex NonSpeech = new(@"^\s*(\[[^\]]*\]|\([^)]*\)|♪+)\s*$", RegexOptions.Compiled);

    public static bool IsNonSpeech(string segmentText) => NonSpeech.IsMatch(segmentText);

    /// <summary>Первичная очистка после Whisper.</summary>
    public static string CleanText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "";

        text = Spaces.Replace(text, " ").Trim();
        text = SpaceBeforePunct.Replace(text, "$1");
        return text;
    }

    /// <summary>Финальная обработка: пробелы, заглавные буквы, абзацы по 5 предложений.</summary>
    public static string FinalCleanup(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "";

        text = Spaces.Replace(text, " ").Trim();
        text = SpaceBeforePunct.Replace(text, "$1");
        text = NoSpaceAfterPunct.Replace(text, "$1 ");

        text = LowerAfterEnd.Replace(
            text,
            m => m.Groups[1].Value + " " + m.Groups[2].Value.ToUpperInvariant());

        if (text.Length > 0)
            text = char.ToUpperInvariant(text[0]) + text.Substring(1);

        var paragraphs = new List<string>();
        var current = new List<string>();

        foreach (var raw in SentenceSplit.Split(text))
        {
            var sentence = raw.Trim();
            if (sentence.Length == 0)
                continue;

            current.Add(sentence);

            if (current.Count >= 5)
            {
                paragraphs.Add(string.Join(" ", current));
                current.Clear();
            }
        }

        if (current.Count > 0)
            paragraphs.Add(string.Join(" ", current));

        return string.Join("\n\n", paragraphs);
    }

    /// <summary>
    /// Сохраняет границы фраз, которые вернул Whisper, восстанавливает конец фразы
    /// там, где распознаватель не поставил знак, и разбивает текст на короткие абзацы.
    /// </summary>
    public static string FormatTranscript(IEnumerable<string> segments)
    {
        var sentences = new List<string>();

        foreach (string raw in segments)
        {
            string segment = CleanText(raw);
            if (segment.Length == 0 || IsNonSpeech(segment))
                continue;

            if (!TerminalPunctuation.IsMatch(segment))
                segment += ".";

            foreach (string rawSentence in SentenceSplit.Split(segment))
            {
                string sentence = rawSentence.Trim();
                if (sentence.Length == 0)
                    continue;

                sentence = char.ToUpperInvariant(sentence[0]) + sentence[1..];
                sentences.Add(sentence);
            }
        }

        var paragraphs = new List<string>();
        for (int i = 0; i < sentences.Count; i += 4)
            paragraphs.Add(string.Join(" ", sentences.Skip(i).Take(4)));

        return string.Join("\n\n", paragraphs);
    }

    /// <summary>Формат времени как в Python-версии: м:сс или ч:мм:сс.</summary>
    public static string FormatDuration(double seconds)
    {
        int total = (int)Math.Max(0, seconds);
        int h = total / 3600;
        int m = total % 3600 / 60;
        int s = total % 60;

        return h > 0 ? $"{h}:{m:00}:{s:00}" : $"{m}:{s:00}";
    }
}
