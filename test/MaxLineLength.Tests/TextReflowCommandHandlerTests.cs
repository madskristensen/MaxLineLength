using Xunit;

namespace MaxLineLength.Tests
{
    public class TextReflowCommandHandlerTests
    {
        [Theory]
        [InlineData("plaintext", false, false, true)]
        [InlineData("PlainText", false, false, true)]
        [InlineData("markdown", false, true, true)]
        [InlineData("JavaScript", true, false, true)]
        [InlineData("yaml", false, false, false)]
        [InlineData("csv", false, false, false)]
        public void ReflowsOnlySupportedContent(
            string contentTypeName,
            bool supportsCodeComments,
            bool isMarkdown,
            bool expected)
        {
            Assert.Equal(
                expected,
                ReflowContentTypes.IsSupported(
                    contentTypeName,
                    supportsCodeComments,
                    isMarkdown));
        }
    }
}
