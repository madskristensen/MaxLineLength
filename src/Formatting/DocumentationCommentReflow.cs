using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis.Text;

namespace MaxLineLength
{
    internal static class DocumentationCommentReflow
    {
        private static readonly string[] _protectedElements =
        {
            "code",
            "example",
            "list",
            "pre"
        };

        public static IReadOnlyList<TextChange> GetChanges(
            string text,
            IEnumerable<TextSpan> documentationSpans,
            int maxLineLength,
            TextSpan? scope,
            string newLine,
            int tabSize,
            IReadOnlyList<TextSpan> suppressedSpans,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SourceText sourceText = SourceText.From(text);
            var changes = new List<TextChange>();

            foreach (TextSpan span in MergeDocumentationSpans(sourceText, documentationSpans))
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (span.End > sourceText.Length ||
                    (scope.HasValue && !scope.Value.Contains(span)) ||
                    ReflowSuppression.OverlapsAny(suppressedSpans, span) ||
                    !HasOverlengthLine(sourceText, span, maxLineLength, tabSize))
                {
                    continue;
                }

                string original = sourceText.ToString(span);
                TextLine firstLine = sourceText.Lines.GetLineFromPosition(span.Start);
                string externalIndentation = sourceText.ToString(
                    TextSpan.FromBounds(firstLine.Start, span.Start));
                string replacement = ReflowBlock(
                    original,
                    externalIndentation,
                    maxLineLength,
                    newLine,
                    tabSize,
                    cancellationToken);

                if (!string.Equals(original, replacement, StringComparison.Ordinal))
                {
                    changes.Add(new TextChange(span, replacement));
                }
            }

            return changes.OrderByDescending(change => change.Span.Start).ToArray();
        }

        internal static string ReflowBlock(
            string comment,
            string externalIndentation,
            int maxLineLength,
            string newLine,
            int tabSize,
            CancellationToken cancellationToken = default)
        {
            SourceText commentText = SourceText.From(comment);
            var result = new List<string>();
            var paragraph = new List<DocumentationLine>();
            int protectedDepth = 0;

            foreach (TextLine textLine in commentText.Lines)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string line = textLine.ToString();

                if (!TryParseLine(line, externalIndentation, out DocumentationLine documentationLine))
                {
                    FlushParagraph(paragraph, result, maxLineLength, tabSize, cancellationToken);
                    result.Add(line);
                    continue;
                }

                string content = documentationLine.Content;
                bool protectedLine = protectedDepth > 0 || ContainsProtectedOpening(content);
                if (protectedLine)
                {
                    FlushParagraph(paragraph, result, maxLineLength, tabSize, cancellationToken);
                    result.Add(line);
                    protectedDepth += CountProtectedOpenings(content);
                    protectedDepth = Math.Max(0, protectedDepth - CountProtectedClosings(content));
                }
                else if (content.Length == 0 || IsTagOnly(content))
                {
                    FlushParagraph(paragraph, result, maxLineLength, tabSize, cancellationToken);
                    result.Add(line);
                }
                else
                {
                    paragraph.Add(documentationLine);
                }
            }

