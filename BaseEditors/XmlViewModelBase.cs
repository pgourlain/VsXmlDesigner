using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;
using Genius.VisualStudio.BaseEditors.Properties;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Package;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.XmlEditor;
using IOleServiceProvider = Microsoft.VisualStudio.OLE.Interop.IServiceProvider;

namespace Genius.VisualStudio.BaseEditors
{

    /// <summary>
    /// ViewModel base class using by <see cref="XmlDesignerSurfaceBase"/>  
    /// </summary>
    /// <remarks>
    /// this class provide you stuff about loading/saving <see cref="TModel"/> using VisualStudio services
    /// </remarks>
    /// <typeparam name="TModel">your model that support XmlSerialize/XmlDeserialize</typeparam>
    public class XmlViewModelBase<TModel> : IXmlViewModel, INotifyPropertyChanged
        where TModel : new()
    {
        XmlModel _xmlModel;
        XmlStore _xmlStore;

        IServiceProvider _serviceProvider;
        IVsTextLines _buffer;

        bool _synchronizing;
        long _dirtyTime;
        EventHandler<XmlEditingScopeEventArgs> _editingScopeCompletedHandler;
        EventHandler<XmlEditingScopeEventArgs> _undoRedoCompletedHandler;
        EventHandler _bufferReloadedHandler;

        LanguageService _xmlLanguageService;

        TModel _Model;

        /// <summary>
        /// Model property to use in WPF view
        /// </summary>
        /// <returns></returns>
        public TModel Model
        {
            get
            {
                return _Model;
            }
        }

        DynamicProxyIPC _ProxiedModel;
        /// <summary>
        /// return a proxy around your model that supports INotifyPropertyChanged on all public propoerties, use this poperty in your view if your model doesn't support this interface
        /// </summary>
        public dynamic ProxiedModel
        {
            get
            {
                return _ProxiedModel;
            }
        }


        public XmlViewModelBase(XmlStore xmlStore, XmlModel xmlModel, IServiceProvider provider, IVsTextLines buffer)
        {
            if (xmlModel == null)
                throw new ArgumentNullException("xmlModel");
            if (xmlStore == null)
                throw new ArgumentNullException("xmlStore");
            if (provider == null)
                throw new ArgumentNullException("provider");
            if (buffer == null)
                throw new ArgumentNullException("buffer");

            this.BufferDirty = false;
            this.DesignerDirty = false;

            this._serviceProvider = provider;
            this._buffer = buffer;

            this._xmlStore = xmlStore;
            // OnUnderlyingEditCompleted
            _editingScopeCompletedHandler = new EventHandler<XmlEditingScopeEventArgs>(OnUnderlyingEditCompleted);
            this._xmlStore.EditingScopeCompleted += _editingScopeCompletedHandler;
            // OnUndoRedoCompleted
            _undoRedoCompletedHandler = new EventHandler<XmlEditingScopeEventArgs>(OnUndoRedoCompleted);
            this._xmlStore.UndoRedoCompleted += _undoRedoCompletedHandler;

            this._xmlModel = xmlModel;
            // BufferReloaded
            _bufferReloadedHandler += new EventHandler(BufferReloaded);
            this._xmlModel.BufferReloaded += _bufferReloadedHandler;

            LoadModelFromXmlModel();
        }

        public void Close()
        {
            //Unhook the events from the underlying XmlStore/XmlModel
            if (_xmlStore != null)
            {
                this._xmlStore.EditingScopeCompleted -= _editingScopeCompletedHandler;
                this._xmlStore.UndoRedoCompleted -= _undoRedoCompletedHandler;
            }
            if (this._xmlModel != null)
            {
                this._xmlModel.BufferReloaded -= _bufferReloadedHandler;
            }
        }

        /// <summary>
        /// Property indicating if there is a pending change in the underlying text buffer
        /// that needs to be reflected in the ViewModel.
        /// 
        /// Used by DoIdle to determine if we need to sync.
        /// </summary>
        bool BufferDirty
        {
            get;
            set;
        }

        /// <summary>
        /// Property indicating if there is a pending change in the ViewModel that needs to 
        /// be committed to the underlying text buffer.
        /// 
        /// Used by DoIdle to determine if we need to sync.
        /// </summary>
        public bool DesignerDirty
        {
            get;
            set;
        }

        /// <summary>
        /// We must not try and update the XDocument while the XML Editor is parsing as this may cause
        /// a deadlock in the XML Editor!
        /// </summary>
        /// <returns></returns>
        bool IsXmlEditorParsing
        {
            get
            {
                LanguageService langsvc = GetXmlLanguageService();
                return langsvc != null ? langsvc.IsParsing : false;
            }
        }

        /// <summary>
        /// Called on idle time. This is when we check if the designer is out of sync with the underlying text buffer.
        /// </summary>
        public void DoIdle()
        {
            if (BufferDirty || DesignerDirty)
            {
                int delay = 100;

                if ((Environment.TickCount - _dirtyTime) > delay)
                {
                    // Must not try and sync while XML editor is parsing otherwise we just confuse matters.
                    if (IsXmlEditorParsing)
                    {
                        _dirtyTime = System.Environment.TickCount;
                        return;
                    }

                    //If there is contention, give the preference to the designer.
                    if (DesignerDirty)
                    {
                        SaveModelToXmlModel(Resources.SynchronizeBuffer);
                        //We don't do any merging, so just overwrite whatever was in the buffer.
                        BufferDirty = false;
                    }
                    else if (BufferDirty)
                    {
                        LoadModelFromXmlModel();
                    }
                }
            }
        }

        /// <summary>
        /// Load the model from the underlying text buffer.
        /// </summary>
        private void LoadModelFromXmlModel()
        {
            Debug.WriteLine("enter LoadModelFromXmlModel");
            UnregisterFromModel();
            try
            {
                using (XmlReader reader = GetParseTree().CreateReader())
                {
                    _Model = ParseXml(reader);
                    _ProxiedModel = new DynamicProxyIPC(_Model);
                }
                if (_Model == null)
                {
                    throw new Exception(Resources.InvalidXmlData);
                }
            }
            catch (Exception e)
            {
                //Display error message
                ErrorHandler.ThrowOnFailure(VsShellUtilities.ShowMessageBox(_serviceProvider,
                    Resources.InvalidXmlData + e.Message,
                    Resources.ErrorMessageBoxTitle,
                    OLEMSGICON.OLEMSGICON_CRITICAL,
                    OLEMSGBUTTON.OLEMSGBUTTON_OK,
                    OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST));
            }

            BufferDirty = false;
            RegisterFromModel();

            NotifyPropertyChanged(string.Empty);
            if (ViewModelChanged != null)
            {
                // Update the Designer View
                ViewModelChanged(this, new EventArgs());
            }
            
            Debug.WriteLine("leave LoadModelFromXmlModel");
        }

        private void RegisterFromModel()
        {
            if (_Model != null)
            {
                INotifyPropertyChanged npc = _Model as INotifyPropertyChanged;
                if (npc != null)
                {
                    npc.PropertyChanged += Model_PropertyChanged;
                }
            }
            if (_ProxiedModel != null)
            {
                _ProxiedModel.OnAnyChanges += OnAnyChanges;
            }
        }

        private void UnregisterFromModel()
        {
            if (_Model != null)
            {
                INotifyPropertyChanged npc = _Model as INotifyPropertyChanged;
                if (npc != null)
                {
                    npc.PropertyChanged -= Model_PropertyChanged;
                }
            }

            if (_ProxiedModel != null)
            {
                _ProxiedModel.OnAnyChanges -= OnAnyChanges;
            }
        }

        private void OnAnyChanges()
        {
            Debug.WriteLine("any changes received");
            DesignerDirty = true;
        }
        private void Model_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            DesignerDirty = true;
        }

