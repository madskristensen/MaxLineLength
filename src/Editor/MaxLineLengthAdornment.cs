using System.Windows.Shapes;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;

namespace MaxLineLength
{
    internal sealed class MaxLineLengthAdornment
    {
        private readonly IWpfTextView _view;
        private readonly IAdornmentLayer _layer;
        private readonly Line _ruler;
        private readonly ITextDocument? _document;
        private readonly EditorConfigRefreshCoordinator _refreshCoordinator;
        private readonly ToolkitThreadHelper _threadHelper;
        private readonly object _refreshGate = new();
        private int? _column;
        private bool _isAdded;
        private bool _isClosed;
        private bool _isRefreshRunning;
        private int _refreshVersion;
        private string? _pendingFilePath;

        public MaxLineLengthAdornment(
            IWpfTextView view,
            ITextDocument? document,
            EditorConfigRefreshCoordinator refreshCoordinator)
        {
            _view = view;
            _document = document;
            _refreshCoordinator = refreshCoordinator;
            _threadHelper = ToolkitThreadHelper.Create();
            _layer = view.GetAdornmentLayer(MaxLineLengthAdornmentFactory.LayerName);
            _ruler = new Line
            {
                IsHitTestVisible = false,
                Opacity = 0.35,
                SnapsToDevicePixels = true,
                StrokeThickness = 1,
            };

            _view.LayoutChanged += OnLayoutChanged;
            _view.Closed += OnViewClosed;

            RefreshColumn();
            UpdateRuler();
        }

        private void OnLayoutChanged(object sender, TextViewLayoutChangedEventArgs e)
        {
            try
            {
                UpdateRuler();
            }
            catch (Exception ex)
            {
                ex.Log();
            }
        }

        internal void RefreshFromEditorConfig()
        {
            try
            {
                bool startRefresh;
                lock (_refreshGate)
                {
                    if (_isClosed)
                    {
                        return;
                    }

                    _refreshVersion++;
                    _pendingFilePath = _document?.FilePath;
                    startRefresh = !_isRefreshRunning;
                    _isRefreshRunning = true;
                }

                if (startRefresh)
                {
                    _threadHelper.JoinableTaskFactory
                        .RunAsync(RefreshColumnFromFileAsync)
                        .FireAndForget();
                }
            }
            catch (Exception ex)
            {
                ex.Log();
            }
        }

        private void OnViewClosed(object sender, EventArgs e)
        {
            lock (_refreshGate)
            {
                _isClosed = true;
                _refreshVersion++;
            }

            _view.LayoutChanged -= OnLayoutChanged;
            _view.Closed -= OnViewClosed;
            _refreshCoordinator.Unregister(this);
            RemoveRuler();
            _threadHelper.Dispose();
        }

        private async Task RefreshColumnFromFileAsync()
        {
            while (true)
            {
                int refreshVersion;
                string? filePath;
                lock (_refreshGate)
                {
                    if (_isClosed)
                    {
                        _isRefreshRunning = false;
                        return;
                    }

                    refreshVersion = _refreshVersion;
                    filePath = _pendingFilePath;
                }

                try
                {
                    int? column = await Task.Run(() =>
                        filePath != null && filePath.Length > 0
                            ? MaxLineLengthSettings.GetMaxLineLength(filePath)
                            : null);

                    await _threadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    lock (_refreshGate)
                    {
                        if (_isClosed || _view.IsClosed)
                        {
                            _isRefreshRunning = false;
                            return;
                        }

                        if (refreshVersion != _refreshVersion)
                        {
                            continue;
                        }

                        _column = filePath != null && filePath.Length > 0
                            ? column
                            : MaxLineLengthSettings.GetMaxLineLength(_view);
                        MaxLineLengthSettings.SetResolvedMaxLineLength(_view, _column);
                        UpdateRuler();
                        _isRefreshRunning = false;
                        return;
                    }
                }
                catch (Exception ex)
                {
                    await ex.LogAsync();

                    lock (_refreshGate)
                    {
                        if (_isClosed || refreshVersion == _refreshVersion)
                        {
                            _isRefreshRunning = false;
                            return;
                        }
                    }
                }
            }
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
                _isAdded = _layer.AddAdornment(
                    AdornmentPositioningBehavior.OwnerControlled,
                    visualSpan: null,
                    tag: null,
                    adornment: _ruler,
                    removedCallback: (_, _) => _isAdded = false);
            }
        }

        private void RemoveRuler()
        {
            if (_isAdded)
            {
                _layer.RemoveAdornment(_ruler);
                _isAdded = false;
            }
        }
    }
}
