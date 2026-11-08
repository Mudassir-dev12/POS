using System;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using ShopPOS.UI;

namespace ShopPOS.Tests;

public static class StaTestHelper
{
    private static Thread? _staThread;
    private static Dispatcher? _dispatcher;
    private static readonly object _syncLock = new();

    public static void RunOnSta(Action action)
    {
        lock (_syncLock)
        {
            if (_dispatcher == null || _staThread == null || !_staThread.IsAlive)
            {
                App.IsTestEnvironment = true;
                using var readyEvent = new ManualResetEventSlim(false);
                _staThread = new Thread(() =>
                {
                    App.IsTestEnvironment = true;
                    if (Application.Current == null)
                    {
                        var app = new App();
                        app.InitializeComponent();
                        app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
                    }
                    else
                    {
                        Application.Current.ShutdownMode = ShutdownMode.OnExplicitShutdown;
                    }
                    _dispatcher = Dispatcher.CurrentDispatcher;
                    readyEvent.Set();
                    Dispatcher.Run();
                });
                _staThread.SetApartmentState(ApartmentState.STA);
                _staThread.IsBackground = true;
                _staThread.Start();
                readyEvent.Wait();
            }
        }

        Exception? threadEx = null;
        _dispatcher!.Invoke(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                threadEx = ex;
            }
        });

        if (threadEx != null)
        {
            ExceptionDispatchInfo.Capture(threadEx).Throw();
        }
    }
}

