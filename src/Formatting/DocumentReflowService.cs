using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;

namespace MaxLineLength
{
    [Export]
    internal sealed class DocumentReflowService
    {
        private readonly IClassifierAggregatorService _classifierAggregatorService;

        [ImportingConstructor]
        public DocumentReflowService(IClassifierAggregatorService classifierAggregatorService)
        {
            _classifierAggregatorService = classifierAggregatorService;
        }

        public bool CanReflow(IContentType contentType)
        {
            return contentType.IsOfType("CSharp") ||
                contentType.IsOfType("Basic") ||
                CanReflowText(contentType);
        }

        public bool CanReflowText(IContentType contentType)
        {
            return contentType.IsOfType("markdown") ||
                contentType.IsOfType("plaintext") ||
                ReflowContentTypes.SupportsCodeComments(contentType);
        }

        public void Reflow(
            ITextBuffer subjectBuffer,
            ReflowOptions options,
            TextSpan? scope,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ITextSnapshot snapshot = subjectBuffer.CurrentSnapshot;
            TextSpan effectiveScope = scope ?? new TextSpan(0, snapshot.Length);
            IReadOnlyList<TextChange> changes;

            if (subjectBuffer.ContentType.IsOfType("CSharp"))
            {
                changes = CSharpListReflow.GetChanges(
                    snapshot.GetText(),
                    options.MaxLineLength,
                    effectiveScope,
                    options.NewLine,
                    options.IndentUnit,
                    options.TabSize,
                    cancellationToken);
            }
            else if (subjectBuffer.ContentType.IsOfType("Basic"))
            {
                changes = VisualBasicListReflow.GetChanges(
                    snapshot.GetText(),
                    options.MaxLineLength,
                    effectiveScope,
                    options.NewLine,
                    options.IndentUnit,
                    options.TabSize,
                    cancellationToken);
            }
            else if (ReflowContentTypes.SupportsCodeComments(subjectBuffer.ContentType))
            {
                IClassifier classifier = _classifierAggregatorService.GetClassifier(subjectBuffer);
                var snapshotSpan = new SnapshotSpan(snapshot, 0, snapshot.Length);
                IEnumerable<TextSpan> commentSpans = classifier
                    .GetClassificationSpans(snapshotSpan)
                    .Where(classification =>
                        classification.ClassificationType.IsOfType("comment"))
                    .Select(classification => new TextSpan(
                        classification.Span.Start.Position,
                        classification.Span.Length));

                changes = CommentReflow.GetChanges(
                    snapshot.GetText(),
                    commentSpans,
                    options.MaxLineLength,
                    effectiveScope,
                    options.NewLine,
                    options.TabSize,
                    cancellationToken);
            }
            else if (subjectBuffer.ContentType.IsOfType("markdown"))
            {
                changes = MarkdownReflow.GetChanges(
                    snapshot.GetText(),
                    options.MaxLineLength,
                    effectiveScope,
                    options.NewLine,
                    options.TabSize,
                    cancellationToken);
            }
            else if (subjectBuffer.ContentType.IsOfType("plaintext"))
            {
                changes = TextLineReflow.GetChanges(
                    snapshot.GetText(),
                    options.MaxLineLength,
                    effectiveScope,
                    options.NewLine,
                    options.TabSize,
                    cancellationToken);
            }
            else
            {
                return;
            }

            ReflowSelection.ApplyChanges(subjectBuffer, changes);
        }
    }
}