        protected virtual TModel ParseXml(XmlReader reader)
        {
            XmlSerializer serializer = new XmlSerializer(typeof(TModel));

            var model = (TModel)serializer.Deserialize(reader);
            if (model == null)
                model = new TModel();
            return model;
        }



        /// <summary>
        /// Get an up to date XML parse tree from the XML Editor.
        /// </summary>
        XDocument GetParseTree()
        {
            LanguageService langsvc = this.GetXmlLanguageService();

            // don't crash if the language service is not available
            if (langsvc != null)
            {
                Source src = langsvc.GetSource(_buffer);

                // We need to access this method to get the most up to date parse tree.
                // public virtual XmlDocument GetParseTree(Source source, IVsTextView view, int line, int col, ParseReason reason) {
                MethodInfo mi = langsvc.GetType().GetMethod("GetParseTree");
                int line = 0, col = 0;
                mi.Invoke(langsvc, new object[] { src, null, line, col, ParseReason.Check });
            }

            // Now the XmlDocument should be up to date also.
            return _xmlModel.Document;
        }

        /// <summary>
        /// Get the XML Editor language service
        /// </summary>
        /// <returns></returns>
        LanguageService GetXmlLanguageService()
        {
            if (_xmlLanguageService == null)
            {
                _xmlLanguageService = _serviceProvider.XmlLanguageService();
            }
            return _xmlLanguageService;
        }

