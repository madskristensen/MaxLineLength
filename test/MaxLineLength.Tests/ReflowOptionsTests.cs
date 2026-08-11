using System.Collections.Generic;
using Xunit;

namespace MaxLineLength.Tests
{
    public class ReflowOptionsTests
    {
        [Fact]
        public void ReturnsNullWithoutMaxLineLength()
        {
            var conventions = new Dictionary<string, object>();

            Assert.Null(ReflowOptions.FromConventions(conventions, "text\r\n"));
        }

        [Fact]
        public void UsesEditorConfigFormattingConventions()
        {
            var conventions = new Dictionary<string, object>
            {
                ["max_line_length"] = "100",
                ["indent_style"] = "tab",
                ["indent_size"] = "tab",
                ["tab_width"] = "8",
                ["end_of_line"] = "lf",
            };

            ReflowOptions options = ReflowOptions.FromConventions(conventions, "text\r\n");

            Assert.Equal(100, options.MaxLineLength);
            Assert.Equal("\t", options.IndentUnit);
            Assert.Equal(8, options.TabSize);
            Assert.Equal("\n", options.NewLine);
        }

        [Theory]
        [InlineData("first\r\nsecond", "\r\n")]
        [InlineData("first\nsecond", "\n")]
        [InlineData("first\rsecond", "\r")]
        public void InfersExistingNewLineWhenNotConfigured(string text, string expected)
        {
            var conventions = new Dictionary<string, object>
            {
                ["max_line_length"] = 80,
            };

            ReflowOptions options = ReflowOptions.FromConventions(conventions, text);

            Assert.Equal(expected, options.NewLine);
        }
    }
}
