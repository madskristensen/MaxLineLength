using Xunit;

namespace MaxLineLength.Tests
{
    public class CodeCleanupMetadataTests
    {
        [Fact]
        public void ReflowFixUsesStableIdentifier()
        {
            Assert.Equal("MaxLineLength.ReflowLines", MaxLineLengthCodeCleanupFixIds.ReflowLines);
        }
    }
}