        /// <summary>
        /// Reformat the text buffer
        /// </summary>
        void FormatBuffer(Source src)
        {
            using (EditArray edits = new EditArray(src, null, false, Resources.ReformatBuffer))
            {
                TextSpan span = src.GetDocumentSpan();
                src.ReformatSpan(edits, span);
            }
        }

        /// <summary>
        /// Get the XML Editor Source object for this document.
        /// </summary>
        /// <returns></returns>
        Source GetSource()
        {
            LanguageService langsvc = GetXmlLanguageService();
            if (langsvc == null)
            {
                return null;
            }
            Source src = langsvc.GetSource(_buffer);
            return src;
        }

        /// <summary>
        /// This method is called when it is time to save the designer values to the
        /// underlying buffer.
        /// </summary>
        /// <param name="undoEntry"></param>
        void SaveModelToXmlModel(string undoEntry)
        {
            LanguageService langsvc = GetXmlLanguageService();

            try
            {
                //We can't edit this file (perhaps the user cancelled a SCC prompt, etc...)
                if (!CanEditFile())
                {
                    DesignerDirty = false;
                    BufferDirty = true;
                    throw new Exception();
                }

                XDocument documentFromDesignerState = new XDocument();
                using (XmlWriter w = documentFromDesignerState.CreateWriter())
                {
                    SaveXml(w);
                }

                _synchronizing = true;
                XDocument document = GetParseTree();
                Source src = GetSource();
                if (src == null || langsvc == null)
                {
                    return;
                }

                langsvc.IsParsing = true; // lock out the background parse thread.

                // Wrap the buffer sync and the formatting in one undo unit.
                using (CompoundAction ca = new CompoundAction(src, Resources.SynchronizeBuffer))
                {
                    using (XmlEditingScope scope = _xmlStore.BeginEditingScope(Resources.SynchronizeBuffer, this))
                    {
                        //Replace the existing XDocument with the new one we just generated.
                        document.Root.ReplaceWith(documentFromDesignerState.Root);
                        scope.Complete();
                    }
                    ca.FlushEditActions();
                    FormatBuffer(src);
                }
                DesignerDirty = false;
            }
            catch (Exception)
            {
                // if the synchronization fails then we'll just try again in a second.
                _dirtyTime = Environment.TickCount;
            }
            finally
            {
                langsvc.IsParsing = false;
                _synchronizing = false;
            }
        }

        protected virtual void SaveXml(XmlWriter w)
        {
            XmlSerializer serializer = new XmlSerializer(typeof(TModel));
            serializer.Serialize(w, _Model);
        }


        /// <summary>
        /// Fired when all controls should be re-bound.
        /// </summary>
        public event EventHandler ViewModelChanged;

