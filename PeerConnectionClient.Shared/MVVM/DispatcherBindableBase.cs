using System;
using Windows.UI.Core;

namespace PeerConnectionClient.MVVM
{
    public abstract class DispatcherBindableBase : BindableBase
    {
        private readonly CoreDispatcher _uiDispatcher;

        protected DispatcherBindableBase(CoreDispatcher uiDispatcher)
        {
            _uiDispatcher = uiDispatcher;
        }

        protected override void OnPropertyChanged(string propertyName)
        {
            RunOnUiThread(()=> base.OnPropertyChanged(propertyName));
        }


        protected void RunOnUiThread(Action fn)
        {
            _uiDispatcher.RunAsync(CoreDispatcherPriority.Normal, new DispatchedHandler(fn));
        }
    }
}