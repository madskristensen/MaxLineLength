using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Threading;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.Commanding;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Editor.Commanding.Commands;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Utilities;

namespace MaxLineLength
{
    [Export(typeof(ICommandHandler))]
    [ContentType("CSharp")]
    [Name(nameof(CSharpReflowCommandHandler))]
    [Order(Before = "Format Document")]
    [TextViewRole(PredefinedTextViewRoles.Editable)]
    internal sealed class CSharpReflowCommandHandler :
        IChainedCommandHandler<FormatDocumentCommandArgs>,
        IChainedCommandHandler<FormatSelectionCommandArgs>
    {
        private readonly ITextUndoHistoryRegistry _undoHistoryRegistry;

        [ImportingConstructor]
        public CSharpReflowCommandHandler(ITextUndoHistoryRegistry undoHistoryRegistry)
        {
            _undoHistoryRegistry = undoHistoryRegistry;
        }

        public string DisplayName => "Max line length reflow";

        public CommandState GetCommandState(
            FormatDocumentCommandArgs args,
            Func<CommandState> nextCommandHandler)
        {
            return nextCommandHandler();
        }

        public CommandState GetCommandState(
            FormatSelectionCommandArgs args,
            Func<CommandState> nextCommandHandler)
        {
            return nextCommandHandler();
        }

        public void ExecuteCommand(
            FormatDocumentCommandArgs args,
            Action nextCommandHandler,
            CommandExecutionContext executionContext)
        {
            Execute(
                args.TextView,
                args.SubjectBuffer,
                selectionOnly: false,
                nextCommandHandler,
                executionContext.OperationContext.UserCancellationToken);
        }

        public void ExecuteCommand(
            FormatSelectionCommandArgs args,
            Action nextCommandHandler,
            CommandExecutionContext executionContext)
        {
            Execute(
                args.TextView,
                args.SubjectBuffer,
                selectionOnly: true,
                nextCommandHandler,
                executionContext.OperationContext.UserCancellationToken);
        }

        private void Execute(
            ITextView textView,
            ITextBuffer subjectBuffer,
            bool selectionOnly,
            Action nextCommandHandler,
            CancellationToken cancellationToken)
        {
            if (textView.TextBuffer != subjectBuffer)
            {
                nextCommandHandler();
                return;
            }

            using (ITextUndoTransaction transaction =
                _undoHistoryRegistry.GetHistory(subjectBuffer).CreateTransaction("Format and reflow"))
            {
                nextCommandHandler();
                Reflow(textView, subjectBuffer, selectionOnly, cancellationToken);
                transaction.Complete();
            }
        }

        private static void Reflow(
            ITextView textView,
            ITextBuffer subjectBuffer,
            bool selectionOnly,
            CancellationToken cancellationToken)
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
            int indentSize = MaxLineLengthSettings.GetIndentSize(textView);
            int tabSize = MaxLineLengthSettings.GetTabSize(textView);
            string indentUnit = MaxLineLengthSettings.UseTabs(textView)
                ? "\t"
                : new string(' ', indentSize);

            IReadOnlyList<TextChange> changes = CSharpListReflow.GetChanges(
                snapshot.GetText(),
                maxLineLength.Value,
                scope,
                newLine,
                indentUnit,
                tabSize,
                cancellationToken);

            ReflowSelection.ApplyChanges(subjectBuffer, changes);
        }
    }
}
