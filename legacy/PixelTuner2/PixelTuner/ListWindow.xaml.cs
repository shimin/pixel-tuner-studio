using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using DockingLibrary;
using System.Xml;

namespace PixelTuner
{
    /// <summary>
    /// Interaction logic for ListWindow.xaml
    /// </summary>
    public partial class ListWindow : DockingLibrary.DockableContent
    {
        public ListWindow()
        {
            InitializeComponent();
        }

        public override System.Xml.XmlNode Serialize(System.Xml.XmlDocument doc)
        {
            XmlNode node = base.Serialize(doc);
            node.Attributes.Append(doc.CreateAttribute("Name"));
            node.Attributes["Name"].Value = Name.ToString();
            node.Attributes.Append(doc.CreateAttribute("Title"));
            node.Attributes["Title"].Value = Title.ToString();
            return node;
        }
    }
}
