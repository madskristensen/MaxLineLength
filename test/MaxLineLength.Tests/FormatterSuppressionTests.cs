using System.Linq;
using Microsoft.CodeAnalysis.Text;
using Xunit;

namespace MaxLineLength.Tests
{
    public class FormatterSuppressionTests
    {
        [Fact]
        public void SuppressesCSharpReflowBetweenFormatterMarkers()
        {
            const string source =
                "class C\r\n" +
                "{\r\n" +
                "    // @formatter:off\r\n" +
                "    void Suppressed() { Call(firstArgument, secondArgument, thirdArgument); }\r\n" +
                "    // @formatter:on\r\n" +
                "    void Reflowed() { Call(firstArgument, secondArgument, thirdArgument); }\r\n" +
                "}";

            string actual = ReflowCSharp(source, 55);

            Assert.Contains(
                "void Suppressed() { Call(firstArgument, secondArgument, thirdArgument); }",
                actual);
            Assert.Contains("void Reflowed() { Call(firstArgument,\r\n", actual);
        }

        [Fact]
        public void DoesNotTreatCSharpStringAsFormatterMarker()
        {
            const string source =
                "class C { string Marker = \"// @formatter:off\"; void M() { Call(firstArgument, secondArgument, thirdArgument); } }";

            Assert.Contains("Call(firstArgument,\r\n", ReflowCSharp(source, 55));
        }

        [Fact]
        public void SuppressesVisualBasicReflowBetweenFormatterMarkers()
        {
            const string source =
                "Module M\r\n" +
                "    ' @formatter:off\r\n" +
                "    Sub Suppressed()\r\n" +
                "        CallMethod(firstArgument, secondArgument, thirdArgument)\r\n" +
                "    End Sub\r\n" +
                "    ' @formatter:on\r\n" +
                "    Sub Reflowed()\r\n" +
                "        CallMethod(firstArgument, secondArgument, thirdArgument)\r\n" +
                "    End Sub\r\n" +
                "End Module";

            string actual = ReflowVisualBasic(source, 45);

            Assert.Contains(
                "CallMethod(firstArgument, secondArgument, thirdArgument)\r\n    End Sub\r\n    ' @formatter:on",
                actual);
            Assert.Contains("CallMethod(firstArgument,\r\n            secondArgument", actual);
        }

        [Fact]
        public void SuppressesClassifiedCommentReflow()
        {
            const string source =
                "// @formatter:off\r\n" +
                "// This deliberately long comment remains unchanged while formatting is disabled\r\n" +
                "// @formatter:on\r\n" +
                "// This deliberately long comment is wrapped after formatting is enabled";
            TextSpan[] spans = GetLineSpans(source);

            string actual = Apply(source, CommentReflow.GetChanges(
                source,
                spans,
                45,
                scope: null,
                "\r\n",
                tabSize: 4));

            Assert.Contains("comment remains unchanged while formatting is disabled", actual);
            Assert.DoesNotContain(
                "// This deliberately long comment is wrapped after formatting is enabled",
                actual);
            Assert.Contains("formatting is enabled", actual);
        }

        [Fact]
        public void SuppressesMarkdownParagraphReflow()
        {
            const string source =
                "<!-- @formatter:off -->\r\n" +
                "This deliberately long Markdown paragraph remains unchanged while formatting is disabled.\r\n" +
                "<!-- @formatter:on -->\r\n\r\n" +
                "This deliberately long Markdown paragraph is wrapped after formatting is enabled.";

            string actual = Apply(source, MarkdownReflow.GetChanges(
                source,
                45,
                scope: null,
                "\r\n",
                tabSize: 4));

            Assert.Contains("paragraph remains unchanged while formatting is disabled.", actual);
            Assert.Contains("Markdown paragraph is\r\nwrapped after", actual);
        }

        private static string ReflowCSharp(string source, int maxLineLength)
        {
            return Apply(source, CSharpListReflow.GetChanges(
                source,
                maxLineLength,
                scope: null,
                "\r\n",
                "    ",
                tabSize: 4));
        }

        private static string ReflowVisualBasic(string source, int maxLineLength)
        {
            return Apply(source, VisualBasicListReflow.GetChanges(
                source,
                maxLineLength,
                scope: null,
                "\r\n",
                "    ",
                tabSize: 4));
        }

        private static TextSpan[] GetLineSpans(string source)
        {
            return SourceText.From(source).Lines
                .Select(line => line.Span)
                .Where(span => !span.IsEmpty)
                .ToArray();
        }

        private static string Apply(string source, System.Collections.Generic.IEnumerable<TextChange> changes)
        {
            return SourceText.From(source)
                .WithChanges(changes.OrderBy(change => change.Span.Start))
                .ToString();
        }
    }
}
