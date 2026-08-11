using System.Linq;
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
