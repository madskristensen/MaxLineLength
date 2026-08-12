using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.VisualBasic;
using Microsoft.CodeAnalysis.VisualBasic.Syntax;

namespace MaxLineLength
{
    internal static class VisualBasicListReflow
    {
        public static IReadOnlyList<TextChange> GetChanges(
            string text,
            int maxLineLength,
            TextSpan? scope,
            string newLine,
            string indentUnit,
            int tabSize,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SourceText sourceText = SourceText.From(text);
            SyntaxTree tree = VisualBasicSyntaxTree.ParseText(
                sourceText,
                cancellationToken: cancellationToken);
            SyntaxNode root = tree.GetRoot(cancellationToken);
            SyntaxTrivia[] trivia = root.DescendantTrivia(descendIntoTrivia: false).ToArray();
            IReadOnlyList<TextSpan> suppressedSpans = ReflowSuppression.GetSpans(
                sourceText,
                trivia.Select(item => item.FullSpan));
            var changes = new Dictionary<int, TextChange>();

            foreach (SyntaxNode node in root.DescendantNodes())
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!IsSupportedList(node) ||
                    node.ContainsDiagnostics ||
                    (scope.HasValue && !scope.Value.Contains(node.Span)) ||
                    ReflowSuppression.OverlapsAny(suppressedSpans, node.FullSpan) ||
                    !HasOverlengthLine(node, sourceText, maxLineLength, tabSize))
                {
                    continue;
                }

                string indentation = GetLeadingWhitespace(sourceText, node.SpanStart) + indentUnit;
                foreach (SyntaxToken comma in node.ChildTokens())
                {
                    if (comma.RawKind != (int)SyntaxKind.CommaToken)
                    {
                        continue;
                    }

                    TextSpan whitespaceSpan = TextSpan.FromBounds(comma.Span.End, comma.FullSpan.End);
                    if (ContainsLineBreakOrNonWhitespace(sourceText, whitespaceSpan))
                    {
                        continue;
                    }

                    changes[whitespaceSpan.Start] = new TextChange(
                        whitespaceSpan,
                        newLine + indentation);
                }
            }

            TextChange[] syntaxChanges = changes.Values.OrderBy(change => change.Span.Start).ToArray();
            foreach (SyntaxTrivia item in trivia)
            {
                cancellationToken.ThrowIfCancellationRequested();
                TextSpan commentSpan = item.FullSpan;
                string comment = sourceText.ToString(commentSpan);

                if (!comment.StartsWith("'", StringComparison.Ordinal) ||
                    comment.StartsWith("'''", StringComparison.Ordinal) ||
                    (scope.HasValue && !scope.Value.Contains(commentSpan)) ||
                    ReflowSuppression.OverlapsAny(suppressedSpans, commentSpan) ||
                    OverlapsAny(syntaxChanges, commentSpan))
                {
                    continue;
                }

                TextLine line = sourceText.Lines.GetLineFromPosition(commentSpan.Start);
                if (TextLineReflow.GetVisualLength(line.ToString(), tabSize) <= maxLineLength)
                {
                    continue;
                }

                string textBeforeComment = sourceText.ToString(
                    TextSpan.FromBounds(line.Start, commentSpan.Start));
                string replacement = CommentReflow.WrapSingleLineComment(
                    comment,
                    textBeforeComment,
                    maxLineLength,
                    newLine,
                    tabSize,
                    cancellationToken);

                if (!string.Equals(comment, replacement, StringComparison.Ordinal))
                {
                    changes[commentSpan.Start] = new TextChange(commentSpan, replacement);
                }
            }

            foreach (TextChange change in DocumentationCommentReflow.GetChanges(
                text,
                trivia.Where(item => sourceText.ToString(item.FullSpan).StartsWith("'''", StringComparison.Ordinal))
                    .Select(item => item.FullSpan),
                maxLineLength,
                scope,
                newLine,
                tabSize,
                suppressedSpans,
                cancellationToken))
            {
                if (!OverlapsAny(syntaxChanges, change.Span))
                {
                    changes[change.Span.Start] = change;
                }
            }

            return changes.Values.OrderByDescending(change => change.Span.Start).ToArray();
        }

        private static bool IsSupportedList(SyntaxNode node)
        {
            return node is ArgumentListSyntax ||
                node is ParameterListSyntax ||
                node is TypeArgumentListSyntax ||
                node is TypeParameterListSyntax ||
                node is CollectionInitializerSyntax ||
                node is ObjectCollectionInitializerSyntax ||
                node is ObjectMemberInitializerSyntax ||
                node is TupleExpressionSyntax;
        }

        private static bool HasOverlengthLine(
            SyntaxNode node,
            SourceText sourceText,
            int maxLineLength,
            int tabSize)
        {
            int firstLine = sourceText.Lines.GetLineFromPosition(node.SpanStart).LineNumber;
            int lastPosition = node.Span.End > node.SpanStart ? node.Span.End - 1 : node.SpanStart;
            int lastLine = sourceText.Lines.GetLineFromPosition(lastPosition).LineNumber;

            for (int lineNumber = firstLine; lineNumber <= lastLine; lineNumber++)
            {
                if (TextLineReflow.GetVisualLength(
                    sourceText.Lines[lineNumber].ToString(),
                    tabSize) > maxLineLength)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsLineBreakOrNonWhitespace(SourceText sourceText, TextSpan span)
        {
            for (int position = span.Start; position < span.End; position++)
            {
                char character = sourceText[position];
                if (character == '\r' || character == '\n' || !char.IsWhiteSpace(character))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool OverlapsAny(TextChange[] changes, TextSpan span)
        {
            return changes.Any(change => change.Span.OverlapsWith(span));
        }

        private static string GetLeadingWhitespace(SourceText sourceText, int position)
        {
            TextLine line = sourceText.Lines.GetLineFromPosition(position);
            string lineText = line.ToString();
            int length = 0;

            while (length < lineText.Length && char.IsWhiteSpace(lineText[length]))
            {
                length++;
            }

            return lineText.Substring(0, length);
        }
    }
}
