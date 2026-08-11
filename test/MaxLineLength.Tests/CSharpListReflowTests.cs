using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Xunit;

namespace MaxLineLength.Tests
{
    public class CSharpListReflowTests
    {
        [Fact]
        public void ReflowsLongArgumentList()
        {
            const string source = "class C { void M() { Call(firstArgument, secondArgument, thirdArgument); } }";

            string actual = Reflow(source, 50);

            Assert.Contains("Call(firstArgument,\r\n    secondArgument,\r\n    thirdArgument)", actual);
        }

        [Fact]
        public void LeavesShortListUnchanged()
        {
            const string source = "class C { void M() { Call(first, second); } }";

            Assert.Equal(source, Reflow(source, 120));
        }

        [Fact]
        public void DoesNotReflowCommasInsideStrings()
        {
            const string source = "class C { string Value = \"first, second, third, fourth, fifth\"; }";

            Assert.Equal(source, Reflow(source, 20));
        }

        [Fact]
        public void RequiresListToBeInsideSelection()
        {
            const string source = "class C { void M() { Call(firstArgument, secondArgument, thirdArgument); } }";
            var partialSelection = new TextSpan(source.IndexOf("secondArgument"), "secondArgument".Length);

            Assert.Equal(source, Reflow(source, 40, partialSelection));
        }

        [Fact]
        public void PreservesCommentsBetweenArguments()
        {
            const string source = "class C { void M() { Call(firstArgument, /* important */ secondArgument, thirdArgument); } }";

            string actual = Reflow(source, 40);

            Assert.Contains("firstArgument, /* important */ secondArgument", actual);
            Assert.Contains("secondArgument,\r\n    thirdArgument", actual);
        }

        [Fact]
        public void ReflowsLongParameterList()
        {
            const string source = "class C { void Method(string firstParameter, string secondParameter, string thirdParameter) { } }";

            string actual = Reflow(source, 55);

            Assert.Contains("string firstParameter,\r\n    string secondParameter,\r\n    string thirdParameter", actual);
        }

        [Fact]
        public void ReflowsSingleLineCommentAndPreservesPrefix()
        {
            const string source = "    // This is a long comment that should wrap before the configured maximum line length";

            string actual = Reflow(source, 45);

            Assert.Equal(
                "    // This is a long comment that should\r\n    // wrap before the configured maximum\r\n    // line length",
                actual);
        }

        [Fact]
        public void PreservesDocumentationCommentMarker()
        {
            const string source =
                "    /// This C sharp documentation comment should wrap before the configured maximum line length";

            string actual = Reflow(source, 45);

            Assert.StartsWith(
                "    /// This C sharp documentation comment\r\n    /// should wrap",
                actual);
        }

        [Fact]
        public void LeavesStringLiteralUnchanged()
        {
            const string source = "class C { string Value = \"this ordinary string literal contains spaces but cannot accept raw newlines\"; }";

            Assert.Equal(source, Reflow(source, 30));
        }

        [Fact]
        public void ReflowsNestedArgumentLists()
        {
            const string source = "class C { void M() { Outer(Inner(firstArgument, secondArgument, thirdArgument), fourthArgument, fifthArgument); } }";

            string actual = Reflow(source, 55);

            Assert.Contains("Inner(firstArgument,\r\n    secondArgument,\r\n    thirdArgument)", actual);
            Assert.Contains("fourthArgument,\r\n    fifthArgument", actual);
        }

        [Fact]
        public void ReflowsFluentInvocationChain()
        {
            const string source =
                "class C { void M() { var result = services.AddAuthentication().AddAuthorization().AddPolicyHandler(); } }";

            string actual = Reflow(source, 60);

            Assert.Contains(
                "services\r\n    .AddAuthentication()\r\n    .AddAuthorization()\r\n    .AddPolicyHandler()",
                actual);
        }

        [Fact]
        public void IndentsFluentChainInsideReflowedArgumentList()
        {
            const string source =
                "class C { void M() { Call(firstArgument, services.AddAuthentication().AddAuthorization().AddPolicyHandler(), thirdArgument); } }";

            string actual = Reflow(source, 65);

            Assert.Contains(
                "firstArgument,\r\n    services\r\n        .AddAuthentication()\r\n        .AddAuthorization()\r\n        .AddPolicyHandler()",
                actual);
        }

        [Fact]
        public void PreservesTriviaInsideFluentChain()
        {
            const string source =
                "class C { void M() { var result = services.AddAuthentication() /* keep */ .AddAuthorization().AddPolicyHandler(); } }";

            string actual = Reflow(source, 65);

            Assert.Contains("AddAuthentication() /* keep */ .AddAuthorization()", actual);
            AssertValidSyntax(actual);
        }

        [Fact]
        public void RequiresFluentChainToBeInsideSelection()
        {
            const string source =
                "class C { void M() { var result = services.AddAuthentication().AddAuthorization().AddPolicyHandler(); } }";
            var partialSelection = new TextSpan(
                source.IndexOf("AddAuthorization", StringComparison.Ordinal),
                "AddAuthorization".Length);

            Assert.Equal(source, Reflow(source, 60, partialSelection));
        }

        [Fact]
        public void ReflowsLogicalExpression()
        {
            const string source =
                "class C { bool M() { return firstCondition && secondCondition && thirdCondition; } }";

            string actual = Reflow(source, 55);

            Assert.Contains(
                "firstCondition\r\n    && secondCondition\r\n    && thirdCondition",
                actual);
        }

