using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Package;
using IOleServiceProvider = Microsoft.VisualStudio.OLE.Interop.IServiceProvider;

namespace Genius.VisualStudio.BaseEditors
{
    class DiagHelper
    {
        [Conditional("DEBUG")]
        public static void DumpContext()
        {
            Debug.WriteLine("ThreadId:{0}, ApartmentState:{1}", Thread.CurrentThread.ManagedThreadId, Thread.CurrentThread.GetApartmentState());
        }

        public static Microsoft.XmlEditor.XmlLanguageService GetXmlLanguageService(IServiceProvider spp)
        {
            return (Microsoft.XmlEditor.XmlLanguageService)spp.GetService(typeof(Microsoft.XmlEditor.XmlLanguageService));
        }
    }
}
