using System;
using System.Threading;
using System.Windows;

namespace ChatOutLoud;

public partial class App : Application
{
    private const string SingleInstanceMutexName =
        @"Local\ChatOutLoud.SingleInstance";

    private const string ActivateEventName =
        @"Local\ChatOutLoud.Activate";

    private Mutex? _singleInstanceMutex;

    private EventWaitHandle? _activateEvent;

    private RegisteredWaitHandle? _activateRegistration;

    protected override void OnStartup(
        StartupEventArgs e)
    {
        _singleInstanceMutex =
            new Mutex(
                true,
                SingleInstanceMutexName,
                out bool isFirstInstance);

        if (!isFirstInstance)
        {
            try
            {
                using EventWaitHandle activateEvent =
                    EventWaitHandle.OpenExisting(
                        ActivateEventName);

                activateEvent.Set();
            }
            catch (WaitHandleCannotBeOpenedException)
            {
            }

            Shutdown();
            return;
        }

        _activateEvent =
            new EventWaitHandle(
                false,
                EventResetMode.AutoReset,
                ActivateEventName);

        _activateRegistration =
            ThreadPool.RegisterWaitForSingleObject(
                _activateEvent,
                (_, _) =>
                    Dispatcher.BeginInvoke(
                        new Action(
                            ActivateExistingWindow)),
                null,
                Timeout.Infinite,
                false);

        base.OnStartup(e);

        ChatOutLoud.MainWindow window =
            new();

        MainWindow = window;

        window.Show();
    }

    private void ActivateExistingWindow()
    {
        if (MainWindow is null)
        {
            return;
        }

        if (MainWindow.WindowState ==
            WindowState.Minimized)
        {
            MainWindow.WindowState =
                WindowState.Normal;
        }

        MainWindow.Show();
        MainWindow.Activate();

        MainWindow.Topmost = true;
        MainWindow.Topmost = false;

        MainWindow.Focus();
    }

    protected override void OnExit(
        ExitEventArgs e)
    {
        _activateRegistration?.Unregister(
            null);

        _activateRegistration = null;

        _activateEvent?.Dispose();
        _activateEvent = null;

        if (_singleInstanceMutex is not null)
        {
            try
            {
                _singleInstanceMutex.ReleaseMutex();
            }
            catch (ApplicationException)
            {
            }

            _singleInstanceMutex.Dispose();
            _singleInstanceMutex = null;
        }

        base.OnExit(e);
    }
}