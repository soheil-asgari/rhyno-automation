using System.Text;
using System.Text.RegularExpressions;
using OfficeAutomation.Utilities;

namespace OfficeAutomation.Services.Contracts;

public interface IContractDiffService
{
    /// <summary>
    /// Compares old text/HTML against new text/HTML and returns HTML with track-changes highlighting
    /// (<ins> for insertions in green, <del> for deletions in red).
    /// </summary>
    string GenerateRedlineDiff(string? oldText, string? newText);
}

public class ContractDiffService : IContractDiffService
{
    public string GenerateRedlineDiff(string? oldText, string? newText)
    {
        oldText ??= string.Empty;
        newText ??= string.Empty;

        if (string.IsNullOrWhiteSpace(oldText) && string.IsNullOrWhiteSpace(newText))
            return "<div class='text-slate-400 text-xs text-center py-2'>هیچ متنی برای مقایسه وجود ندارد.</div>";

        if (string.IsNullOrWhiteSpace(oldText))
        {
            return $"<ins class='diff-ins bg-emerald-100 text-emerald-800 dark:bg-emerald-950/50 dark:text-emerald-300 px-1 py-0.5 rounded font-semibold no-underline'>{HtmlSanitizer.Sanitize(newText)}</ins>";
        }

        if (string.IsNullOrWhiteSpace(newText))
        {
            return $"<del class='diff-del bg-rose-100 text-rose-800 dark:bg-rose-950/50 dark:text-rose-300 px-1 py-0.5 rounded font-semibold line-through'>{HtmlSanitizer.Sanitize(oldText)}</del>";
        }

        // Tokenize by whitespace/words to perform word-level diff
        var oldWords = Tokenize(oldText);
        var newWords = Tokenize(newText);

        var diffChunks = ComputeLcsDiff(oldWords, newWords);

        var sb = new StringBuilder();
        foreach (var chunk in diffChunks)
        {
            if (chunk.Type == DiffType.Unchanged)
            {
                sb.Append(chunk.Text);
            }
            else if (chunk.Type == DiffType.Inserted)
            {
                sb.Append($"<ins class='diff-ins bg-emerald-100 text-emerald-800 dark:bg-emerald-950/50 dark:text-emerald-300 px-1 py-0.5 rounded font-semibold no-underline' title='افزوده شده'>{chunk.Text}</ins>");
            }
            else if (chunk.Type == DiffType.Deleted)
            {
                sb.Append($"<del class='diff-del bg-rose-100 text-rose-800 dark:bg-rose-950/50 dark:text-rose-300 px-1 py-0.5 rounded font-semibold line-through' title='حذف شده'>{chunk.Text}</del>");
            }
        }

        return sb.ToString();
    }

    private static List<string> Tokenize(string text)
    {
        var matches = Regex.Matches(text, @"\S+|\s+");
        var tokens = new List<string>(matches.Count);
        foreach (Match m in matches)
        {
            tokens.Add(m.Value);
        }
        return tokens;
    }

    private enum DiffType { Unchanged, Inserted, Deleted }

    private class DiffChunk
    {
        public DiffType Type { get; set; }
        public string Text { get; set; } = string.Empty;
    }

    private static List<DiffChunk> ComputeLcsDiff(List<string> oldTokens, List<string> newTokens)
    {
        int n = oldTokens.Count;
        int m = newTokens.Count;
        int[,] lcs = new int[n + 1, m + 1];

        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j < m; j++)
            {
                if (oldTokens[i] == newTokens[j])
                    lcs[i + 1, j + 1] = lcs[i, j] + 1;
                else
                    lcs[i + 1, j + 1] = Math.Max(lcs[i + 1, j], lcs[i, j + 1]);
            }
        }

        var chunks = new List<DiffChunk>();
        int curI = n, curJ = m;

        var revChunks = new List<DiffChunk>();
        while (curI > 0 || curJ > 0)
        {
            if (curI > 0 && curJ > 0 && oldTokens[curI - 1] == newTokens[curJ - 1])
            {
                revChunks.Add(new DiffChunk { Type = DiffType.Unchanged, Text = oldTokens[curI - 1] });
                curI--;
                curJ--;
            }
            else if (curJ > 0 && (curI == 0 || lcs[curI, curJ - 1] >= lcs[curI - 1, curJ]))
            {
                revChunks.Add(new DiffChunk { Type = DiffType.Inserted, Text = newTokens[curJ - 1] });
                curJ--;
            }
            else if (curI > 0 && (curJ == 0 || lcs[curI, curJ - 1] < lcs[curI - 1, curJ]))
            {
                revChunks.Add(new DiffChunk { Type = DiffType.Deleted, Text = oldTokens[curI - 1] });
                curI--;
            }
        }

        revChunks.Reverse();

        foreach (var chunk in revChunks)
        {
            if (chunks.Count > 0 && chunks[^1].Type == chunk.Type)
            {
                chunks[^1].Text += chunk.Text;
            }
            else
            {
                chunks.Add(chunk);
            }
        }

        return chunks;
    }
}
