using System.Windows;
using ImplantMaterialID.Services;
using ImplantMaterialID.ViewModels;

namespace ImplantMaterialID.Views
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _viewModel;

        // Parameterless overload kept for the XAML designer and any other caller that doesn't
        // have launch arguments to supply - equivalent to launching with no command-line args.
        public MainWindow() : this(null)
        {
        }

        public MainWindow(LaunchArguments launchArguments)
        {
            InitializeComponent();

            // Composition root: swap StaEsapiPatientService() for FakeEsapiPatientService() to
            // run the UI and interpolation logic without a live Eclipse connection. See
            // EsapiStaThread's remarks for why this can't just be Task.Run from here.
            _viewModel = new MainViewModel(new StaEsapiPatientService());
            DataContext = _viewModel;

            Closed += (s, e) => _viewModel.Dispose();

            // If launched by the Eclipse plugin (see ImplantMaterialID.EclipseLauncher) with a
            // patient already identified, load it - and its structure set, if that was open too
            // - automatically instead of waiting for the user to type a patient ID in. Fire-and-
            // forget is fine here: it's the same async flow LoadPatientCommand already drives,
            // and all status/errors surface through the ViewModel's StatusMessage binding.
            if (!string.IsNullOrWhiteSpace(launchArguments?.PatientId))
                _ = _viewModel.InitializeFromLaunchAsync(launchArguments.PatientId, launchArguments.StructureSetId);
        }
    }
}
