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
    [ContentType("Basic")]
    [Name(nameof(VisualBasicReflowCommandHandler))]
    [Order(Before = "Format Document")]
    [TextViewRole(PredefinedTextViewRoles.Editable)]
    internal sealed class VisualBasicReflowCommandHandler :
        IChainedCommandHandler<FormatDocumentCommandArgs>,
        IChainedCommandHandler<FormatSelectionCommandArgs>
    {
        private readonly ITextUndoHistoryRegistry _undoHistoryRegistry;
        private readonly DocumentReflowService _reflowService;

        [ImportingConstructor]
        public VisualBasicReflowCommandHandler(
            ITextUndoHistoryRegistry undoHistoryRegistry,
            DocumentReflowService reflowService)
        {
            _undoHistoryRegistry = undoHistoryRegistry;
            _reflowService = reflowService;
        }

        public string DisplayName => "Max line length Visual Basic reflow";

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

        private void Reflow(
            ITextView textView,
            ITextBuffer subjectBuffer,
            bool selectionOnly,
            CancellationToken cancellationToken)
        {
            ReflowOptions? options = ReflowOptions.FromView(textView);
            if (options == null)
            {
                return;
            }

            ITextSnapshot snapshot = subjectBuffer.CurrentSnapshot;
            TextSpan? scope = ReflowSelection.GetScope(textView, snapshot, selectionOnly);
            if (selectionOnly && !scope.HasValue)
            {
                return;
            }

            _reflowService.Reflow(subjectBuffer, options, scope, cancellationToken);
        }
    }
}
