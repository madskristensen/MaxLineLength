using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Differencing;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;

namespace MaxLineLength
{
    [Export(typeof(IWpfTextViewCreationListener))]
    [ContentType("text")]
    [TextViewRole(PredefinedTextViewRoles.Document)]
    internal sealed class MaxLineLengthAdornmentFactory : IWpfTextViewCreationListener
    {
        internal const string LayerName = "MaxLineLengthAdornment";

        private readonly ITextDocumentFactoryService _documentFactory;
        private readonly IEditorFormatMapService _formatMapService;
        private readonly EditorConfigRefreshCoordinator _refreshCoordinator;

        [ImportingConstructor]
        public MaxLineLengthAdornmentFactory(
            ITextDocumentFactoryService documentFactory,
            IEditorFormatMapService formatMapService,
            EditorConfigRefreshCoordinator refreshCoordinator)
        {
            _documentFactory = documentFactory;
            _formatMapService = formatMapService;
            _refreshCoordinator = refreshCoordinator;
        }

        [Export(typeof(AdornmentLayerDefinition))]
        [Name(LayerName)]
        [Order(Before = PredefinedAdornmentLayers.Text)]
        [TextViewRole(PredefinedTextViewRoles.Document)]
        public AdornmentLayerDefinition LayerDefinition = null!;

        public void TextViewCreated(IWpfTextView textView)
        {
            if (textView.Roles.Contains(DifferenceViewerRoles.DiffTextViewRole))
            {
                return;
            }

            ITextDocument? document = _documentFactory.TryGetTextDocument(
                textView.TextBuffer,
                out ITextDocument textDocument)
                ? textDocument
                : null;

            var adornment = new MaxLineLengthAdornment(
                textView,
                document,
                _formatMapService.GetEditorFormatMap(textView),
                _refreshCoordinator);
            _refreshCoordinator.Register(adornment, document);
        }
    }
}
