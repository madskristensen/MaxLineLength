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

                    TextSpan whitespaceSpan = TextSpan.FromBounds(comma.Span.End, comma.FullSpan.End);
                    if (ContainsLineBreakOrNonWhitespace(sourceText, whitespaceSpan))
                    {
                        continue;
                    }

                    changes[whitespaceSpan.Start] = new TextChange(whitespaceSpan, newLine + indentation);
                }
            }

            AddExpressionChanges(
                root,
                sourceText,
                overlengthLineCounts,
                scope,
                newLine,
                indentUnit,
                changes,
                cancellationToken);

            if (text.IndexOf("//", StringComparison.Ordinal) < 0)
            {
                return changes.Values.OrderByDescending(change => change.Span.Start).ToArray();
            }

            TextChange[] syntaxChanges = changes.Values.OrderBy(change => change.Span.Start).ToArray();

            foreach (SyntaxTrivia trivia in root.DescendantTrivia(descendIntoTrivia: false))
            {
                cancellationToken.ThrowIfCancellationRequested();
                TextSpan commentSpan = trivia.FullSpan;

                if (!IsSingleLineComment(trivia) ||
                    (scope.HasValue && !scope.Value.Contains(commentSpan)) ||
                    OverlapsAny(syntaxChanges, commentSpan))
                {
                    continue;
                }

                TextLine line = sourceText.Lines.GetLineFromPosition(commentSpan.Start);
                if (overlengthLineCounts[line.LineNumber + 1] ==
                    overlengthLineCounts[line.LineNumber])
                {
                    continue;
                }

                string original = sourceText.ToString(commentSpan);
                string replacement = WrapSingleLineComment(
                    original,
                    sourceText.ToString(TextSpan.FromBounds(line.Start, commentSpan.Start)),
                    maxLineLength,
                    newLine,
                    tabSize);

                if (!string.Equals(replacement, original, StringComparison.Ordinal))
                {
                    changes[commentSpan.Start] = new TextChange(commentSpan, replacement);
                }
            }

            return changes.Values.OrderByDescending(change => change.Span.Start).ToArray();
        }

        private static void AddExpressionChanges(
            SyntaxNode root,
            SourceText sourceText,
            int[] overlengthLineCounts,
            TextSpan? scope,
            string newLine,
            string indentUnit,
            IDictionary<int, TextChange> changes,
            CancellationToken cancellationToken)
        {
            foreach (SyntaxNode node in root.DescendantNodes())
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (node.ContainsDiagnostics ||
                    (scope.HasValue && !scope.Value.Contains(node.Span)) ||
                    !HasOverlengthLine(node, sourceText, overlengthLineCounts))
                {
                    continue;
                }

                string indentation = GetExpressionIndentation(
                    node,
                    sourceText,
                    overlengthLineCounts,
                    scope,
                    indentUnit);

                if (node is InvocationExpressionSyntax invocation &&
                    !IsFluentContinuation(invocation.Parent))
                {
                    var operators = new List<SyntaxToken>();
                    CollectFluentOperators(invocation, operators);
                    if (operators.Count > 1)
                    {
                        AddBreaksBeforeTokens(
                            operators,
                            indentation,
                            sourceText,
                            newLine,
                            changes);
                    }
                }
                else if (node is BinaryExpressionSyntax binary &&
                    IsBreakableBinary(binary) &&
                    !(binary.Parent is BinaryExpressionSyntax parentBinary &&
                        IsBreakableBinary(parentBinary)))
                {
                    var operators = new List<SyntaxToken>();
                    CollectBinaryOperators(binary, operators);
                    AddBreaksBeforeTokens(
                        operators,
                        indentation,
                        sourceText,
                        newLine,
                        changes);
                }
                else if (node is ConditionalExpressionSyntax conditional &&
                    !(conditional.Parent is ConditionalExpressionSyntax))
                {
                    AddBreaksBeforeTokens(
                        new[] { conditional.QuestionToken, conditional.ColonToken },
                        indentation,
                        sourceText,
                        newLine,
                        changes);
                }
                else if (node is QueryExpressionSyntax query)
                {
                    AddBreaksBeforeTokens(
                        GetQueryClauseTokens(query),
                        indentation,
                        sourceText,
                        newLine,
                        changes);
                }
            }
        }

        private static bool IsFluentContinuation(SyntaxNode? node)
        {
            return node is MemberAccessExpressionSyntax ||
                (node is InvocationExpressionSyntax invocation &&
                    invocation.Expression is MemberAccessExpressionSyntax);
        }

        private static void CollectFluentOperators(
            ExpressionSyntax expression,
            ICollection<SyntaxToken> operators)
        {
            ExpressionSyntax current = expression;
            while (true)
            {
                if (current is InvocationExpressionSyntax invocation)
                {
                    current = invocation.Expression;
                }
                else if (current is MemberAccessExpressionSyntax memberAccess)
                {
                    operators.Add(memberAccess.OperatorToken);
                    current = memberAccess.Expression;
                }
                else
                {
                    break;
                }
            }

            if (operators is List<SyntaxToken> list)
            {
                list.Reverse();
            }
        }

        private static bool IsBreakableBinary(BinaryExpressionSyntax binary)
        {
            return binary.IsKind(SyntaxKind.LogicalAndExpression) ||
                binary.IsKind(SyntaxKind.LogicalOrExpression) ||
                binary.IsKind(SyntaxKind.CoalesceExpression);
        }

        private static void CollectBinaryOperators(
            ExpressionSyntax expression,
            ICollection<SyntaxToken> operators)
        {
            var pending = new Stack<SyntaxNodeOrToken>();
            pending.Push(expression);

            while (pending.Count > 0)
            {
                SyntaxNodeOrToken item = pending.Pop();
                if (item.IsToken)
                {
                    operators.Add(item.AsToken());
                }
                else if (item.AsNode() is BinaryExpressionSyntax binary &&
                    IsBreakableBinary(binary))
                {
                    pending.Push(binary.Right);
                    pending.Push(binary.OperatorToken);
                    pending.Push(binary.Left);
                }
            }
        }

        private static IEnumerable<SyntaxToken> GetQueryClauseTokens(QueryExpressionSyntax query)
        {
            yield return query.FromClause.FromKeyword;
            QueryBodySyntax body = query.Body;

            while (true)
            {
                foreach (QueryClauseSyntax clause in body.Clauses)
                {
                    yield return clause.GetFirstToken();
                }

                yield return body.SelectOrGroup.GetFirstToken();
                if (body.Continuation == null)
                {
                    yield break;
                }

                yield return body.Continuation.IntoKeyword;
                body = body.Continuation.Body;
            }
        }

        private static string GetExpressionIndentation(
            SyntaxNode node,
            SourceText sourceText,
            int[] overlengthLineCounts,
            TextSpan? scope,
            string indentUnit)
        {
            string indentation = GetLeadingWhitespace(sourceText, node.SpanStart) + indentUnit;

            foreach (SyntaxNode ancestor in node.Ancestors())
            {
                if (IsSupportedList(ancestor) &&
                    (!scope.HasValue || scope.Value.Contains(ancestor.Span)) &&
                    HasOverlengthLine(ancestor, sourceText, overlengthLineCounts) &&
                    HasPrecedingCommaBreak(ancestor, node.SpanStart, sourceText))
                {
                    indentation += indentUnit;
                }
            }

            return indentation;
        }

        private static bool HasPrecedingCommaBreak(
            SyntaxNode list,
            int position,
            SourceText sourceText)
        {
            foreach (SyntaxToken comma in list.ChildTokens())
            {
                if (!comma.IsKind(SyntaxKind.CommaToken) || comma.Span.End >= position)
                {
                    continue;
                }

                TextSpan whitespaceSpan = TextSpan.FromBounds(comma.Span.End, comma.FullSpan.End);
                if (!ContainsLineBreakOrNonWhitespace(sourceText, whitespaceSpan))
                {
                    return true;
                }
            }

            return false;
        }

        private static void AddBreaksBeforeTokens(
            IEnumerable<SyntaxToken> tokens,
            string indentation,
            SourceText sourceText,
            string newLine,
            IDictionary<int, TextChange> changes)
        {
            foreach (SyntaxToken token in tokens)
            {
                SyntaxToken previous = token.GetPreviousToken();
                TextSpan whitespaceSpan = TextSpan.FromBounds(previous.Span.End, token.SpanStart);
                if (ContainsLineBreakOrNonWhitespace(sourceText, whitespaceSpan))
                {
                    continue;
                }

                changes[whitespaceSpan.Start] = new TextChange(
                    whitespaceSpan,
                    newLine + indentation);
            }
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
