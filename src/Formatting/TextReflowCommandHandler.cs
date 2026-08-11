using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.Commanding;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Editor.Commanding.Commands;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Utilities;

namespace MaxLineLength
{
    [Export(typeof(ICommandHandler))]
    [ContentType("text")]
    [Name(nameof(TextReflowCommandHandler))]
    [Order(Before = "Format Document")]
    [TextViewRole(PredefinedTextViewRoles.Editable)]
    internal sealed class TextReflowCommandHandler :
        IChainedCommandHandler<FormatDocumentCommandArgs>,
        IChainedCommandHandler<FormatSelectionCommandArgs>
    {
        private readonly ITextUndoHistoryRegistry _undoHistoryRegistry;
        private readonly IClassifierAggregatorService _classifierAggregatorService;

        [ImportingConstructor]
        public TextReflowCommandHandler(
            ITextUndoHistoryRegistry undoHistoryRegistry,
            IClassifierAggregatorService classifierAggregatorService)
        {
            _undoHistoryRegistry = undoHistoryRegistry;
            _classifierAggregatorService = classifierAggregatorService;
        }

        public string DisplayName => "Max line length text reflow";

        public CommandState GetCommandState(FormatDocumentCommandArgs args, Func<CommandState> nextCommandHandler)
            => nextCommandHandler();

        public CommandState GetCommandState(FormatSelectionCommandArgs args, Func<CommandState> nextCommandHandler)
            => nextCommandHandler();

        public void ExecuteCommand(
            FormatDocumentCommandArgs args,
            Action nextCommandHandler,
            CommandExecutionContext executionContext)
        {
            Execute(args.TextView, args.SubjectBuffer, selectionOnly: false, nextCommandHandler);
        }

        public void ExecuteCommand(
            FormatSelectionCommandArgs args,
            Action nextCommandHandler,
            CommandExecutionContext executionContext)
        {
            Execute(args.TextView, args.SubjectBuffer, selectionOnly: true, nextCommandHandler);
        }

        private void Execute(
            ITextView textView,
            ITextBuffer subjectBuffer,
            bool selectionOnly,
            Action nextCommandHandler)
        {
            bool supportsCodeComments = SupportsCodeComments(subjectBuffer);
            bool isMarkdown = subjectBuffer.ContentType.IsOfType("markdown");

            if (textView.TextBuffer != subjectBuffer ||
                !ReflowContentTypes.IsSupported(
                    subjectBuffer.ContentType.TypeName,
                    supportsCodeComments,
                    isMarkdown))
            {
                nextCommandHandler();
                return;
            }

            using (ITextUndoTransaction transaction =
                _undoHistoryRegistry.GetHistory(subjectBuffer).CreateTransaction("Format and reflow"))
            {
                nextCommandHandler();
                Reflow(textView, subjectBuffer, selectionOnly, supportsCodeComments, isMarkdown);
                transaction.Complete();
            }
        }

        private void Reflow(
            ITextView textView,
            ITextBuffer subjectBuffer,
            bool selectionOnly,
            bool supportsCodeComments,
            bool isMarkdown)
        {
            int? maxLineLength = MaxLineLengthSettings.GetMaxLineLength(textView);
            if (!maxLineLength.HasValue)
            {
                return;
            }

            ITextSnapshot snapshot = subjectBuffer.CurrentSnapshot;
            TextSpan? scope = ReflowSelection.GetScope(textView, snapshot, selectionOnly);
            if (selectionOnly && !scope.HasValue)
            {
                return;
            }

            string newLine = textView.Options.GetOptionValue<string>(DefaultOptions.NewLineCharacterOptionName);
            int tabSize = MaxLineLengthSettings.GetTabSize(textView);
            IReadOnlyList<TextChange> changes;

            if (supportsCodeComments)
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
                    maxLineLength.Value,
                    scope,
                    newLine,
                    tabSize);
            }
            else if (isMarkdown)
            {
                changes = MarkdownReflow.GetChanges(
                    snapshot.GetText(),
                    maxLineLength.Value,
                    scope,
                    newLine,
                    tabSize);
            }
            else
            {
                changes = TextLineReflow.GetChanges(
                    snapshot.GetText(),
                    maxLineLength.Value,
                    scope,
                    newLine,
                    tabSize);
            }

            ReflowSelection.ApplyChanges(subjectBuffer, changes);
        }

        private static bool SupportsCodeComments(ITextBuffer subjectBuffer)
        {
            return subjectBuffer.ContentType.IsOfType("JavaScript") ||
                subjectBuffer.ContentType.IsOfType("TypeScript") ||
                subjectBuffer.ContentType.IsOfType("C/C++");
        }
    }
}
