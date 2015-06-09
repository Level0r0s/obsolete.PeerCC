using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Input;

namespace PeerConnectionClient.UAP
{
    public class ActionCommand : ICommand
    {
        public delegate bool CanExecuteDelegate(object parameter);

        private readonly Action<object> _actionExecute;
        private readonly CanExecuteDelegate _actionCanExecute;

        public ActionCommand(Action<object> actionExecute, CanExecuteDelegate actionCanExecute = null)
        {
            _actionExecute = actionExecute;
            _actionCanExecute = actionCanExecute;
        }

        public bool CanExecute(object parameter)
        {
            if (_actionCanExecute != null)
                return _actionCanExecute(parameter);
            return true;
        }

        public void Execute(object parameter)
        {
            _actionExecute(parameter);
        }

        public void RaiseCanExecuteChanged()
        {
            if (CanExecuteChanged != null)
            {
                CanExecuteChanged(this, null);
            }
        }

        public event EventHandler CanExecuteChanged;
    }
}
