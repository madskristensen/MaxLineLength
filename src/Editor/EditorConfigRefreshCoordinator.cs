using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.Text;

namespace MaxLineLength
{
    [Export]
    [PartCreationPolicy(CreationPolicy.Shared)]
    internal sealed class EditorConfigRefreshCoordinator : IDisposable
    {
        private readonly ITextDocumentFactoryService _documentFactory;
        private readonly object _gate = new();
        private readonly HashSet<MaxLineLengthAdornment> _adornments = new();
        private readonly HashSet<ITextDocument> _documents = new();
        private bool _isDisposed;

        [ImportingConstructor]
        public EditorConfigRefreshCoordinator(ITextDocumentFactoryService documentFactory)
        {
            _documentFactory = documentFactory;
            _documentFactory.TextDocumentCreated += OnTextDocumentCreated;
            _documentFactory.TextDocumentDisposed += OnTextDocumentDisposed;
        }

        public void Register(MaxLineLengthAdornment adornment, ITextDocument? document)
        {
            lock (_gate)
            {
                if (_isDisposed)
                {
                    return;
                }

                _adornments.Add(adornment);
            }

            if (document != null)
            {
                TrackDocument(document);
            }

            adornment.RefreshFromEditorConfig();
        }

        public void Unregister(MaxLineLengthAdornment adornment)
        {
            lock (_gate)
            {
                _adornments.Remove(adornment);
            }
        }

        public void Dispose()
        {
            _documentFactory.TextDocumentCreated -= OnTextDocumentCreated;
            _documentFactory.TextDocumentDisposed -= OnTextDocumentDisposed;

            ITextDocument[] documents;
            lock (_gate)
            {
                _isDisposed = true;
                documents = _documents.ToArray();
                _documents.Clear();
                _adornments.Clear();
            }

            foreach (ITextDocument document in documents)
            {
                document.FileActionOccurred -= OnFileActionOccurred;
            }
        }

        internal static bool IsEditorConfigFile(string filePath)
        {
            return string.Equals(
                Path.GetFileName(filePath),
                ".editorconfig",
                StringComparison.OrdinalIgnoreCase);
        }

        private void OnTextDocumentCreated(object sender, TextDocumentEventArgs e)
        {
            try
            {
                TrackDocument(e.TextDocument);
            }
            catch (Exception ex)
            {
                ex.Log();
            }
        }

        private void OnTextDocumentDisposed(object sender, TextDocumentEventArgs e)
        {
            try
            {
                UntrackDocument(e.TextDocument);
            }
            catch (Exception ex)
            {
                ex.Log();
            }
        }

        private void OnFileActionOccurred(object sender, TextDocumentFileActionEventArgs e)
        {
            if ((e.FileActionType & FileActionTypes.ContentSavedToDisk) == 0 ||
                sender is not ITextDocument document ||
                !IsEditorConfigFile(document.FilePath))
            {
                return;
            }

            try
            {
                MaxLineLengthAdornment[] adornments;
                lock (_gate)
                {
                    adornments = _adornments.ToArray();
                }

                foreach (MaxLineLengthAdornment adornment in adornments)
                {
                    adornment.RefreshFromEditorConfig();
                }
            }
            catch (Exception ex)
            {
                ex.Log();
            }
        }

        private void TrackDocument(ITextDocument document)
        {
            lock (_gate)
            {
                if (_isDisposed || _documents.Contains(document))
                {
                    return;
                }

                document.FileActionOccurred += OnFileActionOccurred;
                _documents.Add(document);
            }
        }

        private void UntrackDocument(ITextDocument document)
        {
            lock (_gate)
            {
                if (!_documents.Contains(document))
                {
                    return;
                }

                document.FileActionOccurred -= OnFileActionOccurred;
                _documents.Remove(document);
            }
        }
    }
}
