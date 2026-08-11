using System.Linq;
using Microsoft.CodeAnalysis.Text;
using Xunit;

namespace MaxLineLength.Tests
{
    public class TextLineReflowTests
    {
        [Fact]
        public void WrapsAtLastSpaceBeforeLimit()
        {
            const string source = "one two three four five";

            string actual = Reflow(source, 14);

            Assert.Equal("one two three\r\nfour five", actual);
        }

        [Fact]
        public void PreservesIndentation()
        {
            const string source = "    one two three four five";

            string actual = Reflow(source, 16);

            Assert.Equal("    one two\r\n    three four\r\n    five", actual);
        }

        [Fact]
        public void LeavesLongWordUnchanged()
        {
            const string source = "averylongwordwithoutspaces";

            Assert.Equal(source, Reflow(source, 10));
        }

        [Fact]
        public void RequiresWholeLineInsideSelection()
        {
            const string source = "one two three four five";
            var scope = new TextSpan(4, 9);

            Assert.Equal(source, Reflow(source, 10, scope));
        }

        [Fact]
        public void WrapsVeryLongLineWithoutDroppingContent()
        {
            string source = string.Join(" ", Enumerable.Repeat("word", 20000));

            string actual = Reflow(source, 40);

            Assert.Equal(source, actual.Replace("\r\n", " "));
            Assert.All(
                actual.Split(new[] { "\r\n" }, System.StringSplitOptions.None),
                line => Assert.True(TextLineReflow.GetVisualLength(line, 4) <= 40));
        }

        private static string Reflow(string source, int maxLineLength, TextSpan? scope = null)
        {
            var changes = TextLineReflow.GetChanges(source, maxLineLength, scope, "\r\n", tabSize: 4);
            return SourceText.From(source)
                .WithChanges(changes.OrderBy(change => change.Span.Start))
                .ToString();
        }
    }
}
