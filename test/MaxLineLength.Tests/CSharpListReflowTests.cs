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
