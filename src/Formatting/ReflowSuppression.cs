using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis.Text;

namespace MaxLineLength
{
    internal static class ReflowSuppression
    {
        private const string DisableMarker = "@formatter:off";
        private const string EnableMarker = "@formatter:on";

        public static IReadOnlyList<TextSpan> GetSpans(
            SourceText sourceText,
            IEnumerable<TextSpan>? eligibleSpans = null)
        {
            var markers = new Dictionary<int, bool>();
            IEnumerable<TextSpan> searchSpans = eligibleSpans ??
                new[] { new TextSpan(0, sourceText.Length) };

            foreach (TextSpan span in searchSpans)
            {
                AddMarkers(sourceText, span, DisableMarker, isDisable: true, markers);
                AddMarkers(sourceText, span, EnableMarker, isDisable: false, markers);
            }

            var suppressedSpans = new List<TextSpan>();
            int depth = 0;
            int start = 0;

            foreach (KeyValuePair<int, bool> marker in markers.OrderBy(pair => pair.Key))
            {
                if (marker.Value)
                {
                    if (depth == 0)
                    {
                        start = sourceText.Lines.GetLineFromPosition(marker.Key).Start;
                    }

                    depth++;
                }
                else if (depth > 0)
                {
                    depth--;
                    if (depth == 0)
                    {
                        TextLine line = sourceText.Lines.GetLineFromPosition(marker.Key);
                        suppressedSpans.Add(TextSpan.FromBounds(start, line.EndIncludingLineBreak));
                    }
                }
            }

            if (depth > 0)
            {
                suppressedSpans.Add(TextSpan.FromBounds(start, sourceText.Length));
            }

            return suppressedSpans;
        }

        public static bool OverlapsAny(IReadOnlyList<TextSpan> spans, TextSpan candidate)
        {
            foreach (TextSpan span in spans)
            {
                if (span.Start >= candidate.End)
                {
                    return false;
                }

                if (span.OverlapsWith(candidate))
                {
                    return true;
                }
            }

            return false;
        }

        private static void AddMarkers(
            SourceText sourceText,
            TextSpan span,
            string marker,
            bool isDisable,
            IDictionary<int, bool> markers)
        {
            if (span.IsEmpty || span.Start < 0 || span.End > sourceText.Length)
            {
                return;
            }

            string text = sourceText.ToString(span);
            int index = 0;

            while ((index = text.IndexOf(marker, index, StringComparison.OrdinalIgnoreCase)) >= 0)
            {
                markers[span.Start + index] = isDisable;
                index += marker.Length;
            }
        }
    }
}
