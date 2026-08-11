using System.Collections.Generic;
using System.Globalization;
using Microsoft.VisualStudio.Text.Editor;

namespace MaxLineLength
{
    internal static class MaxLineLengthSettings
    {
        private const string CodingConventionsOptionName = "CodingConventionsSnapshot";
        private const int MaximumColumn = 10000;

        public static int? GetMaxLineLength(ITextView view)
        {
            IReadOnlyDictionary<string, object> conventions = GetConventions(view);
            return TryGetPositiveInt(conventions, "max_line_length", out int value) && value <= MaximumColumn
                ? value
                : null;
        }

        public static int GetIndentSize(ITextView view)
        {
            IReadOnlyDictionary<string, object> conventions = GetConventions(view);
            return TryGetPositiveInt(conventions, "indent_size", out int value) ? value : 4;
        }

        public static bool UseTabs(ITextView view)
        {
            IReadOnlyDictionary<string, object> conventions = GetConventions(view);
            return conventions != null &&
                conventions.TryGetValue("indent_style", out object value) &&
                string.Equals(value as string, "tab", StringComparison.OrdinalIgnoreCase);
        }

        private static IReadOnlyDictionary<string, object> GetConventions(ITextView view)
        {
            return view.Options.GetOptionValue<IReadOnlyDictionary<string, object>>(CodingConventionsOptionName);
        }

        private static bool TryGetPositiveInt(
            IReadOnlyDictionary<string, object> conventions,
            string settingName,
            out int value)
        {
            if (conventions != null && conventions.TryGetValue(settingName, out object settingValue))
            {
                if (settingValue is int intValue && intValue > 0)
                {
                    value = intValue;
                    return true;
                }

                if (settingValue is string text &&
                    int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out int parsedValue) &&
                    parsedValue > 0)
                {
                    value = parsedValue;
                    return true;
                }
            }

            value = 0;
            return false;
        }
    }
}
