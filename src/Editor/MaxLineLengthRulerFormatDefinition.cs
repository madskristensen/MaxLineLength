using System.ComponentModel.Composition;
using System.Windows;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;

namespace MaxLineLength
{
    [Export(typeof(EditorFormatDefinition))]
    [Name(FormatName)]
    [UserVisible(true)]
    internal sealed class MaxLineLengthRulerFormatDefinition : EditorFormatDefinition
    {
        public const string FormatName = "MaxLineLength/RulerForeground";

        public MaxLineLengthRulerFormatDefinition()
        {
            DisplayName = "Max Line Length ruler";
            ForegroundColor = SystemColors.GrayTextColor;
            ForegroundCustomizable = true;
            BackgroundCustomizable = false;
        }
    }
}
