using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using Genius.VisualStudio.BaseEditors;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.XmlEditor;

namespace Genius.VS2013DesignerAndEditor
{
    public class MyXmlViewModel : XmlViewModelBase<MyModel>
    {
        public MyXmlViewModel(XmlStore xmlStore, XmlModel xmlModel, IServiceProvider provider, IVsTextLines buffer) 
            : base(xmlStore, xmlModel, provider, buffer)
        {

        }
    }
}
