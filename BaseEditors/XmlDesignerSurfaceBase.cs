using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace Genius.VisualStudio.BaseEditors
{
    public class XmlDesignerSurfaceBase : UserControl, IXmlDesignerControl
    {
        public XmlDesignerSurfaceBase(IXmlViewModel viewModel)
        {
            if (viewModel != null)
            {
                DataContext = viewModel;
                // wait until we're initialized to handle events
                viewModel.ViewModelChanged += new EventHandler(ViewModelChanged);
            }
        }

        public void DoIdle()
        {
            // only call the view model DoIdle if this control has focus
            // otherwise, we should skip and this will be called again
            // once focus is regained
            IXmlViewModel viewModel = DataContext as IXmlViewModel;
            if (viewModel != null && this.IsKeyboardFocusWithin)
            {
                viewModel.DoIdle();
            }
        }

        private void ViewModelChanged(object sender, EventArgs e)
        {
            // this gets called when the view model is updated because the Xml Document was updated
            // since we don't get individual PropertyChanged events, just re-set the DataContext
            IXmlViewModel viewModel = DataContext as IXmlViewModel;
            //DataContext = null; // first, set to null so that we see the change and rebind
            //DataContext = viewModel;
        }

        internal void UnderlyingFileChanged()
        {
            IXmlViewModel viewModel = DataContext as IXmlViewModel;
            if (viewModel != null)
            {
                viewModel.UnderlyingFileChanged();
            }

        }

    }
}
