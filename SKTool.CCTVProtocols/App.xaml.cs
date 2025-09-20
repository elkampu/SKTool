using System;
using System.Threading.Tasks;
using System.Windows;
using SKTool.CCTVProtocols.Samples.WPF.Services;

namespace SKTool.CCTVProtocols.Samples.WPF
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // UI thread exceptions (e.g., async void event handlers)
            this.DispatcherUnhandledException += (s, args) =>
            {
                CameraErrorHandler.Handle(args.Exception, "Unhandled (UI)");
                args.Handled = true; // prevent crash; remove if you prefer to crash
            };

            // Background task exceptions that were not observed
            TaskScheduler.UnobservedTaskException += (s, args) =>
            {
                CameraErrorHandler.Handle(args.Exception, "Unhandled (TaskScheduler)");
                args.SetObserved();
            };
        }
    }
}