        [Fact]
        public void ReflowsConditionalExpression()
        {
            const string source =
                "class C { string M() { return condition ? GetSuccessfulValue() : GetFallbackValue(); } }";

            string actual = Reflow(source, 55);

            Assert.Contains(
                "condition\r\n    ? GetSuccessfulValue()\r\n    : GetFallbackValue()",
                actual);
        }

        [Fact]
        public void ReflowsQueryExpression()
        {
            const string source =
                "class C { object M() { var result = from customer in customers where customer.IsActive orderby customer.Name select customer; return result; } }";

            string actual = Reflow(source, 70);

            Assert.Contains(
                "var result =\r\n    from customer in customers\r\n    where customer.IsActive\r\n    orderby customer.Name\r\n    select customer",
                actual);
        }

        [Fact]
        public void ReflowsQueryContinuation()
        {
            const string source =
                "class C { object M() { var result = from customer in customers group customer by customer.Region into region select region; return result; } }";

            string actual = Reflow(source, 70);

            Assert.Contains(
                "from customer in customers\r\n    group customer by customer.Region\r\n    into region\r\n    select region",
                actual);
        }

        [Theory]
        [InlineData("class C { object M() { return services.AddAuthentication().AddAuthorization().AddPolicyHandler(); } }", 60)]
        [InlineData("class C { bool M() { return firstCondition && secondCondition && thirdCondition; } }", 55)]
        [InlineData("class C { string M() { return condition ? GetSuccessfulValue() : GetFallbackValue(); } }", 55)]
        [InlineData("class C { object M() { return from customer in customers where customer.IsActive select customer; } }", 60)]
        public void ProducesValidIdempotentExpressionReflow(string source, int maxLineLength)
        {
            string actual = Reflow(source, maxLineLength);

            Assert.NotEqual(source, actual);
            AssertValidSyntax(actual);
            Assert.Equal(actual, Reflow(actual, maxLineLength));
        }

        [Fact]
        public void LeavesSingleMemberAccessUnchanged()
        {
            const string source =
                "class C { string M() { return customer.ExceptionallyLongPropertyNameThatExceedsTheLimit; } }";

            Assert.Equal(source, Reflow(source, 50));
        }

        [Fact]
        public void HonorsCancellationBeforeParsing()
        {
            const string source = "class C { void M() { Call(firstArgument, secondArgument, thirdArgument); } }";
            var cancellationToken = new System.Threading.CancellationToken(canceled: true);

            Assert.Throws<System.OperationCanceledException>(() =>
                CSharpListReflow.GetChanges(
                    source,
                    maxLineLength: 40,
                    scope: null,
                    "\r\n",
                    "    ",
                    tabSize: 4,
                    cancellationToken));
        }

        [Fact]
        [Trait("Category", "Profiling")]
        public void ProcessesRepresentativeFormattingWorkload()
        {
            const int itemCount = 500;
            const string plainLine =
                "This representative plain text line contains enough words to exceed the configured maximum line length.";
            const string markdownParagraph =
                "This representative Markdown paragraph contains enough words to exceed the configured maximum line length.\r\n" +
                "It also includes an existing soft break that must be combined before wrapping.";
            const string commentLine =
                "// This representative code comment contains enough words to exceed the configured maximum line length.";

            string plainText = string.Join("\r\n", Enumerable.Repeat(plainLine, itemCount));
            string markdown = string.Join("\r\n\r\n", Enumerable.Repeat(markdownParagraph, itemCount));
            string csharp = "class ProfileTarget\r\n{\r\n" + string.Join(
                "\r\n",
                Enumerable.Range(0, itemCount).Select(index =>
                    $"    void Method{index}() {{ Call(firstArgument, secondArgument, thirdArgument, fourthArgument); }}")) +
                "\r\n}";
            string comments = string.Join("\r\n", Enumerable.Repeat(commentLine, itemCount));
            TextSpan[] commentSpans = Enumerable.Range(0, itemCount)
                .Select(index => new TextSpan(
                    index * (commentLine.Length + 2),
                    commentLine.Length))
                .ToArray();

            Assert.NotEmpty(TextLineReflow.GetChanges(plainText, 80, null, "\r\n", 4));
            Assert.NotEmpty(MarkdownReflow.GetChanges(markdown, 80, null, "\r\n", 4));
            Assert.NotEmpty(CommentReflow.GetChanges(
                comments,
                commentSpans,
                80,
                null,
                "\r\n",
                4));
            Assert.NotEmpty(CSharpListReflow.GetChanges(
                csharp,
                80,
                null,
                "\r\n",
                "    ",
                4));
        }

        private static void AssertValidSyntax(string source)
        {
            var errors = CSharpSyntaxTree.ParseText(source)
                .GetDiagnostics()
                .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);

            Assert.Empty(errors);
        }

        private static string Reflow(string source, int maxLineLength, TextSpan? scope = null)
        {
            var changes = CSharpListReflow.GetChanges(
                source,
                maxLineLength,
                scope,
                "\r\n",
                "    ",
                tabSize: 4);

            return SourceText.From(source)
                .WithChanges(changes.OrderBy(change => change.Span.Start))
                .ToString();
        }
    }
}
