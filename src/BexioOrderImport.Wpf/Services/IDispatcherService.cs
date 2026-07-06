using System;

namespace BexioOrderImport.Wpf.Services;

public interface IDispatcherService
{
    void Invoke(Action action);
    void BeginInvoke(Action action);
}
