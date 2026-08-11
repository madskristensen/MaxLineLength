using System.Collections.Generic;
using System.IO;
using Xunit;

namespace MaxLineLength.Tests
{
    public class MaxLineLengthSettingsTests
    {
        [Fact]
        public void UsesTabWidthWhenIndentSizeIsTab()
        {
            var conventions = new Dictionary<string, object>
            {
                ["indent_size"] = "tab",
                ["tab_width"] = "8",
            };

            Assert.Equal(8, MaxLineLengthSettings.GetIndentSize(conventions));
            Assert.Equal(8, MaxLineLengthSettings.GetTabSize(conventions));
        }

        [Fact]
        public void UsesNumericIndentSizeAsDefaultTabSize()
        {
            var conventions = new Dictionary<string, object>
            {
                ["indent_size"] = 2,
            };

            Assert.Equal(2, MaxLineLengthSettings.GetIndentSize(conventions));
            Assert.Equal(2, MaxLineLengthSettings.GetTabSize(conventions));
        }

        [Fact]
        public void RejectsUnreasonablyLargeIndentationValues()
        {
            var conventions = new Dictionary<string, object>
            {
                ["indent_size"] = int.MaxValue,
                ["tab_width"] = int.MaxValue,
            };

            Assert.Equal(4, MaxLineLengthSettings.GetIndentSize(conventions));
            Assert.Equal(4, MaxLineLengthSettings.GetTabSize(conventions));
        }

        [Fact]
        public void ResolvesEffectiveMaxLineLengthFromFile()
        {
            string directory = CreateTemporaryDirectory();
            try
            {
                File.WriteAllText(
                    Path.Combine(directory, ".editorconfig"),
                    "root = true\r\n[*]\r\nmax_line_length = 80\r\n[*.cs]\r\nmax_line_length = 120\r\n");
                string csharpFile = Path.Combine(directory, "File.cs");
                string textFile = Path.Combine(directory, "File.txt");
                File.WriteAllText(csharpFile, string.Empty);
                File.WriteAllText(textFile, string.Empty);

                Assert.Equal(120, MaxLineLengthSettings.GetMaxLineLength(csharpFile));
                Assert.Equal(80, MaxLineLengthSettings.GetMaxLineLength(textFile));
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }

        [Fact]
        public void HonorsUnsetMaxLineLengthFromFile()
        {
            string directory = CreateTemporaryDirectory();
            try
            {
                File.WriteAllText(
                    Path.Combine(directory, ".editorconfig"),
                    "root = true\r\n[*]\r\nmax_line_length = 80\r\n[*.cs]\r\nmax_line_length = unset\r\n");
                string csharpFile = Path.Combine(directory, "File.cs");
                File.WriteAllText(csharpFile, string.Empty);

                Assert.Null(MaxLineLengthSettings.GetMaxLineLength(csharpFile));
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }

        private static string CreateTemporaryDirectory()
        {
            string directory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(directory);
            return directory;
        }
    }
}
