﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Genius.VisualStudio.BaseEditors;

namespace Genius.VS2013DesignerAndEditor
{
    /// <summary>
    /// Interaction logic for DesignerEditorSurface.xaml
    /// </summary>
    public partial class MyDesignerSurfaceEditor : XmlDesignerSurfaceBase
    {
        public MyDesignerSurfaceEditor() : base(null)
        {
            InitializeComponent();
        }

        public MyDesignerSurfaceEditor(IXmlViewModel viewModel) :base (viewModel)
        {
            InitializeComponent();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            //we must add ligne via Proxy in order to notify datagrid
            this.ViewModel.ProxiedModel.delivrables.Items.Add(new projectDelivrablesItem() { path = "dummy" });
        }
    }
}
