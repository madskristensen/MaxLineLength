using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace MaxLineLength
{
    internal static class CSharpListReflow
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
            int[] overlengthLineCounts = GetOverlengthLineCounts(
                sourceText,
                maxLineLength,
                tabSize,
                cancellationToken);
            if (overlengthLineCounts[overlengthLineCounts.Length - 1] == 0)
            {
                return Array.Empty<TextChange>();
            }

            SyntaxTree tree = CSharpSyntaxTree.ParseText(
                sourceText,
                cancellationToken: cancellationToken);
            SyntaxNode root = tree.GetRoot(cancellationToken);
            var changes = new Dictionary<int, TextChange>();

            foreach (SyntaxNode node in root.DescendantNodes())
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!IsSupportedList(node) ||
                    node.ContainsDiagnostics ||
                    (scope.HasValue && !scope.Value.Contains(node.Span)) ||
                    !HasOverlengthLine(node, sourceText, overlengthLineCounts))
                {
                    continue;
                }

                string indentation = GetLeadingWhitespace(sourceText, node.SpanStart) + indentUnit;

                foreach (SyntaxToken comma in node.ChildTokens())
                {
                    if (!comma.IsKind(SyntaxKind.CommaToken))
                    {
                        continue;
                    }

                    SyntaxToken nextToken = comma.GetNextToken(includeZeroWidth: true);
                    if (nextToken.RawKind == 0 ||
                        sourceText.Lines.GetLineFromPosition(comma.Span.End).LineNumber !=
                        sourceText.Lines.GetLineFromPosition(nextToken.SpanStart).LineNumber)
                    {
                        continue;
                    }

                    TextSpan whitespaceSpan = TextSpan.FromBounds(comma.Span.End, nextToken.SpanStart);
                    string whitespace = sourceText.ToString(whitespaceSpan);
                    if (ContainsNonWhitespace(whitespace))
                    {
                        continue;
                    }

                    changes[whitespaceSpan.Start] = new TextChange(whitespaceSpan, newLine + indentation);
                }
            }

            TextChange[] syntaxChanges = changes.Values
                .OrderBy(change => change.Span.Start)
                .ToArray();

            foreach (SyntaxTrivia trivia in root.DescendantTrivia(descendIntoTrivia: true))
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!IsSingleLineComment(trivia) ||
                    (scope.HasValue && !scope.Value.Contains(trivia.Span)) ||
                    OverlapsAny(syntaxChanges, trivia.Span))
                {
                    continue;
                }

                TextLine line = sourceText.Lines.GetLineFromPosition(trivia.SpanStart);
                if (overlengthLineCounts[line.LineNumber + 1] ==
                    overlengthLineCounts[line.LineNumber])
                {
                    continue;
                }

                string original = trivia.ToFullString();
                string replacement = WrapSingleLineComment(
                    original,
                    sourceText.ToString(TextSpan.FromBounds(line.Start, trivia.SpanStart)),
                    maxLineLength,
                    newLine,
                    tabSize);

                if (!string.Equals(replacement, original, StringComparison.Ordinal))
                {
                    changes[trivia.SpanStart] = new TextChange(trivia.Span, replacement);
                }
            }

            return changes.Values.OrderByDescending(change => change.Span.Start).ToArray();
        }

        private static bool IsSingleLineComment(SyntaxTrivia trivia)
        {
            return trivia.IsKind(SyntaxKind.SingleLineCommentTrivia) ||
                trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia);
        }

        private static string WrapSingleLineComment(
            string comment,
            string textBeforeComment,
            int maxLineLength,
            string newLine,
            int tabSize)
        {
            return CommentReflow.WrapSingleLineComment(
                comment,
                textBeforeComment,
                maxLineLength,
                newLine,
                tabSize);
        }

        private static bool IsSupportedList(SyntaxNode node)
        {
            return node is ArgumentListSyntax ||
                node is BracketedArgumentListSyntax ||
                node is ParameterListSyntax ||
                node is BracketedParameterListSyntax ||
                node is TypeArgumentListSyntax ||
                node is TypeParameterListSyntax ||
                node is InitializerExpressionSyntax ||
                node is TupleExpressionSyntax ||
                node is TupleTypeSyntax ||
                node is BaseListSyntax ||
                node is AttributeArgumentListSyntax ||
                node is AttributeListSyntax ||
                node is EnumDeclarationSyntax ||
                node is VariableDeclarationSyntax ||
                node is AnonymousObjectCreationExpressionSyntax ||
                node is SwitchExpressionSyntax ||
                node is PositionalPatternClauseSyntax ||
                node is PropertyPatternClauseSyntax;
        }

        private static int[] GetOverlengthLineCounts(
            SourceText sourceText,
            int maxLineLength,
            int tabSize,
            CancellationToken cancellationToken)
        {
            var counts = new int[sourceText.Lines.Count + 1];

            for (int lineNumber = 0; lineNumber < sourceText.Lines.Count; lineNumber++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                counts[lineNumber + 1] = counts[lineNumber] +
                    (TextLineReflow.GetVisualLength(
                        sourceText.Lines[lineNumber].ToString(),
                        tabSize) > maxLineLength
                        ? 1
                        : 0);
            }

            return counts;
        }

        private static bool HasOverlengthLine(
            SyntaxNode node,
            SourceText sourceText,
            int[] overlengthLineCounts)
        {
            int firstLine = sourceText.Lines.GetLineFromPosition(node.SpanStart).LineNumber;
            int lastPosition = node.Span.End > node.SpanStart ? node.Span.End - 1 : node.SpanStart;
            int lastLine = sourceText.Lines.GetLineFromPosition(lastPosition).LineNumber;

            return overlengthLineCounts[lastLine + 1] > overlengthLineCounts[firstLine];
        }

        private static bool ContainsNonWhitespace(string text)
        {
            foreach (char character in text)
            {
                if (!char.IsWhiteSpace(character))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool OverlapsAny(TextChange[] changes, TextSpan span)
        {
            int low = 0;
            int high = changes.Length;

            while (low < high)
            {
                int middle = low + ((high - low) / 2);
                if (changes[middle].Span.Start < span.Start)
                {
                    low = middle + 1;
                }
                else
                {
                    high = middle;
                }
            }

            return (low < changes.Length && changes[low].Span.OverlapsWith(span)) ||
                (low > 0 && changes[low - 1].Span.OverlapsWith(span));
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
