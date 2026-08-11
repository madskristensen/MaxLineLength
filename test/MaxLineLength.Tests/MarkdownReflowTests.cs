using System.Linq;
using Microsoft.CodeAnalysis.Text;
using Xunit;

namespace MaxLineLength.Tests
{
    public class MarkdownReflowTests
    {
        [Fact]
        public void ReflowsParagraphAcrossExistingSoftBreaks()
        {
            const string source =
                "This paragraph already has a soft\r\nbreak and should be reflowed consistently.";

            string actual = Reflow(source, 35);

            Assert.Equal(
                "This paragraph already has a soft\r\n" +
                "break and should be reflowed\r\n" +
                "consistently.",
                actual);
        }

        [Fact]
        public void LeavesFencedCodeUnchanged()
        {
            const string source =
                "```javascript\r\n" +
                "const value = \"this deliberately long line remains unchanged inside a fence\";\r\n" +
                "```";

            Assert.Equal(source, Reflow(source, 30));
        }

        [Fact]
        public void LeavesListsAndTablesUnchanged()
        {
            const string source =
                "- This deliberately long list item remains unchanged because list structure is excluded\r\n" +
                "| Name | This deliberately long table cell remains unchanged |";

            Assert.Equal(source, Reflow(source, 30));
        }

        [Fact]
        public void LeavesFrontMatterUnchanged()
        {
            const string source =
                "---\r\n" +
                "description: This deliberately long metadata value remains unchanged\r\n" +
                "---";

            Assert.Equal(source, Reflow(source, 30));
        }

        [Fact]
        public void RequiresWholeParagraphInsideSelection()
        {
            const string source =
                "This long paragraph should remain unchanged when only a small part is selected.";
            var selection = new TextSpan(5, 20);

            Assert.Equal(source, Reflow(source, 30, selection));
        }

        private static string Reflow(
            string source,
            int maxLineLength,
            TextSpan? scope = null)
        {
            var changes = MarkdownReflow.GetChanges(
                source,
                maxLineLength,
                scope,
                "\r\n",
                tabSize: 4);

            return SourceText.From(source)
                .WithChanges(changes.OrderBy(change => change.Span.Start))
                .ToString();
        }
    }
}
