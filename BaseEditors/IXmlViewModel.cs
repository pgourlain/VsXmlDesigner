using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Genius.VisualStudio.BaseEditors
{
    /// <summary>
    /// 
    /// </summary>
    public interface IXmlViewModel
    {
        event EventHandler ViewModelChanged;

        void DoIdle();
        void Close();

        void UnderlyingFileChanged();

        dynamic ProxiedModel { get; }
    }
}
