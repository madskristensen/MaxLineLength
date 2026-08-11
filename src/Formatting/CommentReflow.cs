using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis.Text;

namespace MaxLineLength
{
    internal static class CommentReflow
    {
        public static IReadOnlyList<TextChange> GetChanges(
            string text,
            IEnumerable<TextSpan> commentSpans,
            int maxLineLength,
            TextSpan? scope,
            string newLine,
            int tabSize,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SourceText sourceText = SourceText.From(text);
            var changes = new List<TextChange>();

            foreach (TextSpan span in MergeSpans(commentSpans, cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (span.End > sourceText.Length ||
                    (scope.HasValue && !scope.Value.Contains(span)) ||
                    !HasOverlengthLine(
                        sourceText,
                        span,
                        maxLineLength,
                        tabSize,
                        cancellationToken))
                {
                    continue;
                }

                string comment = sourceText.ToString(span);
                TextLine firstLine = sourceText.Lines.GetLineFromPosition(span.Start);
                string textBeforeComment = sourceText.ToString(
                    TextSpan.FromBounds(firstLine.Start, span.Start));
                string replacement = IsLineComment(comment)
                    ? WrapSingleLineComment(
                        comment,
                        textBeforeComment,
                        maxLineLength,
                        newLine,
                        tabSize,
                        cancellationToken)
                    : comment.StartsWith("/*", StringComparison.Ordinal)
                        ? WrapBlockComment(
                            comment,
                            textBeforeComment,
                            maxLineLength,
                            newLine,
                            tabSize,
                            cancellationToken)
                        : comment;

                if (!string.Equals(comment, replacement, StringComparison.Ordinal))
                {
                    changes.Add(new TextChange(span, replacement));
                }
            }

            return changes.OrderByDescending(change => change.Span.Start).ToArray();
        }

        internal static string WrapSingleLineComment(
            string comment,
            string textBeforeComment,
            int maxLineLength,
            string newLine,
            int tabSize,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int markerLength = GetLineCommentMarkerLength(comment);
            if (markerLength == 0)
            {
                return comment;
            }

            int contentStart = markerLength;
            while (contentStart < comment.Length && char.IsWhiteSpace(comment[contentStart]))
            {
                contentStart++;
            }

            string marker = comment.Substring(0, markerLength) + " ";
            string content = comment.Substring(contentStart);
            string leadingWhitespace = TextLineReflow.GetLeadingWhitespace(textBeforeComment);
            int firstWidth = maxLineLength -
                TextLineReflow.GetVisualLength(textBeforeComment + marker, tabSize);
            int continuationWidth = maxLineLength -
                TextLineReflow.GetVisualLength(leadingWhitespace + marker, tabSize);
            IReadOnlyList<string> lines = TextLineReflow.WrapWords(
                content,
                firstWidth,
                continuationWidth,
                cancellationToken);

            if (lines.Count < 2)
            {
                return comment;
            }

            return marker + lines[0] +
                string.Concat(lines.Skip(1).Select(
                    line => newLine + leadingWhitespace + marker + line));
        }

        private static bool IsLineComment(string comment)
        {
            return GetLineCommentMarkerLength(comment) > 0;
        }

        private static int GetLineCommentMarkerLength(string comment)
        {
            if (comment.StartsWith("//", StringComparison.Ordinal))
            {
                int length = 2;
                while (length < comment.Length && comment[length] == '/')
                {
                    length++;
                }

                return length;
            }

            if (comment.StartsWith("--", StringComparison.Ordinal))
            {
                return 2;
            }

            if (comment.StartsWith("'", StringComparison.Ordinal))
            {
                int length = 1;
                while (length < comment.Length && comment[length] == '\'')
                {
                    length++;
                }

                return length;
            }

            return 0;
        }

        private static string WrapBlockComment(
            string comment,
            string textBeforeComment,
            int maxLineLength,
            string newLine,
            int tabSize,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SourceText commentText = SourceText.From(comment);
            var result = new List<string>(commentText.Lines.Count);
            bool changed = false;

            for (int lineNumber = 0; lineNumber < commentText.Lines.Count; lineNumber++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string line = commentText.Lines[lineNumber].ToString();
                string replacement = WrapBlockCommentLine(
                    line,
                    lineNumber == 0 ? textBeforeComment : string.Empty,
                    maxLineLength,
                    newLine,
                    tabSize,
                    cancellationToken);

                changed |= !string.Equals(line, replacement, StringComparison.Ordinal);
                result.Add(replacement);
            }

            return changed ? string.Join(newLine, result) : comment;
        }

        private static string WrapBlockCommentLine(
            string line,
            string textBeforeLine,
            int maxLineLength,
            string newLine,
            int tabSize,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int markerStart = 0;
            while (markerStart < line.Length && char.IsWhiteSpace(line[markerStart]))
            {
                markerStart++;
            }

            int contentStart;
            bool hasOpeningMarker =
                line.Substring(markerStart).StartsWith("/*", StringComparison.Ordinal);
            if (hasOpeningMarker)
            {
                contentStart = markerStart + 2;
                while (contentStart < line.Length && line[contentStart] == '*')
                {
                    contentStart++;
                }
            }
            else if (markerStart < line.Length && line[markerStart] == '*')
            {
                contentStart = markerStart + 1;
            }
            else
            {
                contentStart = markerStart;
            }

            while (contentStart < line.Length && char.IsWhiteSpace(line[contentStart]))
            {
                contentStart++;
            }

            int closingStart = line.LastIndexOf("*/", StringComparison.Ordinal);
            bool hasClosingMarker = closingStart >= contentStart;
            int contentEnd = hasClosingMarker ? closingStart : line.Length;
            string content = line.Substring(contentStart, contentEnd - contentStart).Trim();
            if (content.Length == 0)
            {
                return line;
            }

            string prefix = line.Substring(0, contentStart);
            string leadingWhitespace = TextLineReflow.GetLeadingWhitespace(
                textBeforeLine.Length > 0 ? textBeforeLine : line);
            string continuationPrefix = leadingWhitespace +
                (hasOpeningMarker ? " * " : "* ");
            int firstWidth = maxLineLength -
                TextLineReflow.GetVisualLength(textBeforeLine + prefix, tabSize);
            int continuationWidth = maxLineLength -
                TextLineReflow.GetVisualLength(continuationPrefix, tabSize);
            IReadOnlyList<string> wrapped = TextLineReflow.WrapWords(
                content,
                firstWidth,
                continuationWidth,
                cancellationToken);

            if (wrapped.Count < 2)
            {
                return line;
            }

            string suffix = hasClosingMarker ? " */" : string.Empty;
            return prefix + wrapped[0] +
                string.Concat(wrapped.Skip(1).Select(
                    part => newLine + continuationPrefix + part)) +
                suffix;
        }

        private static bool HasOverlengthLine(
            SourceText sourceText,
            TextSpan span,
            int maxLineLength,
            int tabSize,
            CancellationToken cancellationToken)
        {
            int firstLine = sourceText.Lines.GetLineFromPosition(span.Start).LineNumber;
            int lastPosition = span.IsEmpty ? span.Start : span.End - 1;
            int lastLine = sourceText.Lines.GetLineFromPosition(lastPosition).LineNumber;

            for (int lineNumber = firstLine; lineNumber <= lastLine; lineNumber++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (TextLineReflow.GetVisualLength(
                    sourceText.Lines[lineNumber].ToString(),
                    tabSize) > maxLineLength)
                {
                    return true;
                }
            }

            return false;
        }

        private static IEnumerable<TextSpan> MergeSpans(
            IEnumerable<TextSpan> spans,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            TextSpan? current = null;

            foreach (TextSpan span in spans.Where(span => !span.IsEmpty).OrderBy(span => span.Start))
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!current.HasValue)
                {
                    current = span;
                }
                else if (span.Start < current.Value.End)
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
    }
}
