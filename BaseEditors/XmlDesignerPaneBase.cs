using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Permissions;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.XmlEditor;

namespace Genius.VisualStudio.BaseEditors
{

    /// <summary>
    /// base windowpane to edit an Xml in your specific designer
    /// </summary>
    /// <typeparam name="TDesignerControl">Type</typeparam>
    /// <typeparam name="TPackage"></typeparam>
    [ComVisible(true)]
    public class XmlDesignerPaneBase<TDesignerControl, TViewModel> :  Microsoft.VisualStudio.Shell.WindowPane, 
        IOleComponent, 
        IVsDeferredDocView, 
        IVsLinkedUndoClient,
        IVsFileChangeEvents,
        IVsDocDataFileChangeControl,
        IXmlDesignerPane
        where TDesignerControl : XmlDesignerSurfaceBase
    {
        private string _fileName;
        protected Package _package;
        private IVsTextLines _textBuffer;
        private IOleUndoManager _undoManager;
        private XmlStore _store;
        private XmlModel _model;
        private uint _componentId;
        private TDesignerControl _designerControl;
        Guid _guidEditorFactory;
        IVsFileChangeEx vsFileChangeEx;
        private uint vsFileChangeCookie;


        public XmlDesignerPaneBase(Package package, string fileName, IVsTextLines textBuffer, Guid guidEditorFactory)
        {
            this._fileName = fileName;
            this._package = package;
            this._textBuffer = textBuffer;
            this._guidEditorFactory = guidEditorFactory;
        }

        public string FileName
        {
            get { return _fileName; }
        }

        #region private
        #region unused methods
        /*
        /// <summary>
        /// Gets an instance of the RunningDocumentTable (RDT) service which manages the set of currently open 
        /// documents in the environment and then notifies the client that an open document has changed
        /// </summary>
        private void NotifyDocChanged()
        {
            // Make sure that we have a file name
            if (_fileName.Length == 0)
                return;

            // Get a reference to the Running Document Table
            IVsRunningDocumentTable runningDocTable = (IVsRunningDocumentTable)GetService(typeof(SVsRunningDocumentTable));

            // Lock the document
            uint docCookie;
            IVsHierarchy hierarchy;
            uint itemID;
            IntPtr docData;
            int hr = runningDocTable.FindAndLockDocument(
                (uint)_VSRDTFLAGS.RDT_ReadLock,
                _fileName,
                out hierarchy,
                out itemID,
                out docData,
                out docCookie
            );
            ErrorHandler.ThrowOnFailure(hr);

            // Send the notification
            hr = runningDocTable.NotifyDocumentChanged(docCookie, (uint)__VSRDTATTRIB.RDTA_DocDataReloaded);

            // Unlock the document.
            // Note that we have to unlock the document even if the previous call failed.
            ErrorHandler.ThrowOnFailure(runningDocTable.UnlockDocument((uint)_VSRDTFLAGS.RDT_ReadLock, docCookie));

            // Check ff the call to NotifyDocChanged failed.
            ErrorHandler.ThrowOnFailure(hr);
        }
        */
        #endregion

        /// <summary>
        /// Registers an independent view with the IVsTextManager so that it knows
        /// the user is working with a view over the text buffer. This will trigger
        /// the text buffer to prompt the user whether to reload the file if it is
        /// edited outside of the environment.
        /// </summary>
        /// <param name="subscribe">True to subscribe, false to unsubscribe</param>
        void RegisterIndependentView(bool subscribe)
        {
            IVsTextManager textManager = (IVsTextManager)GetService(typeof(SVsTextManager));

            if (textManager != null)
            {
                if (subscribe)
                {
                    textManager.RegisterIndependentView((IVsWindowPane)this, this._textBuffer);
                }
                else
                {
                    textManager.UnregisterIndependentView((IVsWindowPane)this, this._textBuffer);
                }
            }
        }
        #endregion

        #region overrides
        protected override void OnClose()
        {
            // unhook from Undo related services
            if (_undoManager != null)
            {
                IVsLinkCapableUndoManager linkCapableUndoMgr = (IVsLinkCapableUndoManager)_undoManager;
                if (linkCapableUndoMgr != null)
                {
                    linkCapableUndoMgr.UnadviseLinkedUndoClient();
                }

                // Throw away the undo stack etc.
                // It is important to â€œzombifyâ€ the undo manager when the owning object is shutting down.
                // This is done by calling IVsLifetimeControlledObject.SeverReferencesToOwner on the undoManager.
                // This call will clear the undo and redo stacks. This is particularly important to do if
                // your undo units hold references back to your object. It is also important if you use
                // "mdtStrict" linked undo transactions as this sample does (see IVsLinkedUndoTransactionManager). 
                // When one object involved in linked undo transactions clears its undo/redo stacks, then 
                // the stacks of the other documents involved in the linked transaction will also be cleared. 
                IVsLifetimeControlledObject lco = (IVsLifetimeControlledObject)_undoManager;
                lco.SeverReferencesToOwner();
                _undoManager = null;
            }

            IOleComponentManager mgr = GetService(typeof(SOleComponentManager)) as IOleComponentManager;
            mgr.FRevokeComponent(_componentId);

            this.Dispose(true);

            base.OnClose();
        }

        protected override void Initialize()
        {
            base.Initialize();

            // Create and initialize the editor
            #region Register with IOleComponentManager
            IOleComponentManager componentManager = (IOleComponentManager)GetService(typeof(SOleComponentManager));
            if (this._componentId == 0 && componentManager != null)
            {
                OLECRINFO[] crinfo = new OLECRINFO[1];
                crinfo[0].cbSize = (uint)Marshal.SizeOf(typeof(OLECRINFO));
                crinfo[0].grfcrf = (uint)_OLECRF.olecrfNeedIdleTime | (uint)_OLECRF.olecrfNeedPeriodicIdleTime;
                crinfo[0].grfcadvf = (uint)_OLECADVF.olecadvfModal | (uint)_OLECADVF.olecadvfRedrawOff | (uint)_OLECADVF.olecadvfWarningsOff;
                crinfo[0].uIdleTimeInterval = 100;
                int hr = componentManager.FRegisterComponent(this, crinfo, out this._componentId);
                ErrorHandler.Succeeded(hr);
            }
            #endregion

            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(this.GetType());

            #region Hook Undo Manager
            // Attach an IOleUndoManager to our WindowFrame. Merely calling QueryService 
            // for the IOleUndoManager on the site of our IVsWindowPane causes an IOleUndoManager
            // to be created and attached to the IVsWindowFrame. The WindowFrame automaticall 
            // manages to route the undo related commands to the IOleUndoManager object.
            // Thus, our only responsibilty after this point is to add IOleUndoUnits to the 
            // IOleUndoManager (aka undo stack).
            _undoManager = (IOleUndoManager)GetService(typeof(SOleUndoManager));

            // In order to use the IVsLinkedUndoTransactionManager, it is required that you
            // advise for IVsLinkedUndoClient notifications. This gives you a callback at 
            // a point when there are intervening undos that are blocking a linked undo.
            // You are expected to activate your document window that has the intervening undos.
            if (_undoManager != null)
            {
                IVsLinkCapableUndoManager linkCapableUndoMgr = (IVsLinkCapableUndoManager)_undoManager;
                if (linkCapableUndoMgr != null)
                {
                    linkCapableUndoMgr.AdviseLinkedUndoClient(this);
                }
            }
            #endregion

            var xmlSvc = this.XmlLanguageService();
            //stop parsing if running to avoid deadlock on OpenXmlModel
            if (xmlSvc.IsParsing)
            {
                if (!xmlSvc.IsMainThread)
                {
                    //abort background if running 
                    xmlSvc.AbortBackgroundParse();
                }
                xmlSvc.IsParsing = false;
                xmlSvc.WaitForParse();
            }
            // hook up our 
            XmlEditorService es = GetService(typeof(XmlEditorService)) as XmlEditorService;
            _store = es.CreateXmlStore();
            _store.UndoManager = _undoManager;

            _model = _store.OpenXmlModel(new Uri(_fileName));

            // This is the user control hosted by the tool window; Note that, even if this class implements IDisposable,
            // we are not calling Dispose on this object. This is because ToolWindowPane calls Dispose on 
            // the object returned by the Content property.
            _designerControl = CreateDesigner(_store, _model, _textBuffer);//,  new VsDesignerControl(new ViewModel(_store, _model, this, _textBuffer));
            base.Content = _designerControl;

            RegisterIndependentView(true);

            IMenuCommandService mcs = GetService(typeof(IMenuCommandService)) as IMenuCommandService;
            if (null != mcs)
            {
                // Now create one object derived from MenuCommnad for each command defined in
                // the CTC file and add it to the command service.

                // For each command we have to define its id that is a unique Guid/integer pair, then
                // create the OleMenuCommand object for this command. The EventHandler object is the
                // function that will be called when the user will select the command. Then we add the 
                // OleMenuCommand to the menu service.  The addCommand helper function does all this for us.
                mcs.AddCommand(VSConstants.GUID_VSStandardCommandSet97, (int)VSConstants.VSStd97CmdID.NewWindow,
                                new EventHandler(OnNewWindow), new EventHandler(OnQueryNewWindow));
                mcs.AddCommand(VSConstants.GUID_VSStandardCommandSet97, (int)VSConstants.VSStd97CmdID.ViewCode,
                                new EventHandler(OnViewCode), new EventHandler(OnQueryViewCode));
            }
            SetFileChangeNotification(_fileName, true);
        }

        private int SetFileChangeNotification(string pszFileName, bool fStart)
        {
            Debug.WriteLine(string.Format(CultureInfo.CurrentCulture, "\t **** Inside SetFileChangeNotification ****"));

            int result = VSConstants.E_FAIL;

            //Get the File Change service
            if (null == vsFileChangeEx)
                vsFileChangeEx = (IVsFileChangeEx)GetService(typeof(SVsFileChangeEx));
            if (null == vsFileChangeEx)
                return VSConstants.E_UNEXPECTED;

            // Setup Notification if fStart is TRUE, Remove if fStart is FALSE.
            if (fStart)
            {
                if (vsFileChangeCookie == VSConstants.VSCOOKIE_NIL)
                {
                    //Receive notifications if either the attributes of the file change or 
                    //if the size of the file changes or if the last modified time of the file changes
                    result = vsFileChangeEx.AdviseFileChange(pszFileName,
                        (uint)(_VSFILECHANGEFLAGS.VSFILECHG_Attr | _VSFILECHANGEFLAGS.VSFILECHG_Size | _VSFILECHANGEFLAGS.VSFILECHG_Time),
                        (IVsFileChangeEvents)this,
                        out vsFileChangeCookie);
                    if (vsFileChangeCookie == VSConstants.VSCOOKIE_NIL)
                        return VSConstants.E_FAIL;
                }
            }
            else
            {
                if (vsFileChangeCookie != VSConstants.VSCOOKIE_NIL)
                {
                    result = vsFileChangeEx.UnadviseFileChange(vsFileChangeCookie);
                    vsFileChangeCookie = VSConstants.VSCOOKIE_NIL;
                }
            }
            return result;
        }

        protected virtual TDesignerControl CreateDesigner(XmlStore _store, XmlModel _model, IVsTextLines _textBuffer)
        {
            IXmlViewModel viewModel = (IXmlViewModel)Activator.CreateInstance(typeof(TViewModel), _store, _model, (System.IServiceProvider)this, _textBuffer);

            var designer = (TDesignerControl)Activator.CreateInstance(typeof(TDesignerControl), viewModel);
            return designer;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                RegisterIndependentView(false);
                SetFileChangeNotification(null, false);
                using (_model)
                {
                    _model = null;
                }
                using (_store)
                {
                    _store = null;
                }
            }
            base.Dispose(disposing);
        }
        #endregion

