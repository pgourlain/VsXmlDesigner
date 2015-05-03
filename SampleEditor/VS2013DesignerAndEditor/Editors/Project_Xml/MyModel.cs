using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace Genius.VS2013DesignerAndEditor
{

    public class NotifyPropertyChangedClass : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected void RPC(string propName)
        {
            if (propName == null) return;
            if (this.PropertyChanged != null)
                this.PropertyChanged(this, new PropertyChangedEventArgs(propName));
        }
    }

    [System.SerializableAttribute()]

    [System.Xml.Serialization.XmlRootAttribute(Namespace = "http://schemas.pgourlain.fr/developer/project/2015", IsNullable = false, ElementName ="project")]

    public class MyModel : NotifyPropertyChangedClass
    {
        string _version;
        [XmlAttribute("version")]
        public string version { get {return _version; } set { _version = value; RPC("version"); } }
        projectDescription _description;
        [XmlElement(ElementName = "description")]
        public projectDescription description { get { return _description; } set { _description = value; RPC("description"); } }

        [XmlElement(ElementName = "delivrables")]
        public projectDelivrables delivrables { get; set; }
    }


    [System.SerializableAttribute()]
    public class projectDescription : NotifyPropertyChangedClass
    {
        [XmlElement(ElementName = "ou")]
        public string ou { get; set; }
        [XmlElement(ElementName = "owner")]
        public string owner { get; set; }
    }

    [System.SerializableAttribute()]
    public class projectDelivrables : NotifyPropertyChangedClass
    {
        [XmlElement(ElementName = "item")]
        public projectDelivrablesItem[] Items { get; set; }
    }


    [System.SerializableAttribute()]
    public class projectDelivrablesItem : NotifyPropertyChangedClass
    {
        [XmlAttribute("path")]
        public string path { get; set; }
    }
}
