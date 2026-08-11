using System.ComponentModel.Composition;
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

[Export(typeof(AdornmentLayerDefinition))]
[Name(LayerName)]
[Order(Before = PredefinedAdornmentLayers.Text)]
[TextViewRole(PredefinedTextViewRoles.Document)]
public AdornmentLayerDefinition LayerDefinition = null!;

        public void TextViewCreated(IWpfTextView textView)
        {
            _ = new MaxLineLengthAdornment(textView);
        }
    }
}
