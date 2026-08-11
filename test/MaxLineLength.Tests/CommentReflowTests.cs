using System.Linq;
using Microsoft.CodeAnalysis.Text;
using Xunit;

namespace MaxLineLength.Tests
{
    public class CommentReflowTests
    {
        [Fact]
        public void WrapsClassifiedLineComment()
        {
            const string source =
                "    // This JavaScript comment should wrap at the configured maximum line length";
            var commentSpan = new TextSpan(source.IndexOf("//"), source.Length - source.IndexOf("//"));

            string actual = Reflow(source, new[] { commentSpan }, 42);

            Assert.Equal(
                "    // This JavaScript comment should wrap\r\n" +
                "    // at the configured maximum line\r\n" +
                "    // length",
                actual);
        }

        [Fact]
        public void DoesNotTreatUnclassifiedTemplateContentAsComment()
        {
            const string source =
                "const value = `// This text looks like a comment but is template literal content`;";

            Assert.Equal(source, Reflow(source, new TextSpan[0], 30));
        }

        [Fact]
        public void WrapsSingleLineBlockComment()
        {
            const string source =
                "    /* This C++ block comment should wrap at the configured maximum line length */";
            var commentSpan = new TextSpan(source.IndexOf("/*"), source.Length - source.IndexOf("/*"));

            string actual = Reflow(source, new[] { commentSpan }, 42);

            Assert.Equal(
                "    /* This C++ block comment should wrap\r\n" +
                "     * at the configured maximum line\r\n" +
                "     * length */",
                actual);
        }

        [Fact]
        public void WrapsLongLineWithinMultilineBlockComment()
        {
            const string source =
                "/**\r\n" +
                " * This documentation line should wrap at the configured maximum line length\r\n" +
                " */";
            var commentSpan = new TextSpan(0, source.Length);

            string actual = Reflow(source, new[] { commentSpan }, 42);

            Assert.Equal(
                "/**\r\n" +
                " * This documentation line should wrap at\r\n" +
                " * the configured maximum line length\r\n" +
                " */",
                actual);
        }

        [Fact]
        public void LeavesFSharpBlockCommentUnchanged()
        {
            const string source =
                "    (* This F sharp block comment exceeds the configured maximum line length but may contain nested comments *)";
            var commentSpan = new TextSpan(source.IndexOf("(*"), source.Length - source.IndexOf("(*"));

            Assert.Equal(source, Reflow(source, new[] { commentSpan }, 42));
        }

        [Fact]
        public void WrapsSqlLineComment()
        {
            const string source =
                "    -- This SQL comment should wrap at the configured maximum line length";
            var commentSpan = new TextSpan(source.IndexOf("--"), source.Length - source.IndexOf("--"));

            string actual = Reflow(source, new[] { commentSpan }, 40);

            Assert.Equal(
                "    -- This SQL comment should wrap at\r\n" +
                "    -- the configured maximum line\r\n" +
                "    -- length",
                actual);
        }

        [Fact]
        public void WrapsVisualBasicLineComment()
        {
            const string source =
                "    ' This Visual Basic comment should wrap at the configured maximum line length";
            var commentSpan = new TextSpan(source.IndexOf("'"), source.Length - source.IndexOf("'"));

            string actual = Reflow(source, new[] { commentSpan }, 42);

            Assert.Equal(
                "    ' This Visual Basic comment should\r\n" +
                "    ' wrap at the configured maximum line\r\n" +
                "    ' length",
                actual);
        }

        [Fact]
        public void RequiresWholeCommentInsideSelection()
        {
            const string source =
                "// This comment should not wrap when only part of it is selected";
            var commentSpan = new TextSpan(0, source.Length);
            var selection = new TextSpan(3, 20);

            Assert.Equal(source, Reflow(source, new[] { commentSpan }, 30, selection));
        }

        [Fact]
        public void HonorsCancellationBeforeReflow()
        {
            const string source = "// A comment that would otherwise be wrapped";
            var cancellationToken = new System.Threading.CancellationToken(canceled: true);

            Assert.Throws<System.OperationCanceledException>(() =>
                CommentReflow.GetChanges(
                    source,
                    new[] { new TextSpan(0, source.Length) },
                    maxLineLength: 20,
                    scope: null,
                    "\r\n",
                    tabSize: 4,
                    cancellationToken));
        }

        private static string Reflow(
            string source,
            TextSpan[] commentSpans,
            int maxLineLength,
            TextSpan? scope = null)
        {
            var changes = CommentReflow.GetChanges(
                source,
                commentSpans,
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
