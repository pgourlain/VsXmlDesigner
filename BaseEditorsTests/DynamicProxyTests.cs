using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Dynamic;
using System.Linq;
using System.Reflection;
using Genius.VisualStudio.BaseEditors;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BaseEditorsTests
{

    [TestClass]
    public class DynamicProxyTests
    {
        [TestMethod]
        public void TestProxiedProperty()
        {
            MyModel m = new MyModel();
            dynamic mp = new DynamicProxyIPC(m);
            mp.IntProperty = 123;
            Assert.AreEqual(123, m.IntProperty);

            mp.SubModelProperty = new SubModel();
            Assert.IsNotNull(m.SubModelProperty);

            Assert.AreEqual(typeof(DynamicProxyIPC), mp.SubModelProperty.GetType());
        }

        private void Subscribe(INotifyPropertyChanged ipc, PropertyChangedEventHandler handler)
        {
            ipc.PropertyChanged += handler;
        }


        [TestMethod]
        public void TestProxiedPropertyWithNotifyPropertyChanged()
        {
            int notify = 0;
            PropertyChangedEventHandler handler = (_, __) =>
            {
                notify++;
            };
            MyModel m = new MyModel();
            dynamic mp = new DynamicProxyIPC(m);
            Subscribe(mp, handler);
            mp.IntProperty = 123;
            Assert.AreEqual(1, notify);

            mp.SubModelProperty = new SubModel();
            Assert.IsNotNull(m.SubModelProperty);
            Assert.AreEqual(2, notify);
            Subscribe(mp.SubModelProperty, handler);
            mp.SubModelProperty.Name = "coucou";
            Assert.AreEqual(3, notify);

        }

        [TestMethod]
        public void TestProxiedArrayProperty()
        {
            int notify = 0;
            Action handler = () =>
            {
                notify++;
            };
            MyModel m = new MyModel();
            m.Submodels = new SubModel[0];

            var dp = new DynamicProxyIPC(m);
            dp.OnAnyChanges += handler;
            dynamic mp = dp;

            var submodels = mp.Submodels;

            Assert.IsTrue(submodels is ObservableCollection<object>, "invalide array property type");

            submodels.Add(new SubModel { Name = "PGO" });
            Assert.AreEqual(1, mp.Submodels.Count);
            Assert.AreEqual(1, m.Submodels.Length);
            Assert.AreEqual(1, notify);
            Assert.AreEqual("PGO", m.Submodels[0].Name);

            mp.Submodels[0].Name = "PGOGPO";
            Assert.AreEqual(2, notify);

        }
    }


    class MyModel
    {
        public int IntProperty { get; set; }

        public SubModel SubModelProperty { get; set; }
        public SubModel[] Submodels { get; set; }
    }


    class SubModel
    {
        public string Name { get; set; }
        public string LastName { get; set; }
    }
}
