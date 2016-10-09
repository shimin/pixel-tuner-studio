using System;
using System.ComponentModel;
using System.Xml.Serialization;

namespace Common.ViewModel
{
    public class ViewModelBase : INotifyPropertyChanged
    {
        protected virtual void RaisePropertyChanged(string propertyName)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
        }
        
        public event PropertyChangedEventHandler PropertyChanged;

        public virtual object Clone()
        {
            var modelCopy = (ViewModelBase)Activator.CreateInstance(this.GetType());
            return modelCopy;
        }
    }
}
