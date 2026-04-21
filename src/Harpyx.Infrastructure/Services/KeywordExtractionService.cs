using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace Harpyx.Infrastructure.Services;

public interface IKeywordExtractionService
{
    IReadOnlyList<string> ExtractKeywords(string text, int maxCount, bool useRake);
}

public class KeywordExtractionService : IKeywordExtractionService
{
    private readonly ILogger<KeywordExtractionService> _logger;

    public KeywordExtractionService(ILogger<KeywordExtractionService> logger)
    {
        _logger = logger;
    }

    public IReadOnlyList<string> ExtractKeywords(string text, int maxCount, bool useRake)
    {
        if (!useRake)
            return LocalKeywordExtractor.Extract(text, maxCount);

        try
        {
            var rake = RakeKeywordExtractor.Extract(text, maxCount);
            if (rake.Count > 0)
                return rake;

            _logger.LogDebug("RAKE produced no keywords. Falling back to local keyword extraction.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "RAKE keyword extraction failed. Falling back to local keyword extraction.");
        }

        return LocalKeywordExtractor.Extract(text, maxCount);
    }
}

internal static class KeywordExtractionShared
{
    public static readonly Regex TokenRegex = new(@"[\p{L}\p{Nd}][\p{L}\p{Nd}_-]{1,}", RegexOptions.Compiled);

    public static readonly HashSet<string> Stopwords = new(StringComparer.OrdinalIgnoreCase)
    {
        "a","ad","ai","al","alla","alle","allo","all","anche","ancora","avere","con","come","cui","da","dal","dalla","dalle",
        "dello","dei","del","della","delle","di","ed","e","gli","ha","hanno","ho","i","il","in","io","la","le","li","lo","ma",
        "mi","ne","nei","nel","nella","nelle","noi","non","o","per","piu","puo","se","si","sono","su","tra","un","una","uno",
        "all","an","and","any","are","as","at","be","but","by","can","do","for","from","has","have","if","in","is","it","its",
        "of","on","or","that","the","their","there","they","this","to","was","were","which","with","you","your"
    };

    public static bool IsCandidateToken(string token)
        => token.Length >= 3 && !Stopwords.Contains(token);
}

internal static class LocalKeywordExtractor
{
    public static IReadOnlyList<string> Extract(string text, int maxCount)
    {
        if (string.IsNullOrWhiteSpace(text) || maxCount <= 0)
            return Array.Empty<string>();

        var frequencies = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in KeywordExtractionShared.TokenRegex.Matches(text))
        {
            var token = match.Value.Trim().ToLowerInvariant();
            if (!KeywordExtractionShared.IsCandidateToken(token))
                continue;

            frequencies[token] = frequencies.TryGetValue(token, out var count) ? count + 1 : 1;
        }

        return frequencies
            .OrderByDescending(kv => kv.Value)
            .ThenByDescending(kv => kv.Key.Length)
            .ThenBy(kv => kv.Key, StringComparer.Ordinal)
            .Take(maxCount)
            .Select(kv => kv.Key)
            .ToList();
    }
}

internal static class RakeKeywordExtractor
{
    private static readonly Regex SentenceSplitRegex = new(@"[\r\n]+|[.!?,;:()\[\]{}""'`|/\\]+", RegexOptions.Compiled);

    public static IReadOnlyList<string> Extract(string text, int maxCount)
    {
        if (string.IsNullOrWhiteSpace(text) || maxCount <= 0)
            return Array.Empty<string>();

        var phrases = BuildCandidatePhrases(text);
        if (phrases.Count == 0)
            return Array.Empty<string>();

        var wordFrequency = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var wordDegree = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var phrase in phrases)
        {
            var words = phrase.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0)
                continue;

            var degree = Math.Max(0, words.Length - 1);
            foreach (var word in words)
            {
                wordFrequency[word] = wordFrequency.TryGetValue(word, out var freq) ? freq + 1 : 1;
                wordDegree[word] = wordDegree.TryGetValue(word, out var currentDegree) ? currentDegree + degree : degree;
            }
        }

        var wordScore = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        foreach (var (word, frequency) in wordFrequency)
        {
            var degree = wordDegree.TryGetValue(word, out var value) ? value : 0;
            wordScore[word] = (degree + frequency) / (double)frequency;
        }

        var ranked = phrases
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(phrase =>
            {
                var score = phrase.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                    .Where(wordScore.ContainsKey)
                    .Sum(word => wordScore[word]);
                return new { Phrase = phrase, Score = score };
            })
            .OrderByDescending(x => x.Score)
            .ThenByDescending(x => x.Phrase.Length)
            .ThenBy(x => x.Phrase, StringComparer.Ordinal)
            .Take(maxCount)
            .Select(x => x.Phrase)
            .ToList();

        return ranked;
    }

    private static List<string> BuildCandidatePhrases(string text)
    {
        var candidates = new List<string>();
        var sentences = SentenceSplitRegex.Split(text);
        foreach (var sentence in sentences)
        {
            var words = KeywordExtractionShared.TokenRegex.Matches(sentence)
                .Select(m => m.Value.Trim().ToLowerInvariant())
                .ToList();

            if (words.Count == 0)
                continue;

            var current = new List<string>();
            foreach (var word in words)
            {
                if (KeywordExtractionShared.IsCandidateToken(word))
                {
                    current.Add(word);
                    continue;
                }

                FlushCurrent();
            }

            FlushCurrent();

            void FlushCurrent()
            {
                if (current.Count == 0)
                    return;

                var phrase = string.Join(' ', current);
                current.Clear();
                if (phrase.Length >= 3)
                    candidates.Add(phrase);
            }
        }

        return candidates;
    }
}
