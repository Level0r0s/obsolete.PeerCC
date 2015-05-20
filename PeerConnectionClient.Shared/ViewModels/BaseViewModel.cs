using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using Windows.UI.Core;

namespace PeerConnectionClient.ViewModels
{
    internal abstract class BaseViewModel : INotifyPropertyChanged
    {
        public BaseViewModel(CoreDispatcher uiDispatcher)
        {
            _uiDispatcher = uiDispatcher;
        }

        CoreDispatcher _uiDispatcher;

        public event PropertyChangedEventHandler PropertyChanged;

        // This method is called by the Set accessor of each property. 
        // The CallerMemberName attribute that is applied to the optional propertyName 
        // parameter causes the property name of the caller to be substituted as an argument. 
        protected void NotifyPropertyChanged([CallerMemberName] String propertyName = "")
        {
            if (PropertyChanged != null)
            {
                _uiDispatcher.RunAsync(CoreDispatcherPriority.Normal, new DispatchedHandler(() =>
                    {
                        PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
                    }));
            }
        }

        protected void RunOnUiThread(Action fn)
        {
            _uiDispatcher.RunAsync(CoreDispatcherPriority.Normal, new DispatchedHandler(fn));
        }
    }
}
