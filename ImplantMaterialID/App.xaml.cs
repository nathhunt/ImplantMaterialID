using System;
using System.Windows;
using System.Windows.Threading;

namespace ImplantMaterialID
{
    /// <summary>
    /// Standalone ESAPI executable entry point.
    ///
    /// This is a "stand-alone" ESAPI application, i.e. one that is launched independently
    /// of Eclipse rather than run as an Eclipse plug-in script. Before it can open patient
    /// data, the compiled .exe (or its checksum, depending on your ARIA version) generally
    /// needs to be approved in Eclipse under Tools > Script Approval (or the equivalent
    /// admin tool for your version) by someone with the appropriate clinical/IT permissions.
    /// See README.md for the full deployment checklist.
    /// </summary>
    public partial class App : Application
    {
        public App()
        {
            DispatcherUnhandledException += App_DispatcherUnhandledException;
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
