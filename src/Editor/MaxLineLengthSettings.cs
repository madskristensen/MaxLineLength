using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using EditorConfig.Core;
using Microsoft.VisualStudio.Text.Editor;

namespace MaxLineLength
{
    internal static class MaxLineLengthSettings
    {
        private const string CodingConventionsOptionName = "CodingConventionsSnapshot";
        private const int DefaultIndentSize = 4;
        private const int MaximumColumn = 10000;
        private const int MaximumIndentSize = 256;

        public static int? GetMaxLineLength(ITextView view)
        {
            if (ResolvedMaxLineLengthCache.Values.TryGetValue(
                view,
                out ResolvedMaxLineLength resolvedMaxLineLength))
            {
                return resolvedMaxLineLength.Value;
            }

            IReadOnlyDictionary<string, object>? conventions = GetConventions(view);
            return TryGetPositiveInt(conventions, "max_line_length", MaximumColumn, out int value)
                ? value
                : null;
        }

        internal static void SetResolvedMaxLineLength(ITextView view, int? value)
        {
            ResolvedMaxLineLengthCache.Values.Remove(view);
            ResolvedMaxLineLengthCache.Values.Add(view, new ResolvedMaxLineLength(value));
        }

        internal static int? GetMaxLineLength(string filePath)
        {
            FileConfiguration configuration = new EditorConfigParser().Parse(filePath);
            return configuration.Properties.TryGetValue("max_line_length", out string value) &&
                int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int parsedValue) &&
                parsedValue > 0 &&
                parsedValue <= MaximumColumn
                ? parsedValue
                : null;
        }

        public static int GetIndentSize(ITextView view)
        {
            return GetIndentSize(GetConventions(view));
        }

        internal static int GetIndentSize(IReadOnlyDictionary<string, object>? conventions)
        {
            if (TryGetPositiveInt(conventions, "indent_size", MaximumIndentSize, out int indentSize))
            {
                return indentSize;
            }

            return IsSetting(conventions, "indent_size", "tab")
                ? GetTabSize(conventions)
                : DefaultIndentSize;
        }

        public static int GetTabSize(ITextView view)
        {
            return GetTabSize(GetConventions(view));
        }

        internal static int GetTabSize(IReadOnlyDictionary<string, object>? conventions)
        {
            if (TryGetPositiveInt(conventions, "tab_width", MaximumIndentSize, out int tabSize))
            {
                return tabSize;
            }

            return TryGetPositiveInt(conventions, "indent_size", MaximumIndentSize, out int indentSize)
                ? indentSize
                : DefaultIndentSize;
        }

        public static bool UseTabs(ITextView view)
        {
            return IsSetting(GetConventions(view), "indent_style", "tab");
        }

        private static IReadOnlyDictionary<string, object>? GetConventions(ITextView view)
        {
            return view.Options.GetOptionValue<IReadOnlyDictionary<string, object>>(
                CodingConventionsOptionName);
        }

        private static bool IsSetting(
            IReadOnlyDictionary<string, object>? conventions,
            string settingName,
            string expectedValue)
        {
            return conventions != null &&
                conventions.TryGetValue(settingName, out object settingValue) &&
                string.Equals(settingValue as string, expectedValue, StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryGetPositiveInt(
            IReadOnlyDictionary<string, object>? conventions,
            string settingName,
            int maximum,
            out int value)
        {
            if (conventions != null && conventions.TryGetValue(settingName, out object settingValue))
            {
                if (settingValue is int intValue && intValue > 0 && intValue <= maximum)
                {
                    value = intValue;
                    return true;
                }

                if (settingValue is string text &&
                    int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out int parsedValue) &&
                    parsedValue > 0 &&
                    parsedValue <= maximum)
                {
                    value = parsedValue;
                    return true;
                }
            }

            value = 0;
            return false;
        }

        private static class ResolvedMaxLineLengthCache
        {
            public static readonly ConditionalWeakTable<ITextView, ResolvedMaxLineLength> Values = new();
        }

        private sealed class ResolvedMaxLineLength
        {
            public ResolvedMaxLineLength(int? value)
            {
                Value = value;
            }

            public int? Value { get; }
        }
    }
}