        private void BufferReloaded(object sender, EventArgs e)
        {
            if (!_synchronizing)
            {
                BufferDirty = true;
                _dirtyTime = Environment.TickCount;
            }
        }

        /// <summary>
        /// Handle undo/redo completion event.  This happens when the user invokes Undo/Redo on a buffer edit operation.
        /// We need to resync when this happens.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void OnUndoRedoCompleted(object sender, XmlEditingScopeEventArgs e)
        {
            if (!_synchronizing)
            {
                BufferDirty = true;
                _dirtyTime = Environment.TickCount;
            }
        }

        /// <summary>
        /// Handle edit scope completion event.  This happens when the XML editor buffer decides to update
        /// it's XDocument parse tree.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void OnUnderlyingEditCompleted(object sender, XmlEditingScopeEventArgs e)
        {
            if (e.EditingScope.UserState != this && !_synchronizing)
            {
                BufferDirty = true;
                _dirtyTime = Environment.TickCount;
            }
        }

        #region Source Control

        bool? _canEditFile;
        bool _gettingCheckoutStatus;

        /// <summary>
        /// This function asks the QueryEditQuerySave service if it is possible to edit the file.
        /// This can result in an automatic checkout of the file and may even prompt the user for
        /// permission to checkout the file.  If the user says no or the file cannot be edited 
        /// this returns false.
        /// </summary>
        private bool CanEditFile()
        {
            // Cache the value so we don't keep asking the user over and over.
            if (_canEditFile.HasValue)
            {
                return (bool)_canEditFile;
            }

            // Check the status of the recursion guard
            if (_gettingCheckoutStatus)
                return false;

            _canEditFile = false; // assume the worst
            try
            {
                // Set the recursion guard
                _gettingCheckoutStatus = true;

                // Get the QueryEditQuerySave service
                IVsQueryEditQuerySave2 queryEditQuerySave = _serviceProvider.GetService(typeof(SVsQueryEditQuerySave)) as IVsQueryEditQuerySave2;

                string filename = _xmlModel.Name;

                // Now call the QueryEdit method to find the edit status of this file
                string[] documents = { filename };
                uint result;
                uint outFlags;

                // Note that this function can popup a dialog to ask the user to checkout the file.
                // When this dialog is visible, it is possible to receive other request to change
                // the file and this is the reason for the recursion guard
                int hr = queryEditQuerySave.QueryEditFiles(
                    0,              // Flags
                    1,              // Number of elements in the array
                    documents,      // Files to edit
                    null,           // Input flags
                    null,           // Input array of VSQEQS_FILE_ATTRIBUTE_DATA
                    out result,     // result of the checkout
                    out outFlags    // Additional flags
                );
                if (ErrorHandler.Succeeded(hr) && (result == (uint)tagVSQueryEditResult.QER_EditOK))
                {
                    // In this case (and only in this case) we can return true from this function
                    _canEditFile = true;
                }
            }
            finally
            {
                _gettingCheckoutStatus = false;
            }
            return (bool)_canEditFile;
        }

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;

        protected void NotifyPropertyChanged(string propertyName)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        #endregion

        #region TreeView SelectionChanged

        private ITrackSelection trackSel;
        private ITrackSelection TrackSelection
        {
            get
            {
                if (trackSel == null)
                    trackSel = _serviceProvider.GetService(typeof(STrackSelection)) as ITrackSelection;
                return trackSel;
            }
        }

        private Microsoft.VisualStudio.Shell.SelectionContainer selContainer;
        public void OnSelectChanged(object p)
        {
            selContainer = new Microsoft.VisualStudio.Shell.SelectionContainer(true, false);
            ArrayList items = new ArrayList();
            items.Add(p);
            selContainer.SelectableObjects = items;
            selContainer.SelectedObjects = items;

            ITrackSelection track = TrackSelection;
            if (track != null)
                track.OnSelectChange((ISelectionContainer)selContainer);
        }

        #endregion

        public void UnderlyingFileChanged()
        {
            //this.LoadModelFromXmlModel();
        }
    }
}