        #region Commands

        private void OnQueryNewWindow(object sender, EventArgs e)
        {
            OleMenuCommand command = (OleMenuCommand)sender;
            command.Enabled = true;
        }

        private void OnNewWindow(object sender, EventArgs e)
        {
            NewWindow();
        }

        private void OnQueryViewCode(object sender, EventArgs e)
        {
            OleMenuCommand command = (OleMenuCommand)sender;
            command.Enabled = true;
        }

        private void OnViewCode(object sender, EventArgs e)
        {
            ViewCode();
        }

        private void NewWindow()
        {
            int hr = VSConstants.S_OK;

            IVsUIShellOpenDocument uishellOpenDocument = (IVsUIShellOpenDocument)GetService(typeof(SVsUIShellOpenDocument));
            if (uishellOpenDocument != null)
            {
                IVsWindowFrame windowFrameOrig = (IVsWindowFrame)GetService(typeof(SVsWindowFrame));
                if (windowFrameOrig != null)
                {
                    IVsWindowFrame windowFrameNew;
                    Guid LOGVIEWID_Primary = Guid.Empty;
                    hr = uishellOpenDocument.OpenCopyOfStandardEditor(windowFrameOrig, ref LOGVIEWID_Primary, out windowFrameNew);
                    if (windowFrameNew != null)
                        hr = windowFrameNew.Show();
                    ErrorHandler.ThrowOnFailure(hr);
                }
            }
        }

