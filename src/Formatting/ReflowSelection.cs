using System.Collections.Generic;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;

namespace MaxLineLength
{
    internal static class ReflowSelection
    {
        public static TextSpan? GetScope(ITextView textView, ITextSnapshot snapshot, bool selectionOnly)
        {
            if (!selectionOnly)
            {
                return new TextSpan(0, snapshot.Length);
            }

            NormalizedSnapshotSpanCollection selectedSpans = textView.Selection.SelectedSpans;
            if (selectedSpans.Count != 1 || selectedSpans[0].IsEmpty || selectedSpans[0].Snapshot != snapshot)
            {
                return null;
            }

            SnapshotSpan selection = selectedSpans[0];
            return new TextSpan(selection.Start.Position, selection.Length);
        }

        public static void ApplyChanges(ITextBuffer subjectBuffer, IReadOnlyList<TextChange> changes)
        {
            if (changes.Count == 0)
            {
                return;
            }

            using (ITextEdit edit = subjectBuffer.CreateEdit())
            {
                foreach (TextChange change in changes)
                {
                    edit.Replace(change.Span.Start, change.Span.Length, change.NewText);
                }

                edit.Apply();
            }
        }
    }
}
