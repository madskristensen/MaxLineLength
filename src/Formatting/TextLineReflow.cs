using System.Collections.Generic;
using Microsoft.CodeAnalysis.Text;

namespace MaxLineLength
{
    internal static class TextLineReflow
    {
        public static IReadOnlyList<TextChange> GetChanges(
            string text,
            int maxLineLength,
            TextSpan? scope,
            string newLine,
            int tabSize)
        {
            SourceText sourceText = SourceText.From(text);
            var changes = new List<TextChange>();

            foreach (TextLine line in sourceText.Lines)
            {
                if (line.Span.IsEmpty ||
                    (scope.HasValue && !scope.Value.Contains(line.Span)) ||
                    GetVisualLength(line.ToString(), tabSize) <= maxLineLength)
                {
                    continue;
                }

                string original = line.ToString();
                string replacement = WrapLine(original, maxLineLength, newLine, tabSize);
                if (!string.Equals(original, replacement, StringComparison.Ordinal))
                {
                    changes.Add(new TextChange(line.Span, replacement));
                }
            }

            return changes;
        }

        internal static string WrapLine(string line, int maxLineLength, string newLine, int tabSize)
        {
            string indentation = GetLeadingWhitespace(line);
            string remaining = line;
            var result = new List<string>();

            while (GetVisualLength(remaining, tabSize) > maxLineLength)
            {
                int breakIndex = FindLastWhitespaceAtOrBeforeColumn(remaining, maxLineLength, tabSize);
                if (breakIndex < indentation.Length || breakIndex <= 0)
                {
                    break;
                }

                result.Add(remaining.Substring(0, breakIndex).TrimEnd());
                remaining = indentation + remaining.Substring(breakIndex).TrimStart();
            }

            if (result.Count == 0)
            {
                return line;
            }

            result.Add(remaining);
            return string.Join(newLine, result);
        }

        internal static int GetVisualLength(string text, int tabSize)
        {
            int column = 0;
            foreach (char character in text)
            {
                column = character == '\t'
                    ? column + tabSize - (column % tabSize)
                    : column + 1;
            }

            return column;
        }

        internal static IReadOnlyList<string> WrapWords(
            string text,
            int firstWidth,
            int continuationWidth)
        {
            var lines = new List<string>();
            string remaining = text.Trim();
            int width = firstWidth;

            while (remaining.Length > width && width > 0)
            {
                int breakIndex = FindLastWhitespace(remaining, width);
                if (breakIndex <= 0)
                {
                    break;
                }

                lines.Add(remaining.Substring(0, breakIndex).TrimEnd());
                remaining = remaining.Substring(breakIndex).TrimStart();
                width = continuationWidth;
            }

            lines.Add(remaining);
            return lines;
        }

        private static int FindLastWhitespaceAtOrBeforeColumn(string text, int maxColumn, int tabSize)
        {
            int column = 0;
            int lastWhitespace = -1;

            for (int index = 0; index < text.Length && column <= maxColumn; index++)
            {
                char character = text[index];
                if (char.IsWhiteSpace(character))
                {
                    lastWhitespace = index;
                }

                column = character == '\t'
                    ? column + tabSize - (column % tabSize)
                    : column + 1;
            }

            return lastWhitespace;
        }

        internal static string GetLeadingWhitespace(string line)
        {
            int length = 0;
            while (length < line.Length && char.IsWhiteSpace(line[length]))
            {
                length++;
            }

            return line.Substring(0, length);
        }

        private static int FindLastWhitespace(string text, int width)
        {
            int lastWhitespace = -1;
            int limit = Math.Min(text.Length - 1, width);

            for (int index = 0; index <= limit; index++)
            {
                if (char.IsWhiteSpace(text[index]))
                {
                    lastWhitespace = index;
                }
            }

            return lastWhitespace;
        }
    }
}
