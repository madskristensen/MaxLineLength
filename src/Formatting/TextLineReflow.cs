using System.Collections.Generic;
using System.Threading;
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
            int tabSize,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SourceText sourceText = SourceText.From(text);
            IReadOnlyList<TextSpan> suppressedSpans = ReflowSuppression.GetSpans(sourceText);
            var changes = new List<TextChange>();

            foreach (TextLine line in sourceText.Lines)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (line.Span.IsEmpty ||
                    (scope.HasValue && !scope.Value.Contains(line.Span)) ||
                    ReflowSuppression.OverlapsAny(suppressedSpans, line.SpanIncludingLineBreak))
                {
                    continue;
                }

                string original = line.ToString();
                if (GetVisualLength(original, tabSize) <= maxLineLength)
                {
                    continue;
                }

                string replacement = WrapLine(
                    original,
                    maxLineLength,
                    newLine,
                    tabSize,
                    cancellationToken);
                if (!string.Equals(original, replacement, StringComparison.Ordinal))
                {
                    changes.Add(new TextChange(line.Span, replacement));
                }
            }

            return changes;
        }

        internal static string WrapLine(
            string line,
            int maxLineLength,
            string newLine,
            int tabSize,
            CancellationToken cancellationToken = default)
        {
            string indentation = GetLeadingWhitespace(line);
            int indentationWidth = GetVisualLength(indentation, tabSize);
            int contentStart = 0;
            bool isFirstLine = true;
            var result = new List<string>();

            while (TryFindWrapBreak(
                line,
                contentStart,
                isFirstLine ? 0 : indentationWidth,
                maxLineLength,
                tabSize,
                cancellationToken,
                out int breakIndex))
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (breakIndex <= contentStart ||
                    (isFirstLine && breakIndex < indentation.Length))
                {
                    break;
                }

                string segment = line.Substring(contentStart, breakIndex - contentStart).TrimEnd();
                result.Add(isFirstLine ? segment : indentation + segment);

                contentStart = breakIndex;
                while (contentStart < line.Length && char.IsWhiteSpace(line[contentStart]))
                {
                    contentStart++;
                }

                isFirstLine = false;
            }

            if (result.Count == 0)
            {
                return line;
            }

            string remainder = line.Substring(contentStart);
            result.Add(isFirstLine ? remainder : indentation + remainder);
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
            int continuationWidth,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var lines = new List<string>();
            string trimmed = text.Trim();
            int contentStart = 0;
            int width = firstWidth;

            while (trimmed.Length - contentStart > width && width > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int breakIndex = FindLastWhitespace(
                    trimmed,
                    contentStart,
                    width,
                    cancellationToken);
                if (breakIndex <= contentStart)
                {
                    break;
                }

                lines.Add(trimmed.Substring(contentStart, breakIndex - contentStart).TrimEnd());
                contentStart = breakIndex;
                while (contentStart < trimmed.Length && char.IsWhiteSpace(trimmed[contentStart]))
                {
                    contentStart++;
                }

                width = continuationWidth;
            }

            lines.Add(trimmed.Substring(contentStart));
            return lines;
        }

        private static bool TryFindWrapBreak(
            string text,
            int startIndex,
            int initialColumn,
            int maxColumn,
            int tabSize,
            CancellationToken cancellationToken,
            out int breakIndex)
        {
            int column = initialColumn;
            breakIndex = -1;

            for (int index = startIndex; index < text.Length && column <= maxColumn; index++)
            {
                if ((index & 1023) == 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }

                char character = text[index];
                if (char.IsWhiteSpace(character))
                {
                    breakIndex = index;
                }

                column = character == '\t'
                    ? column + tabSize - (column % tabSize)
                    : column + 1;
            }

            return column > maxColumn;
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

        private static int FindLastWhitespace(
            string text,
            int startIndex,
            int width,
            CancellationToken cancellationToken)
        {
            int lastWhitespace = -1;
            int limit = Math.Min(text.Length - 1, startIndex + width);

            for (int index = startIndex; index <= limit; index++)
            {
                if ((index & 1023) == 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }

                if (char.IsWhiteSpace(text[index]))
                {
                    lastWhitespace = index;
                }
            }

            return lastWhitespace;
        }
    }
}
