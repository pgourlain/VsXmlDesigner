using System;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Permissions;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Shell;

using IOleServiceProvider = Microsoft.VisualStudio.OLE.Interop.IServiceProvider;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.OLE.Interop;
using System.IO;
using Genius.VisualStudio.BaseEditors;

namespace Genius.VS2013DesignerAndEditor
{
    /// <summary>
    /// Factory for creating our editor object. Extends from the IVsEditoryFactory interface
    /// </summary>
    [Guid(GuidList.guidVS2013DesignerAndEditorEditorFactoryString)]
    public sealed class EditorFactory : XmlEditorFactoryBase<MyDesignerSurfaceEditor, MyXmlViewModel, MyModel>
    {
        public EditorFactory(VS2013DesignerAndEditorPackage package) : base(package)
        {
            Debug.WriteLine(string.Format(CultureInfo.CurrentCulture, "Entering {0} constructor", this.ToString()));
        }

        protected override bool AcceptThisFile(string fileName)
        {
            if (string.Compare(Path.GetFileName(fileName), "project1.xml", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return true;
            }
            return false;
        }
    }
}