            FlushParagraph(paragraph, result, maxLineLength, tabSize, cancellationToken);
            return string.Join(newLine, result);
        }

        private static void FlushParagraph(
            ICollection<DocumentationLine> paragraph,
            ICollection<string> result,
            int maxLineLength,
            int tabSize,
            CancellationToken cancellationToken)
        {
            if (paragraph.Count == 0)
            {
                return;
            }

            DocumentationLine first = paragraph.First();
            string content = string.Join(" ", paragraph.Select(line => line.Content.Trim()));
            string protectedContent = ProtectTagWhitespace(content);
            int firstWidth = maxLineLength -
                TextLineReflow.GetVisualLength(first.WidthPrefix, tabSize);
            int continuationWidth = maxLineLength -
                TextLineReflow.GetVisualLength(first.ContinuationPrefix, tabSize);
            IReadOnlyList<string> wrapped = TextLineReflow.WrapWords(
                protectedContent,
                firstWidth,
                continuationWidth,
                cancellationToken);

            for (int index = 0; index < wrapped.Count; index++)
            {
                string prefix = index == 0 ? first.Prefix : first.ContinuationPrefix;
                result.Add(prefix + RestoreTagWhitespace(wrapped[index]));
            }

            paragraph.Clear();
        }

        private static bool TryParseLine(
            string line,
            string externalIndentation,
            out DocumentationLine documentationLine)
        {
            int markerStart = 0;
            while (markerStart < line.Length && char.IsWhiteSpace(line[markerStart]))
            {
                markerStart++;
            }

            if (markerStart + 3 > line.Length)
            {
                documentationLine = default;
                return false;
            }

            string marker = line.Substring(markerStart, 3);
            if (marker != "///" && marker != "'''")
            {
                documentationLine = default;
                return false;
            }

            int contentStart = markerStart + marker.Length;
            while (contentStart < line.Length && char.IsWhiteSpace(line[contentStart]))
            {
                contentStart++;
            }

            string prefix = line.Substring(0, markerStart) + marker + " ";
            string widthPrefix = markerStart == 0 ? externalIndentation + prefix : prefix;
            documentationLine = new DocumentationLine(
                prefix,
                widthPrefix,
                widthPrefix,
                line.Substring(contentStart));
            return true;
        }

        private static bool IsTagOnly(string content)
        {
            bool inTag = false;
            bool sawTag = false;

            foreach (char character in content)
            {
                if (character == '<')
                {
                    inTag = true;
                    sawTag = true;
                }
                else if (character == '>')
                {
                    inTag = false;
                }
                else if (!inTag && !char.IsWhiteSpace(character))
                {
                    return false;
                }
            }

            return sawTag && !inTag;
        }

        private static bool ContainsProtectedOpening(string content)
        {
            return _protectedElements.Any(element =>
                ContainsNonSelfClosingOpeningTag(content, element));
        }

        private static int CountProtectedOpenings(string content)
        {
            return _protectedElements.Count(element =>
                ContainsNonSelfClosingOpeningTag(content, element));
        }

        private static bool ContainsNonSelfClosingOpeningTag(string content, string element)
        {
            string marker = "<" + element;
            int searchStart = 0;

            while (searchStart < content.Length)
            {
                int tagStart = content.IndexOf(
                    marker,
                    searchStart,
                    StringComparison.OrdinalIgnoreCase);
                if (tagStart < 0)
                {
                    return false;
                }

                int nameEnd = tagStart + marker.Length;
                if (nameEnd < content.Length &&
                    !char.IsWhiteSpace(content[nameEnd]) &&
                    content[nameEnd] != '>' &&
                    content[nameEnd] != '/')
                {
                    searchStart = nameEnd;
                    continue;
                }

                int tagEnd = content.IndexOf('>', nameEnd);
                if (tagEnd < 0)
                {
                    return false;
                }

                int lastContent = tagEnd - 1;
                while (lastContent >= nameEnd && char.IsWhiteSpace(content[lastContent]))
                {
                    lastContent--;
                }

                if (lastContent < nameEnd || content[lastContent] != '/')
                {
                    return true;
                }

                searchStart = tagEnd + 1;
            }

            return false;
        }

        private static int CountProtectedClosings(string content)
        {
            return _protectedElements.Count(element =>
                content.IndexOf("</" + element, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static string ProtectTagWhitespace(string content)
        {
            var characters = content.ToCharArray();
            bool inTag = false;

            for (int index = 0; index < characters.Length; index++)
            {
                if (characters[index] == '<')
                {
                    inTag = content.IndexOf('>', index + 1) >= 0;
                }
                else if (characters[index] == '>')
                {
                    inTag = false;
                }
                else if (inTag && characters[index] == ' ')
                {
                    characters[index] = '\uE000';
                }
                else if (inTag && characters[index] == '\t')
                {
                    characters[index] = '\uE001';
                }
            }

            return new string(characters);
        }

        private static string RestoreTagWhitespace(string content)
        {
            return content.Replace('\uE000', ' ').Replace('\uE001', '\t');
        }

        private static IEnumerable<TextSpan> MergeDocumentationSpans(
            SourceText sourceText,
            IEnumerable<TextSpan> spans)
        {
            TextSpan? current = null;

            foreach (TextSpan span in spans.Where(span => !span.IsEmpty).OrderBy(span => span.Start))
            {
                if (!current.HasValue)
                {
                    current = span;
                    continue;
                }

                bool overlaps = span.Start <= current.Value.End;
                int currentPosition = Math.Max(current.Value.Start, current.Value.End - 1);
                int currentLine = sourceText.Lines.GetLineFromPosition(currentPosition).LineNumber;
                int nextLine = sourceText.Lines.GetLineFromPosition(span.Start).LineNumber;
                bool isAdjacentLine = nextLine <= currentLine + 1;
                if (overlaps || isAdjacentLine)
                {
                    current = TextSpan.FromBounds(
                        current.Value.Start,
                        Math.Max(current.Value.End, span.End));
                }
                else
                {
                    yield return current.Value;
                    current = span;
                }
            }

            if (current.HasValue)
            {
                yield return current.Value;
            }
        }

        private static bool HasOverlengthLine(
            SourceText sourceText,
            TextSpan span,
            int maxLineLength,
            int tabSize)
        {
            int firstLine = sourceText.Lines.GetLineFromPosition(span.Start).LineNumber;
            int lastPosition = span.IsEmpty ? span.Start : span.End - 1;
            int lastLine = sourceText.Lines.GetLineFromPosition(lastPosition).LineNumber;

            for (int lineNumber = firstLine; lineNumber <= lastLine; lineNumber++)
            {
                if (TextLineReflow.GetVisualLength(sourceText.Lines[lineNumber].ToString(), tabSize) >
                    maxLineLength)
                {
                    return true;
                }
            }

            return false;
        }

        private readonly struct DocumentationLine
        {
            public DocumentationLine(
                string prefix,
                string widthPrefix,
                string continuationPrefix,
                string content)
            {
                Prefix = prefix;
                WidthPrefix = widthPrefix;
                ContinuationPrefix = continuationPrefix;
                Content = content;
            }

            public string Prefix { get; }

            public string WidthPrefix { get; }

            public string ContinuationPrefix { get; }

            public string Content { get; }
        }
    }
}
