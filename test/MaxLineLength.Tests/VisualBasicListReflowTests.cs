using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.VisualBasic;
using Xunit;

namespace MaxLineLength.Tests
{
    public class VisualBasicListReflowTests
    {
        [Fact]
        public void ReflowsLongArgumentList()
        {
            const string source =
                "Module M\r\n    Sub Main()\r\n        CallMethod(firstArgument, secondArgument, thirdArgument)\r\n    End Sub\r\nEnd Module";

            string actual = Reflow(source, 45);

            Assert.Contains(
                "CallMethod(firstArgument,\r\n            secondArgument,\r\n            thirdArgument)",
                actual);
        }

        [Fact]
        public void ReflowsLongParameterList()
        {
            const string source =
                "Class C\r\n    Sub Method(firstParameter As String, secondParameter As String, thirdParameter As String)\r\n    End Sub\r\nEnd Class";

            string actual = Reflow(source, 55);

            Assert.Contains(
                "firstParameter As String,\r\n        secondParameter As String,\r\n        thirdParameter As String",
                actual);
        }

        [Fact]
        public void ReflowsCollectionInitializer()
        {
            const string source =
                "Module M\r\n    Dim Values = {firstValue, secondValue, thirdValue}\r\nEnd Module";

            string actual = Reflow(source, 42);

            Assert.Contains(
                "{firstValue,\r\n        secondValue,\r\n        thirdValue}",
                actual);
        }

        [Fact]
        public void UsesConfiguredTabIndentation()
        {
            const string source =
                "Module M\r\n\tSub Main()\r\n\t\tCallMethod(firstArgument, secondArgument, thirdArgument)\r\n\tEnd Sub\r\nEnd Module";

            string actual = Reflow(source, 42, indentUnit: "\t", tabSize: 4);

            Assert.Contains(
                "CallMethod(firstArgument,\r\n\t\t\tsecondArgument,\r\n\t\t\tthirdArgument)",
                actual);
        }

        [Fact]
        public void RequiresListToBeInsideSelection()
        {
            const string source =
                "Module M\r\n    Sub Main()\r\n        CallMethod(firstArgument, secondArgument, thirdArgument)\r\n    End Sub\r\nEnd Module";
            var partialSelection = new TextSpan(
                source.IndexOf("secondArgument", StringComparison.Ordinal),
                "secondArgument".Length);

            Assert.Equal(source, Reflow(source, 45, partialSelection));
        }

        [Theory]
        [InlineData("Module M\r\n    Sub Main()\r\n        CallMethod(firstArgument, secondArgument, thirdArgument)\r\n    End Sub\r\nEnd Module", 45)]
        [InlineData("Module M\r\n    Dim Values = {firstValue, secondValue, thirdValue}\r\nEnd Module", 42)]
        public void ProducesValidIdempotentListReflow(string source, int maxLineLength)
        {
            string actual = Reflow(source, maxLineLength);

            Assert.NotEqual(source, actual);
            AssertValidSyntax(actual);
            Assert.Equal(actual, Reflow(actual, maxLineLength));
        }

        [Fact]
        public void LeavesStringLiteralUnchanged()
        {
            const string source =
                "Module M\r\n    Dim Value = \"first, second, third, fourth, fifth\"\r\nEnd Module";

            Assert.Equal(source, Reflow(source, 20));
        }

        [Fact]
        public void ReflowsVisualBasicComment()
        {
            const string source =
                "    ' This Visual Basic comment should wrap before the configured maximum line length";

            string actual = Reflow(source, 42);

            Assert.Equal(
                "    ' This Visual Basic comment should\r\n" +
                "    ' wrap before the configured maximum\r\n" +
                "    ' line length",
                actual);
        }

        [Fact]
        public void PreservesDocumentationCommentMarker()
        {
            const string source =
                "    ''' This Visual Basic documentation comment should wrap before the maximum line length";

            string actual = Reflow(source, 45);

            Assert.StartsWith("    ''' This Visual Basic documentation\r\n    ''' comment", actual);
        }

        [Fact]
        public void HonorsCancellationBeforeParsing()
        {
            const string source =
                "Module M\r\n    Sub Main()\r\n        CallMethod(firstArgument, secondArgument, thirdArgument)\r\n    End Sub\r\nEnd Module";
            var cancellationToken = new System.Threading.CancellationToken(canceled: true);

            Assert.Throws<System.OperationCanceledException>(() =>
                VisualBasicListReflow.GetChanges(
                    source,
                    maxLineLength: 45,
                    scope: null,
                    "\r\n",
                    "    ",
                    tabSize: 4,
                    cancellationToken));
        }

        private static void AssertValidSyntax(string source)
        {
            var errors = VisualBasicSyntaxTree.ParseText(source)
                .GetDiagnostics()
                .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);

            Assert.Empty(errors);
        }

        private static string Reflow(
            string source,
            int maxLineLength,
            TextSpan? scope = null,
            string indentUnit = "    ",
            int tabSize = 4)
        {
            var changes = VisualBasicListReflow.GetChanges(
                source,
                maxLineLength,
                scope,
                "\r\n",
                indentUnit,
                tabSize);

            return SourceText.From(source)
                .WithChanges(changes.OrderBy(change => change.Span.Start))
                .ToString();
        }
    }
}
