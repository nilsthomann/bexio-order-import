using System;
using System.Windows;

namespace BexioOrderImport.Wpf.Services;

public class WpfDispatcherService : IDispatcherService
{
    public void Invoke(Action action)
    {
        if (System.Windows.Application.Current != null && !System.Windows.Application.Current.Dispatcher.CheckAccess())
        {
            System.Windows.Application.Current.Dispatcher.Invoke(action);
        }
        else
        {
            action();
        }
    }

    public void BeginInvoke(Action action)
    {
        if (System.Windows.Application.Current != null)
        {
            System.Windows.Application.Current.Dispatcher.BeginInvoke(action);
        }
        else
        {
            action();
        }
    }
}
