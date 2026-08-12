using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.VisualBasic;
using Xunit;

namespace MaxLineLength.Tests
{
    public class DocumentationCommentReflowTests
    {
        [Fact]
        public void ReflowsParagraphsAndPreservesStructuredElements()
        {
            const string source =
                "class C\r\n" +
                "{\r\n" +
                "    /// <summary>\r\n" +
                "    /// This documentation paragraph has enough words to exceed the configured maximum line length and wrap safely.\r\n" +
                "    /// </summary>\r\n" +
                "    /// <list type=\"bullet\">\r\n" +
                "    /// <item>This deliberately long list item remains unchanged inside structured documentation.</item>\r\n" +
                "    /// </list>\r\n" +
                "    void M() { }\r\n" +
                "}";

            string actual = Reflow(source, 55);

            Assert.DoesNotContain(
                "This documentation paragraph has enough words to exceed the configured maximum line length",
                actual);
            Assert.Contains("/// <summary>", actual);
            Assert.Contains("/// </summary>", actual);
            Assert.Contains("maximum line", actual);
            Assert.Contains("wrap safely.", actual);
            Assert.Contains(
                "    /// <item>This deliberately long list item remains unchanged inside structured documentation.</item>",
                actual);
            Assert.All(
                SourceText.From(actual).Lines
                    .Select(line => line.ToString())
                    .Where(line => line.Contains("///") && !line.Contains("<item>")),
                line => Assert.True(line.Length <= 55, line));
            AssertValidSyntax(actual);
            Assert.Equal(actual, Reflow(actual, 55));
        }

        [Fact]
        public void KeepsInlineXmlTagsIntactWhileWrappingProse()
        {
            const string source =
                "class C\r\n" +
                "{\r\n" +
                "    /// <summary>This documentation references <see cref=\"System.String\"/> while explaining a deliberately long behavior.</summary>\r\n" +
                "    void M() { }\r\n" +
                "}";

            string actual = Reflow(source, 60);

            Assert.Contains("<see cref=\"System.String\"/>", actual);
            Assert.DoesNotContain("<see\r\n", actual);
            AssertValidSyntax(actual);
        }

        [Fact]
        public void WrapsProseContainingBareLessThanSign()
        {
            const string source =
                "class C\r\n" +
                "{\r\n" +
                "    /// The value must be < the maximum allowed and greater than zero for this deliberately long explanation.\r\n" +
                "    void M() { }\r\n" +
                "}";

            string actual = Reflow(source, 55);

            Assert.DoesNotContain(
                "< the maximum allowed and greater than zero for this deliberately long explanation.",
                actual);
            Assert.Contains("< the maximum", actual);
            Assert.Contains("deliberately long", actual);
            Assert.Contains("explanation.", actual);
        }

        [Fact]
        public void SelfClosingProtectedTagDoesNotSuppressFollowingProse()
        {
            const string source =
                "class C\r\n" +
                "{\r\n" +
                "    /// <code/>\r\n" +
                "    /// This following documentation prose is deliberately long and should still be wrapped normally.\r\n" +
                "    void M() { }\r\n" +
                "}";

            string actual = Reflow(source, 55);

            Assert.Contains("/// <code/>", actual);
            Assert.DoesNotContain(
                "This following documentation prose is deliberately long and should still be wrapped normally.",
                actual);
        }

        [Fact]
        public void ReflowsVisualBasicDocumentationAndPreservesLists()
        {
            const string source =
                "Class C\r\n" +
                "    ''' <summary>\r\n" +
                "    ''' This Visual Basic documentation paragraph has enough words to exceed the configured maximum line length.\r\n" +
                "    ''' </summary>\r\n" +
                "    ''' <list type=\"bullet\">\r\n" +
                "    ''' <item>This deliberately long list item remains unchanged.</item>\r\n" +
                "    ''' </list>\r\n" +
                "    Sub M()\r\n" +
                "    End Sub\r\n" +
                "End Class";

            string actual = ReflowVisualBasic(source, 55);

            Assert.DoesNotContain(
                "This Visual Basic documentation paragraph has enough words to exceed",
                actual);
            Assert.Contains(
                "    ''' <item>This deliberately long list item remains unchanged.</item>",
                actual);
            AssertValidVisualBasicSyntax(actual);
            Assert.Equal(actual, ReflowVisualBasic(actual, 55));
        }

        [Fact]
        public void PreservesCodeAndExampleRegions()
        {
            const string source =
                "class C\r\n" +
                "{\r\n" +
                "    /// <example>\r\n" +
                "    /// <code>Call(firstArgument, secondArgument, thirdArgument);</code>\r\n" +
                "    /// This deliberately long example explanation remains unchanged with its original layout.\r\n" +
                "    /// </example>\r\n" +
                "    void M() { }\r\n" +
                "}";

            Assert.Equal(source, Reflow(source, 45));
        }

        private static string Reflow(string source, int maxLineLength)
        {
            var changes = CSharpListReflow.GetChanges(
                source,
                maxLineLength,
                scope: null,
                "\r\n",
                "    ",
                tabSize: 4);

            return SourceText.From(source)
                .WithChanges(changes.OrderBy(change => change.Span.Start))
                .ToString();
        }

        private static string ReflowVisualBasic(string source, int maxLineLength)
        {
            var changes = VisualBasicListReflow.GetChanges(
                source,
                maxLineLength,
                scope: null,
                "\r\n",
                "    ",
                tabSize: 4);

            return SourceText.From(source)
                .WithChanges(changes.OrderBy(change => change.Span.Start))
                .ToString();
        }

        private static void AssertValidSyntax(string source)
        {
            var errors = CSharpSyntaxTree.ParseText(source)
                .GetDiagnostics()
                .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);

            Assert.Empty(errors);
        }

        private static void AssertValidVisualBasicSyntax(string source)
        {
            var errors = VisualBasicSyntaxTree.ParseText(source)
                .GetDiagnostics()
                .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);

            Assert.Empty(errors);
        }
    }
}
