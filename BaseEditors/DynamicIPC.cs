using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Dynamic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Genius.VisualStudio.BaseEditors
{
    /// <summary>
    /// just for a marker use
    /// </summary>
    interface IProxyProperty
    {

    }

    public class DynamicProxyIPC : DynamicObject, INotifyPropertyChanged, IProxyProperty
    {
        private object _proxiedObject;
        internal object ProxiedObject { get { return _proxiedObject; } }
        public event PropertyChangedEventHandler PropertyChanged;
        //in order to notify the root proxy of any change in the tree
        public event Action OnAnyChanges;

        Dictionary<string, IProxyProperty> _subProxiedProperties = new Dictionary<string, IProxyProperty>();
        protected virtual void RaisePropertyChanged(string propertyName) { OnPropertyChanged(propertyName); }
        protected virtual void OnPropertyChanged(string propertyName)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
            FireAnyChanges();
        }

        private void FireAnyChanges()
        {
            if (OnAnyChanges != null)
                OnAnyChanges();
        }
        public DynamicProxyIPC(object proxiedObject)
        {
            this._proxiedObject = proxiedObject;
        }

        /// <summary>
        /// this constructor is used to bubble any changes to root proxy
        /// </summary>
        /// <param name="proxiedObject"></param>
        /// <param name="onAnyChanges"></param>
        internal DynamicProxyIPC(object proxiedObject, Action onAnyChanges) 
            : this(proxiedObject)
        {
            this.OnAnyChanges += onAnyChanges;
        }
        protected PropertyInfo GetPropertyInfo(string propertyName)
        {
            return _proxiedObject.GetType().GetProperties().First(propertyInfo => propertyInfo.Name == propertyName);
        }
        protected virtual void SetMember(string propertyName, object value)
        {
            GetPropertyInfo(propertyName).SetValue(_proxiedObject, value, null);
            RaisePropertyChanged(propertyName);
        }
        protected virtual object GetMember(string propertyName)
        {
            return GetPropertyInfo(propertyName).GetValue(_proxiedObject, null);
        }
        public override bool TryGetMember(GetMemberBinder binder, out object result)
        {
            IProxyProperty subProxy;
            if (_subProxiedProperties.TryGetValue(binder.Name, out subProxy))
            {
                result = subProxy; return true;
            }
            result = GetMember(binder.Name);
            if (result != null)
            {
                switch(IsComplexType(result))
                {
                    case 1 :
                        result = new DynamicProxyIPC(result, this.FireAnyChanges);
                        _subProxiedProperties.Add(binder.Name, (IProxyProperty)result);
                        break;
                    case 2 :
                        //it's an array, we should proxy as ObservableCollection
                        result = new ProxyObservableCollection(_proxiedObject, GetPropertyInfo(binder.Name), 
                                        (IEnumerable<object>)result, this.FireAnyChanges);
                        _subProxiedProperties.Add(binder.Name, (IProxyProperty)result);
                        break;
                    default:
                        break;
                }
                
            }
            return true;
        }
        private int IsComplexType(object result)
        {
            if (result is string)
                return 0;
            if (result.GetType().IsArray) return 2;
            if (result.GetType().IsClass)
            {
                return 1;
            }
            return 0;
        }
        public override bool TrySetMember(SetMemberBinder binder, object value)
        {
            IProxyProperty subProxy;
            if (_subProxiedProperties.TryGetValue(binder.Name, out subProxy))
            {
                _subProxiedProperties.Remove(binder.Name);
            }
            Debug.WriteLine(string.Format("SetValue ({0}, {1})", binder.Name, value));
            SetMember(binder.Name, value);
            return true;
        }
    }
}
