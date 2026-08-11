using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Threading.Tasks;
using MaxLineLength.Properties;
using Microsoft.VisualStudio.Language.CodeCleanUp;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Threading;
using Microsoft.VisualStudio.Utilities;

namespace MaxLineLength
{
    internal static class MaxLineLengthCodeCleanupFixIds
    {
        public const string ReflowLines = "MaxLineLength.ReflowLines";

        [Export]
        [Name(ReflowLines)]
        [FixId(ReflowLines)]
        [ConfigurationKey(ReflowLines)]
        [LocalizedName(typeof(Resources), nameof(Resources.CodeCleanupReflowLines))]
        [ContentType("text")]
        public static readonly FixIdDefinition? ReflowLinesDefinition;
    }

    [Export(typeof(ICodeCleanUpFixerProvider))]
    [ContentType("text")]
    internal sealed class MaxLineLengthCodeCleanupFixerProvider : ICodeCleanUpFixerProvider
    {
        private readonly DocumentReflowService _reflowService;
        private readonly ITextDocumentFactoryService _textDocumentFactoryService;
        private readonly JoinableTaskContext _joinableTaskContext;
        private readonly IReadOnlyCollection<ICodeCleanUpFixer> _fixers;

        [ImportingConstructor]
        public MaxLineLengthCodeCleanupFixerProvider(
            DocumentReflowService reflowService,
            ITextDocumentFactoryService textDocumentFactoryService,
            JoinableTaskContext joinableTaskContext)
        {
            _reflowService = reflowService;
            _textDocumentFactoryService = textDocumentFactoryService;
            _joinableTaskContext = joinableTaskContext;
            _fixers = new[] { new MaxLineLengthCodeCleanupFixer(this) };
        }

        public IReadOnlyCollection<ICodeCleanUpFixer> GetFixers()
        {
            return Array.Empty<ICodeCleanUpFixer>();
        }

        public IReadOnlyCollection<ICodeCleanUpFixer> GetFixers(IContentType contentType)
        {
            return _reflowService.CanReflow(contentType)
                ? _fixers
                : Array.Empty<ICodeCleanUpFixer>();
        }

        private sealed class MaxLineLengthCodeCleanupFixer : ICodeCleanUpFixer
        {
            private readonly MaxLineLengthCodeCleanupFixerProvider _provider;

            public MaxLineLengthCodeCleanupFixer(MaxLineLengthCodeCleanupFixerProvider provider)
            {
                _provider = provider;
            }

            public Task<bool> FixAsync(
                ICodeCleanUpScope scope,
                ICodeCleanUpExecutionContext context)
            {
                if (!context.EnabledFixIds.IsFixIdEnabled(MaxLineLengthCodeCleanupFixIds.ReflowLines))
                {
                    return Task.FromResult(true);
                }

                return scope is TextBufferCodeCleanUpScope textBufferScope
                    ? FixTextBufferAsync(textBufferScope, context)
                    : Task.FromResult(false);
            }

            private async Task<bool> FixTextBufferAsync(
                TextBufferCodeCleanUpScope scope,
                ICodeCleanUpExecutionContext context)
            {
                var cancellationToken = context.OperationContext.UserCancellationToken;
                await _provider._joinableTaskContext.Factory.SwitchToMainThreadAsync(cancellationToken);

                ITextBuffer subjectBuffer = scope.SubjectBuffer;
                if (subjectBuffer.EditInProgress)
                {
                    return false;
                }

                if (!_provider._textDocumentFactoryService.TryGetTextDocument(
                    subjectBuffer,
                    out ITextDocument textDocument) ||
                    string.IsNullOrEmpty(textDocument.FilePath))
                {
                    return true;
                }

                ReflowOptions? options = ReflowOptions.FromFile(
                    textDocument.FilePath,
                    subjectBuffer.CurrentSnapshot.GetText());
                if (options != null)
                {
                    _provider._reflowService.Reflow(
                        subjectBuffer,
                        options,
                        scope: null,
                        cancellationToken);
                }

                return true;
            }
        }
    }
}
