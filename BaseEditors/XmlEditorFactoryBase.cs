using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Permissions;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TextManager.Interop;
using EnvDTE;

namespace Genius.VisualStudio.BaseEditors
{
    /// <summary>
    /// base classe factory for a custom Wpf Designer over an Xml
    /// </summary>
    /// <typeparam name="TDesignerControl">Designer surface that derive from <see cref="XmlDesignerSurfaceBase"/></typeparam>
    /// <typeparam name="TViewModel">View Model that derive from <see cref="XmlViewModelBase{TModel}"/></typeparam>
    /// <typeparam name="TModel"></typeparam>
    public abstract class XmlEditorFactoryBase<TDesignerControl, TViewModel, TModel> : IVsEditorFactory, IDisposable
        where TDesignerControl : XmlDesignerSurfaceBase
        where TViewModel : XmlViewModelBase<TModel>
        where TModel : new()
    {
        private Package editorPackage;
        private ServiceProvider vsServiceProvider;

        protected Package Package
        {
            get
            {
                return this.editorPackage;
            }
        }

        protected ServiceProvider ServiceProvider
        {
            get
            {
                return vsServiceProvider;
            }
        }

        public XmlEditorFactoryBase(Package package)
        {
            Debug.WriteLine(string.Format(CultureInfo.CurrentCulture, "Entering {0} constructor", this.ToString()));

            this.editorPackage = package;
        }

        /// <summary>
        /// Since we create a ServiceProvider which implements IDisposable we
        /// also need to implement IDisposable to make sure that the ServiceProvider's
        /// Dispose method gets called.
        /// </summary>
        public void Dispose()
        {
            if (vsServiceProvider != null)
            {
                vsServiceProvider.Dispose();
            }
        }

        #region IVsEditorFactory Members

        /// <summary>
        /// Used for initialization of the editor in the environment
        /// </summary>
        /// <param name="psp">pointer to the service provider. Can be used to obtain instances of other interfaces
        /// </param>
        /// <returns></returns>
        public int SetSite(Microsoft.VisualStudio.OLE.Interop.IServiceProvider psp)
        {
            vsServiceProvider = new ServiceProvider(psp);
            return VSConstants.S_OK;
        }

        public object GetService(Type serviceType)
        {
            return vsServiceProvider.GetService(serviceType);
        }

        // This method is called by the Environment (inside IVsUIShellOpenDocument::
        // OpenStandardEditor and OpenSpecificEditor) to map a LOGICAL view to a 
        // PHYSICAL view. A LOGICAL view identifies the purpose of the view that is
        // desired (e.g. a view appropriate for Debugging [LOGVIEWID_Debugging], or a 
        // view appropriate for text view manipulation as by navigating to a find
        // result [LOGVIEWID_TextView]). A PHYSICAL view identifies an actual type 
        // of view implementation that an IVsEditorFactory can create. 
        //
        // NOTE: Physical views are identified by a string of your choice with the 
        // one constraint that the default/primary physical view for an editor  
        // *MUST* use a NULL string as its physical view name (*pbstrPhysicalView = NULL).
        //
        // NOTE: It is essential that the implementation of MapLogicalView properly
        // validates that the LogicalView desired is actually supported by the editor.
        // If an unsupported LogicalView is requested then E_NOTIMPL must be returned.
        //
        // NOTE: The special Logical Views supported by an Editor Factory must also 
        // be registered in the local registry hive. LOGVIEWID_Primary is implicitly 
        // supported by all editor types and does not need to be registered.
        // For example, an editor that supports a ViewCode/ViewDesigner scenario
        // might register something like the following:
        //        HKLM\Software\Microsoft\VisualStudio\<version>\Editors\
        //            {...guidEditor...}\
        //                LogicalViews\
        //                    {...LOGVIEWID_TextView...} = s ''
        //                    {...LOGVIEWID_Code...} = s ''
        //                    {...LOGVIEWID_Debugging...} = s ''
        //                    {...LOGVIEWID_Designer...} = s 'Form'
        //
        public int MapLogicalView(ref Guid rguidLogicalView, out string pbstrPhysicalView)
        {
            pbstrPhysicalView = null;    // initialize out parameter

            if (VSConstants.LOGVIEWID_Primary == rguidLogicalView)
                return VSConstants.S_OK;        // primary view uses NULL as pbstrPhysicalView
            else if (VSConstants.LOGVIEWID.Code_guid == rguidLogicalView)
            {
                pbstrPhysicalView = "Code";
                return VSConstants.S_OK;
            }
            else if (VSConstants.LOGVIEWID.Designer_guid == rguidLogicalView)
            {
                pbstrPhysicalView = "Design";
                return VSConstants.S_OK;
            }
            else
            {
                Debug.WriteLine(string.Format("MapLogicalView notimplemented : {0}", rguidLogicalView));
                return VSConstants.E_NOTIMPL;   // you must return E_NOTIMPL for any unrecognized rguidLogicalView values
            }
        }

        public int Close()
        {
            return VSConstants.S_OK;
        }

        /// <summary>
        /// Used by the editor factory to create an editor instance. the environment first determines the 
        /// editor factory with the highest priority for opening the file and then calls 
        /// IVsEditorFactory.CreateEditorInstance. If the environment is unable to instantiate the document data 
        /// in that editor, it will find the editor with the next highest priority and attempt to so that same 
        /// thing. 
        /// NOTE: The priority of our editor is 32 as mentioned in the attributes on the package class.
        /// 
        /// Since our editor supports opening only a single view for an instance of the document data, if we 
        /// are requested to open document data that is already instantiated in another editor, or even our 
        /// editor, we return a value VS_E_INCOMPATIBLEDOCDATA.
        /// </summary>
        /// <param name="grfCreateDoc">Flags determining when to create the editor. Only open and silent flags 
        /// are valid
        /// </param>
        /// <param name="pszMkDocument">path to the file to be opened</param>
        /// <param name="pszPhysicalView">name of the physical view</param>
        /// <param name="pvHier">pointer to the IVsHierarchy interface</param>
        /// <param name="itemid">Item identifier of this editor instance</param>
        /// <param name="punkDocDataExisting">This parameter is used to determine if a document buffer 
        /// (DocData object) has already been created
        /// </param>
        /// <param name="ppunkDocView">Pointer to the IUnknown interface for the DocView object</param>
        /// <param name="ppunkDocData">Pointer to the IUnknown interface for the DocData object</param>
        /// <param name="pbstrEditorCaption">Caption mentioned by the editor for the doc window</param>
        /// <param name="pguidCmdUI">the Command UI Guid. Any UI element that is visible in the editor has 
        /// to use this GUID. This is specified in the .vsct file
        /// </param>
        /// <param name="pgrfCDW">Flags for CreateDocumentWindow</param>
        /// <returns></returns>
        [SecurityPermission(SecurityAction.Demand, Flags = SecurityPermissionFlag.UnmanagedCode)]
        public int CreateEditorInstance(
                        uint grfCreateDoc,
                        string pszMkDocument,
                        string pszPhysicalView,
                        IVsHierarchy pvHier,
                        uint itemid,
                        System.IntPtr punkDocDataExisting,
                        out System.IntPtr ppunkDocView,
                        out System.IntPtr ppunkDocData,
                        out string pbstrEditorCaption,
                        out Guid pguidCmdUI,
                        out int cancelled)
        {
            Debug.WriteLine(string.Format(CultureInfo.CurrentCulture, "Entering {0} CreateEditorInstance()", this.ToString()));
            // Initialize to null
            ppunkDocView = IntPtr.Zero;
            ppunkDocData = IntPtr.Zero;
            cancelled = 0;
            pbstrEditorCaption = "";
            pguidCmdUI = Guid.Empty;
            if (!AcceptThisFile(pszMkDocument))
            {
                //FROM MSDN
                //When looping through CreateEditorInstance to find an appropriate editor to edit a file, 
                //if the file is currently open, returning VS_E_UNSUPPORTEDFORMAT will allow the loop to continue without closing the document.
                return VSConstants.VS_E_UNSUPPORTEDFORMAT;
            }
            pguidCmdUI = this.GetType().GUID;

            // Validate inputs
            if ((grfCreateDoc & (VSConstants.CEF_OPENFILE | VSConstants.CEF_SILENT)) == 0)
            {
                return VSConstants.E_INVALIDARG;
            }

            IVsTextLines textBuffer = null;

            if (punkDocDataExisting == IntPtr.Zero)
            {
                // punkDocDataExisting is null which means the file is not yet open.
                // We need to create a new text buffer object 

                // get the ILocalRegistry interface so we can use it to
                // create the text buffer from the shell's local registry
                try
                {
                    ILocalRegistry localRegistry = (ILocalRegistry)GetService(typeof(SLocalRegistry));
                    if (localRegistry != null)
                    {
                        IntPtr ptr;
                        Guid iid = typeof(IVsTextLines).GUID;
                        Guid CLSID_VsTextBuffer = typeof(VsTextBufferClass).GUID;
                        localRegistry.CreateInstance(CLSID_VsTextBuffer, null, ref iid, 1 /*CLSCTX_INPROC_SERVER*/, out ptr);
                        try
                        {
                            textBuffer = Marshal.GetObjectForIUnknown(ptr) as IVsTextLines;
                        }
                        finally
                        {
                            Marshal.Release(ptr); // Release RefCount from CreateInstance call
                        }

                        // It is important to site the TextBuffer object
                        IObjectWithSite objWSite = (IObjectWithSite)textBuffer;
                        if (objWSite != null)
                        {
                            Microsoft.VisualStudio.OLE.Interop.IServiceProvider oleServiceProvider = (Microsoft.VisualStudio.OLE.Interop.IServiceProvider)GetService(typeof(Microsoft.VisualStudio.OLE.Interop.IServiceProvider));
                            objWSite.SetSite(oleServiceProvider);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Can not get IVsCfgProviderEventsHelper" + ex.Message);
                    throw;
                }
            }
            else
            {
                // punkDocDataExisting is *not* null which means the file *is* already open. 
                // We need to verify that the open document is in fact a TextBuffer. If not
                // then we need to return the special error code VS_E_INCOMPATIBLEDOCDATA which
                // causes the user to be prompted to close the open file. If the user closes the
                // file then we will be called again with punkDocDataExisting as null

                // QI existing buffer for text lines
                textBuffer = Marshal.GetObjectForIUnknown(punkDocDataExisting) as IVsTextLines;
                if (textBuffer == null)
                {
                    return VSConstants.VS_E_INCOMPATIBLEDOCDATA;
                }
                if (CheckIsOpen(pszMkDocument))
                {
                    cancelled = 1;
                    return VSConstants.S_OK;
                }
            }

            if (pszPhysicalView == "Code")
            {
                CreateStandardXmlDesigner(pszMkDocument, textBuffer);
                return VSConstants.S_OK;
            }
            pbstrEditorCaption = "[Design]";
            // Create the Document (editor)
            WindowPane NewEditor = CreateEditorPane(pszMkDocument, textBuffer);
            ppunkDocView = Marshal.GetIUnknownForObject(NewEditor);
            ppunkDocData = Marshal.GetIUnknownForObject(textBuffer);
            return VSConstants.S_OK;
        }

        private bool CheckIsOpen(string pszMkDocument)
        {
            IVsUIHierarchy uiHierarchy;
            uint itemID;
            IVsWindowFrame windowFrame;

            if (Microsoft.VisualStudio.Shell.VsShellUtilities.IsDocumentOpen(ServiceProvider, pszMkDocument, Guid.Empty,
                                out uiHierarchy, out itemID, out windowFrame))
            {

                var dte = (DTE)this.GetService(typeof(SDTE));
                var wText = VsShellUtilities.GetWindowObject(windowFrame);
                if (wText != null && wText.Caption.EndsWith("[Design]"))
                {
                    wText.Activate();
                    return true;
                }
                foreach (Window w in dte.Windows)
                {
                    if (w.Document != null && string.Compare(w.Document.FullName, pszMkDocument, StringComparison.OrdinalIgnoreCase) == 0)
                    {
                        if (w.Caption.EndsWith("[Design]"))
                        {
                            w.Activate();
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        protected virtual WindowPane CreateEditorPane(string pszMkDocument, IVsTextLines textBuffer)
        {
            return new XmlDesignerPaneBase<TDesignerControl, TViewModel>(this.Package, pszMkDocument, textBuffer, this.GetType().GUID);
        }

        protected virtual bool AcceptThisFile(string fileName)
        {
            return true;
        }

        private void CreateStandardXmlDesigner(string pszMkDocument, IVsTextLines textBuffer)
        {
            Guid XmlTextEditorGuid = new Guid("FA3CD31E-987B-443A-9B81-186104E8DAC1");

            // Open the referenced document using our editor.
            IVsWindowFrame frame;
            IVsUIHierarchy hierarchy;
            uint itemid;
            VsShellUtilities.OpenDocumentWithSpecificEditor(vsServiceProvider, pszMkDocument,
                XmlTextEditorGuid, VSConstants.LOGVIEWID_Primary, out hierarchy, out itemid, out frame);
            ErrorHandler.ThrowOnFailure(frame.Show());
        }

        #endregion
    }
}