        private void ViewCode()
        {
            Guid XmlTextEditorGuid = new Guid("FA3CD31E-987B-443A-9B81-186104E8DAC1");

            // Open the referenced document using our editor.
            IVsWindowFrame frame;
            IVsUIHierarchy hierarchy;
            uint itemid;
            VsShellUtilities.OpenDocumentWithSpecificEditor(this, _model.Name,
                XmlTextEditorGuid, VSConstants.LOGVIEWID_Primary, out hierarchy, out itemid, out frame);
            ErrorHandler.ThrowOnFailure(frame.Show());
            //these lines open designer
            //ServicesExtensions.XmlLanguageService(this).OpenXmlEditor(_textBuffer, _model.Name, new TextSpan(), "tagada", false);
            //ServicesExtensions.XmlLanguageService(this).OpenDocument(_model.Name);
        }

        #endregion

        #region IVsLinkedUndoClient

        public int OnInterveningUnitBlockingLinkedUndo()
        {
            return VSConstants.E_FAIL;
        }

        #endregion

        #region IVsDeferredDocView

        /// <summary>
        /// Assigns out parameter with the Guid of the EditorFactory.
        /// </summary>
        /// <param name="pGuidCmdId">The output parameter that receives a value of the Guid of the EditorFactory.</param>
        /// <returns>S_OK if Marshal operations completed successfully.</returns>
        int IVsDeferredDocView.get_CmdUIGuid(out Guid pGuidCmdId)
        {
            //pGuidCmdId = GuidList.guidVsTemplateDesignerEditorFactory;
            pGuidCmdId = GetGuidOfDesignerEditorFactory();
            return VSConstants.S_OK;
        }
        protected virtual Guid GetGuidOfDesignerEditorFactory()
        {
            return _guidEditorFactory;
        }
        /// <summary>
        /// Assigns out parameter with the document view being implemented.
        /// </summary>
        /// <param name="ppUnkDocView">The parameter that receives a reference to current view.</param>
        /// <returns>S_OK if Marshal operations completed successfully.</returns>
        [EnvironmentPermission(SecurityAction.Demand)]
        int IVsDeferredDocView.get_DocView(out IntPtr ppUnkDocView)
        {
            ppUnkDocView = Marshal.GetIUnknownForObject(this);
            return VSConstants.S_OK;
        }

