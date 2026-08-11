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

        [Theory]
        [InlineData("F#")]
        [InlineData("FSharp")]
        [InlineData("CSS")]
        [InlineData("LESS")]
        [InlineData("SCSS")]
        [InlineData("SQL")]
        [InlineData("T-SQL")]
        [InlineData("T-SQL7")]
        [InlineData("T-SQL80")]
        [InlineData("T-SQL90")]
        [InlineData("SQL Server Tools")]
        public void RecognizesAdditionalCommentContentTypes(string contentTypeName)
        {
            Assert.True(ReflowContentTypes.SupportsCodeComments(contentTypeName));
        }

        [Theory]
        [InlineData("yaml")]
        [InlineData("json")]
        [InlineData("Basic")]
        public void RejectsUnsupportedGenericCommentContentTypes(string contentTypeName)
        {
            Assert.False(ReflowContentTypes.SupportsCodeComments(contentTypeName));
        }
    }
}
