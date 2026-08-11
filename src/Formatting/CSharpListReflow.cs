using System.Collections.Generic;
using System.Linq;
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
            int tabSize)
        {
            SyntaxTree tree = CSharpSyntaxTree.ParseText(text);
            SyntaxNode root = tree.GetRoot();
            SourceText sourceText = tree.GetText();
            var changes = new Dictionary<int, TextChange>();

            foreach (SyntaxNode node in root.DescendantNodes().Where(IsSupportedList))
            {
                if (node.ContainsDiagnostics ||
                    (scope.HasValue && !scope.Value.Contains(node.Span)) ||
                    !HasOverlengthLine(node, sourceText, maxLineLength, tabSize))
                {
                    continue;
                }

                string indentation = GetLeadingWhitespace(sourceText, node.SpanStart) + indentUnit;

                foreach (SyntaxToken comma in node.ChildTokens().Where(token => token.IsKind(SyntaxKind.CommaToken)))
                {
                    SyntaxToken nextToken = comma.GetNextToken(includeZeroWidth: true);
                    if (nextToken.RawKind == 0 ||
                        sourceText.Lines.GetLineFromPosition(comma.Span.End).LineNumber !=
                        sourceText.Lines.GetLineFromPosition(nextToken.SpanStart).LineNumber)
                    {
                        continue;
                    }

                    TextSpan whitespaceSpan = TextSpan.FromBounds(comma.Span.End, nextToken.SpanStart);
                    string whitespace = sourceText.ToString(whitespaceSpan);
                    if (whitespace.Any(character => !char.IsWhiteSpace(character)))
                    {
                        continue;
                    }

                    changes[whitespaceSpan.Start] = new TextChange(whitespaceSpan, newLine + indentation);
                }
            }

            foreach (SyntaxTrivia trivia in root.DescendantTrivia(descendIntoTrivia: true).Where(IsSingleLineComment))
            {
                if ((scope.HasValue && !scope.Value.Contains(trivia.Span)) ||
                    changes.Values.Any(change => change.Span.OverlapsWith(trivia.Span)))
                {
                    continue;
                }

                TextLine line = sourceText.Lines.GetLineFromPosition(trivia.SpanStart);
                if (TextLineReflow.GetVisualLength(line.ToString(), tabSize) <= maxLineLength)
                {
                    continue;
                }

                string replacement = WrapSingleLineComment(
                    trivia.ToFullString(),
                    sourceText.ToString(TextSpan.FromBounds(line.Start, trivia.SpanStart)),
                    maxLineLength,
                    newLine,
                    tabSize);

                if (!string.Equals(replacement, trivia.ToFullString(), StringComparison.Ordinal))
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
                if (TextLineReflow.GetVisualLength(sourceText.Lines[lineNumber].ToString(), tabSize) > maxLineLength)
                {
                    return true;
                }
            }

            return false;
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