        #endregion

        #region IOleComponent

        int IOleComponent.FContinueMessageLoop(uint uReason, IntPtr pvLoopData, MSG[] pMsgPeeked)
        {
            return VSConstants.S_OK;
        }

        int IOleComponent.FDoIdle(uint grfidlef)
        {
            if (_designerControl != null)
            {
                _designerControl.DoIdle();
            }
            return VSConstants.S_OK;
        }

        int IOleComponent.FPreTranslateMessage(MSG[] pMsg)
        {
            return VSConstants.S_OK;
        }

        int IOleComponent.FQueryTerminate(int fPromptUser)
        {
            return 1; //true
        }

        int IOleComponent.FReserved1(uint dwReserved, uint message, IntPtr wParam, IntPtr lParam)
        {
            return VSConstants.S_OK;
        }

        IntPtr IOleComponent.HwndGetWindow(uint dwWhich, uint dwReserved)
        {
            return IntPtr.Zero;
        }

        void IOleComponent.OnActivationChange(IOleComponent pic, int fSameComponent, OLECRINFO[] pcrinfo, int fHostIsActivating, OLECHOSTINFO[] pchostinfo, uint dwReserved)
        {
            Debug.WriteLine("OnActivationChange");
        }
        void IOleComponent.OnAppActivate(int fActive, uint dwOtherThreadID)
        {
            Debug.WriteLine("OnAppActivate");
        }

        void IOleComponent.OnEnterState(uint uStateID, int fEnter)
        {
            Debug.WriteLine("OnEnterState");
        }

        void IOleComponent.OnLoseActivation() { }
        void IOleComponent.Terminate() { }

        int IVsFileChangeEvents.FilesChanged(uint cChanges, string[] rgpszFile, uint[] rggrfChange)
        {
            Debug.WriteLine("FilesChanged");
            this._designerControl.UnderlyingFileChanged();
            return VSConstants.S_OK;
        }

        int IVsFileChangeEvents.DirectoryChanged(string pszDirectory)
        {
            Debug.WriteLine("DirectoryChanged");
            return VSConstants.S_OK;
        }

        int IVsDocDataFileChangeControl.IgnoreFileChanges(int fIgnore)
        {
            Debug.WriteLine("IgnoreFileChanges");
            return VSConstants.S_OK;
        }

        #endregion


        protected override bool PreProcessMessage(ref Message m)
        {

            return base.PreProcessMessage(ref m);
        }

    }
}
