using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Genius.VisualStudio.BaseEditors
{
    class ProxyObservableCollection : ObservableCollection<object>, IProxyProperty
    {
        Action _anyChanges;
        PropertyInfo _propertyInfo;
        object _proxied;
        public ProxyObservableCollection(object proxied, PropertyInfo propertyInfo, IEnumerable<object> list,  Action anyChanges)
        {
            _proxied = proxied;
            this._propertyInfo = propertyInfo;
            foreach (var item in list)
            {
                this.Add(item);
            }
            //init _anychanges after add items, to avoid unexpected notifications
            _anyChanges = anyChanges;
        }

        private void FireAnyChanges()
        {
            if (_anyChanges != null)
            {
                Debug.WriteLine("ProxyCollection fire any changes");
                _anyChanges();
            }
        }

        protected override void InsertItem(int index, object item)
        {
            //add a proxy around item
            base.InsertItem(index, new DynamicProxyIPC(item, this.FireAnyChanges));
        }

        protected override void OnCollectionChanged(System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            //update underlying array property
            _propertyInfo.SetValue(_proxied, GetUnderLyingItems());
            base.OnCollectionChanged(e);
            this.FireAnyChanges();
        }

        object[] GetUnderLyingItems()
        {
            //i think that is not the best way (all items are copied on each collectionChanged)
            //but theses classes are not designed to manage heavy xml, and i think that it's a bad way to use a designer over an heavy xml
            var pType = _propertyInfo.PropertyType.GetElementType();
            var result = this.Items.OfType<DynamicProxyIPC>().Select(x => x.ProxiedObject).ToArray();
            var array = (object[])Array.CreateInstance(pType, result.Length);
            for (int i=0; i < result.Length; i++)
            {
                array[i] = result[i];
            }
            return array;
        }
    }
}
