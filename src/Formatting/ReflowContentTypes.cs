namespace MaxLineLength
{
    internal static class ReflowContentTypes
    {
        public static bool IsSupported(
            string contentTypeName,
            bool supportsCodeComments,
            bool isMarkdown)
        {
            return supportsCodeComments ||
                isMarkdown ||
                string.Equals(contentTypeName, "plaintext", StringComparison.OrdinalIgnoreCase);
        }
    }
}
