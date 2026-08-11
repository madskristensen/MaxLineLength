using System.Windows.Shapes;
using Microsoft.VisualStudio.Text.Editor;

namespace MaxLineLength
{
    internal sealed class MaxLineLengthAdornment
    {
        private const string CodingConventionsOptionName = "CodingConventionsSnapshot";

        private readonly IWpfTextView _view;
        private readonly IAdornmentLayer _layer;
        private readonly Line _ruler;
        private int? _column;
        private bool _isAdded;

        public MaxLineLengthAdornment(IWpfTextView view)
        {
            _view = view;
            _layer = view.GetAdornmentLayer(MaxLineLengthAdornmentFactory.LayerName);
            _ruler = new Line
            {
                IsHitTestVisible = false,
                Opacity = 0.35,
                SnapsToDevicePixels = true,
                StrokeThickness = 1,
            };

            _view.LayoutChanged += OnLayoutChanged;
            _view.Options.OptionChanged += OnOptionChanged;
            _view.Closed += OnViewClosed;

            RefreshColumn();
            UpdateRuler();
        }

        private void OnLayoutChanged(object sender, TextViewLayoutChangedEventArgs e)
        {
            UpdateRuler();
        }

        private void OnOptionChanged(object sender, EditorOptionChangedEventArgs e)
        {
            if (e.OptionId == CodingConventionsOptionName)
            {
                RefreshColumn();
                UpdateRuler();
            }
        }

        private void OnViewClosed(object sender, EventArgs e)
        {
            _view.LayoutChanged -= OnLayoutChanged;
            _view.Options.OptionChanged -= OnOptionChanged;
            _view.Closed -= OnViewClosed;
            RemoveRuler();
        }

        private void RefreshColumn()
        {
            _column = MaxLineLengthSettings.GetMaxLineLength(_view);
        }

        private void UpdateRuler()
        {
            if (!_column.HasValue)
            {
                RemoveRuler();
                return;
            }

            var lineSource = _view.FormattedLineSource;
            if (lineSource == null)
            {
                return;
            }

            _ruler.Stroke = lineSource.DefaultTextProperties.ForegroundBrush;
            _ruler.X1 = _ruler.X2 = lineSource.BaseIndentation + 0.5 + (_column.Value * lineSource.ColumnWidth);
            _ruler.Y1 = _view.ViewportTop;
            _ruler.Y2 = _view.ViewportBottom;

            if (!_isAdded)
            {
                _layer.AddAdornment(
                    AdornmentPositioningBehavior.OwnerControlled,
                    visualSpan: null,
                    tag: null,
                    adornment: _ruler,
                    removedCallback: null);
                _isAdded = true;
            }
        }

        private void RemoveRuler()
        {
            if (_isAdded)
            {
                _layer.RemoveAllAdornments();
                _isAdded = false;
            }
        }
    }
}
