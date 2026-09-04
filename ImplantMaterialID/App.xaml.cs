using System;
using System.Windows;
using System.Windows.Threading;
using ImplantMaterialID.Services;
using ImplantMaterialID.Views;

namespace ImplantMaterialID
{
    /// <summary>
    /// Standalone ESAPI executable entry point. Normally launched independently of Eclipse
    /// (rather than run as an Eclipse plug-in script) with no arguments, for manual patient
    /// entry. It can also be launched *by* an Eclipse binary plugin (see the
    /// ImplantMaterialID.EclipseLauncher project) with --patient/--structureset arguments
    /// identifying whatever was already open in Eclipse - see LaunchArguments and
    /// MainViewModel.InitializeFromLaunchAsync. Either way, before it can open patient data, the
    /// compiled .exe (or its checksum, depending on your ARIA version) generally needs to be
    /// approved in Eclipse under Tools > Script Approval or the equivalent admin tool. See
    /// README.md for the full deployment checklist.
    /// </summary>
    public partial class App : Application
    {
        public App()
        {
            DispatcherUnhandledException += App_DispatcherUnhandledException;
        }

        // No StartupUri in App.xaml: the window is constructed here instead, so the parsed
        // command-line arguments can be passed into it.
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var launchArguments = LaunchArguments.Parse(e.Args);
            var window = new MainWindow(launchArguments);
            MainWindow = window;
            window.Show();
        }

        private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            MessageBox.Show(
                "An unexpected error occurred:\n\n" + e.Exception.Message,
                "Implant Material Identifier",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            e.Handled = true;
        }
    }
}
