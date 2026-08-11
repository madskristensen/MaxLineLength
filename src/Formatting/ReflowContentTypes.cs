using System.Collections.Generic;
using Microsoft.VisualStudio.Utilities;

namespace MaxLineLength
{
    internal static class ReflowContentTypes
    {
        private static readonly string[] _commentContentTypes =
        {
            "JavaScript",
            "TypeScript",
            "C/C++",
            "F#",
            "FSharp",
            "CSS",
            "LESS",
            "SCSS",
            "SQL",
            "T-SQL",
            "T-SQL7",
            "T-SQL80",
            "T-SQL90",
            "SQL Server Tools"
        };

        public static bool IsSupported(
            string contentTypeName,
            bool supportsCodeComments,
            bool isMarkdown)
        {
            return supportsCodeComments ||
                isMarkdown ||
                string.Equals(contentTypeName, "plaintext", StringComparison.OrdinalIgnoreCase);
        }

        public static bool SupportsCodeComments(string contentTypeName)
        {
            foreach (string supportedType in _commentContentTypes)
            {
                if (string.Equals(contentTypeName, supportedType, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        public static bool SupportsCodeComments(IContentType contentType)
        {
            foreach (string supportedType in _commentContentTypes)
            {
                if (contentType.IsOfType(supportedType))
                {
                    return true;
                }
            }

            return false;
        }

        public static IReadOnlyList<string> CommentContentTypes => _commentContentTypes;
    }
}
