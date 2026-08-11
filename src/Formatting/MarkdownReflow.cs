using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis.Text;

namespace MaxLineLength
{
    internal static class MarkdownReflow
    {
        public static IReadOnlyList<TextChange> GetChanges(
            string text,
            int maxLineLength,
            TextSpan? scope,
            string newLine,
            int tabSize)
        {
            SourceText sourceText = SourceText.From(text);
            bool[] excludedLines = GetExcludedLines(sourceText);
            var changes = new List<TextChange>();
            int lineNumber = 0;

            while (lineNumber < sourceText.Lines.Count)
            {
                if (!IsParagraphLine(sourceText, lineNumber, excludedLines))
                {
                    lineNumber++;
                    continue;
                }

                int firstLine = lineNumber;
                string indentation = TextLineReflow.GetLeadingWhitespace(
                    sourceText.Lines[firstLine].ToString());

                while (lineNumber + 1 < sourceText.Lines.Count &&
                    IsParagraphLine(sourceText, lineNumber + 1, excludedLines) &&
                    TextLineReflow.GetLeadingWhitespace(
                        sourceText.Lines[lineNumber + 1].ToString()) == indentation)
                {
                    lineNumber++;
                }

                TextSpan paragraphSpan = TextSpan.FromBounds(
                    sourceText.Lines[firstLine].Start,
                    sourceText.Lines[lineNumber].End);

                if (!scope.HasValue || scope.Value.Contains(paragraphSpan))
                {
                    string paragraph = string.Join(
                        " ",
                        Enumerable.Range(firstLine, lineNumber - firstLine + 1)
                            .Select(index => sourceText.Lines[index].ToString().Trim()));
                    string replacement = TextLineReflow.WrapLine(
                        indentation + paragraph,
                        maxLineLength,
                        newLine,
                        tabSize);
                    string original = sourceText.ToString(paragraphSpan);

                    if (!string.Equals(original, replacement, StringComparison.Ordinal))
                    {
                        changes.Add(new TextChange(paragraphSpan, replacement));
                    }
                }

                lineNumber++;
            }

            return changes.OrderByDescending(change => change.Span.Start).ToArray();
        }

        private static bool[] GetExcludedLines(SourceText sourceText)
        {
            var excluded = new bool[sourceText.Lines.Count];
            bool inFence = false;
            char fenceCharacter = '\0';
            int fenceLength = 0;
            bool inFrontMatter = sourceText.Lines.Count > 0 &&
                sourceText.Lines[0].ToString().Trim() == "---";

            for (int index = 0; index < sourceText.Lines.Count; index++)
            {
                string trimmed = sourceText.Lines[index].ToString().Trim();

                if (inFrontMatter)
                {
                    excluded[index] = true;
                    if (index > 0 && (trimmed == "---" || trimmed == "..."))
                    {
                        inFrontMatter = false;
                    }

                    continue;
                }

                if (TryGetFence(trimmed, out char character, out int length))
                {
                    if (!inFence)
                    {
                        inFence = true;
                        fenceCharacter = character;
                        fenceLength = length;
                    }
                    else if (character == fenceCharacter && length >= fenceLength)
                    {
                        inFence = false;
                    }

                    excluded[index] = true;
                    continue;
                }

                excluded[index] = inFence;
            }

            return excluded;
        }

        private static bool IsParagraphLine(
            SourceText sourceText,
            int lineNumber,
            bool[] excludedLines)
        {
            if (excludedLines[lineNumber])
            {
                return false;
            }

            string line = sourceText.Lines[lineNumber].ToString();
            string trimmed = line.Trim();
            int indentation = line.Length - line.TrimStart().Length;

            if (trimmed.Length == 0 ||
                indentation >= 4 ||
                line.StartsWith("\t", StringComparison.Ordinal) ||
                HasStructuralPrefix(trimmed) ||
                IsThematicBreak(trimmed) ||
                IsSetextUnderline(trimmed) ||
                IsOrderedListItem(trimmed) ||
                trimmed.Contains("|") ||
                trimmed.Contains("`") ||
                trimmed.Contains("<") ||
                trimmed.Contains(">") ||
                line.EndsWith("  ", StringComparison.Ordinal) ||
                line.EndsWith("\\", StringComparison.Ordinal))
            {
                return false;
            }

            return lineNumber + 1 >= sourceText.Lines.Count ||
                !IsSetextUnderline(sourceText.Lines[lineNumber + 1].ToString().Trim());
        }

        private static bool HasStructuralPrefix(string text)
        {
            return text.StartsWith("#", StringComparison.Ordinal) ||
                text.StartsWith(">", StringComparison.Ordinal) ||
                text.StartsWith("- ", StringComparison.Ordinal) ||
                text.StartsWith("* ", StringComparison.Ordinal) ||
                text.StartsWith("+ ", StringComparison.Ordinal) ||
                text.StartsWith("[", StringComparison.Ordinal) ||
                text.StartsWith(":", StringComparison.Ordinal);
        }

        private static bool IsOrderedListItem(string text)
        {
            int index = 0;
            while (index < text.Length && char.IsDigit(text[index]))
            {
                index++;
            }

            return index > 0 &&
                index + 1 < text.Length &&
                (text[index] == '.' || text[index] == ')') &&
                char.IsWhiteSpace(text[index + 1]);
        }

        private static bool IsThematicBreak(string text)
        {
            string compact = text.Replace(" ", string.Empty);
            return compact.Length >= 3 &&
                (compact.All(character => character == '-') ||
                 compact.All(character => character == '*') ||
                 compact.All(character => character == '_'));
        }

        private static bool IsSetextUnderline(string text)
        {
            return text.Length > 0 &&
                (text.All(character => character == '=') ||
                 text.All(character => character == '-'));
        }

        private static bool TryGetFence(string text, out char character, out int length)
        {
            character = text.Length > 0 ? text[0] : '\0';
            length = 0;

            if (character != '`' && character != '~')
            {
                return false;
            }

            while (length < text.Length && text[length] == character)
            {
                length++;
            }

            return length >= 3;
        }
    }
}
