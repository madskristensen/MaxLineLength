using System.Collections.Generic;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;

namespace MaxLineLength
{
    internal sealed class ReflowOptions
    {
        private ReflowOptions(
            int maxLineLength,
            string newLine,
            string indentUnit,
            int tabSize)
        {
            MaxLineLength = maxLineLength;
            NewLine = newLine;
            IndentUnit = indentUnit;
            TabSize = tabSize;
        }

        public int MaxLineLength { get; }

        public string NewLine { get; }

        public string IndentUnit { get; }

        public int TabSize { get; }

        public static ReflowOptions? FromView(ITextView textView)
        {
            int? maxLineLength = MaxLineLengthSettings.GetMaxLineLength(textView);
            if (!maxLineLength.HasValue)
            {
                return null;
            }

            int indentSize = MaxLineLengthSettings.GetIndentSize(textView);
            return new ReflowOptions(
                maxLineLength.Value,
                textView.Options.GetOptionValue<string>(DefaultOptions.NewLineCharacterOptionName),
                MaxLineLengthSettings.UseTabs(textView) ? "\t" : new string(' ', indentSize),
                MaxLineLengthSettings.GetTabSize(textView));
        }

        public static ReflowOptions? FromFile(string filePath, string text)
        {
            return FromConventions(MaxLineLengthSettings.GetConventions(filePath), text);
        }

        internal static ReflowOptions? FromConventions(
            IReadOnlyDictionary<string, object>? conventions,
            string text)
        {
            int? maxLineLength = MaxLineLengthSettings.GetMaxLineLength(conventions);
            if (!maxLineLength.HasValue)
            {
                return null;
            }

            int indentSize = MaxLineLengthSettings.GetIndentSize(conventions);
            return new ReflowOptions(
                maxLineLength.Value,
                GetNewLine(conventions, text),
                MaxLineLengthSettings.UseTabs(conventions) ? "\t" : new string(' ', indentSize),
                MaxLineLengthSettings.GetTabSize(conventions));
        }

        private static string GetNewLine(
            IReadOnlyDictionary<string, object>? conventions,
            string text)
        {
            if (conventions != null &&
                conventions.TryGetValue("end_of_line", out object endOfLine))
            {
                switch (endOfLine as string)
                {
                    case "lf":
                        return "\n";
                    case "cr":
                        return "\r";
                    case "crlf":
                        return "\r\n";
                }
            }

            int lineFeed = text.IndexOf('\n');
            if (lineFeed >= 0)
            {
                return lineFeed > 0 && text[lineFeed - 1] == '\r' ? "\r\n" : "\n";
            }

            return text.IndexOf('\r') >= 0 ? "\r" : Environment.NewLine;
        }
    }
}
