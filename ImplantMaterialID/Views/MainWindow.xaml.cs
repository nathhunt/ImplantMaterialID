using System.Windows;
using ImplantMaterialID.Services;
using ImplantMaterialID.ViewModels;

namespace ImplantMaterialID.Views
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _viewModel;

        public MainWindow()
        {
            InitializeComponent();

            // Composition root: swap StaEsapiPatientService() for a FakeEsapiPatientService()
            // here to run the UI and interpolation logic without a live Eclipse connection.
            // StaEsapiPatientService owns the single dedicated STA thread that ESAPI requires -
            // see EsapiStaThread's remarks for why this can't just be Task.Run from here.
            _viewModel = new MainViewModel(new StaEsapiPatientService());
            DataContext = _viewModel;

            Closed += (s, e) => _viewModel.Dispose();
        }
    }
}
