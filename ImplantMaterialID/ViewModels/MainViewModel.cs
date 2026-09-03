using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Threading.Tasks;
using ImplantMaterialID.Models;
using ImplantMaterialID.Services;

namespace ImplantMaterialID.ViewModels
{
    public class MainViewModel : ViewModelBase, IDisposable
    {
        private readonly IEsapiPatientService _esapiService;

        public MainViewModel(IEsapiPatientService esapiService)
        {
            _esapiService = esapiService ?? throw new ArgumentNullException(nameof(esapiService));

            LoadPatientCommand = new RelayCommand(async () => await LoadPatientAsync(), () => !IsBusy && !string.IsNullOrWhiteSpace(PatientId));
            CalculateCommand = new RelayCommand(async () => await CalculateAsync(), () => !IsBusy && SelectedStructure != null && !string.IsNullOrWhiteSpace(DiameterMmText));
        }

        // --- Patient / structure selection -------------------------------

        private string _patientId;
        public string PatientId
        {
            get => _patientId;
            set { if (SetProperty(ref _patientId, value)) LoadPatientCommand.RaiseCanExecuteChanged(); }
        }

        public ObservableCollection<StructureSetSummary> StructureSets { get; } = new ObservableCollection<StructureSetSummary>();

        private StructureSetSummary _selectedStructureSet;
        public StructureSetSummary SelectedStructureSet
        {
            get => _selectedStructureSet;
            set
            {
                if (SetProperty(ref _selectedStructureSet, value))
                {
                    ResetCalculationOutputs();
                    Structures.Clear();
                    SelectedStructure = null;
                    if (value != null)
                        _ = LoadStructuresAsync(value.Id);
                }
            }
        }

        public ObservableCollection<StructureSummary> Structures { get; } = new ObservableCollection<StructureSummary>();

        private StructureSummary _selectedStructure;
        public StructureSummary SelectedStructure
        {
            get => _selectedStructure;
            set
            {
                if (SetProperty(ref _selectedStructure, value))
                {
                    ResetCalculationOutputs();
                    CalculateCommand.RaiseCanExecuteChanged();
                }
            }
        }

        // --- User-entered implant diameter -------------------------------

        private string _diameterMmText;
        public string DiameterMmText
        {
            get => _diameterMmText;
            set { if (SetProperty(ref _diameterMmText, value)) CalculateCommand.RaiseCanExecuteChanged(); }
        }

        // --- Calculation outputs ------------------------------------------

        private double? _meanHu;
        public double? MeanHu
        {
            get => _meanHu;
            private set => SetProperty(ref _meanHu, value);
        }

        private double? _fovMm;
        public double? FovMm
        {
            get => _fovMm;
            private set => SetProperty(ref _fovMm, value);
        }

        private MaterialClassificationResult _result;
        public MaterialClassificationResult Result
        {
            get => _result;
            private set => SetProperty(ref _result, value);
        }

        // --- Status / busy state --------------------------------------------

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            private set
            {
                if (SetProperty(ref _isBusy, value))
                {
                    LoadPatientCommand.RaiseCanExecuteChanged();
                    CalculateCommand.RaiseCanExecuteChanged();
                }
            }
        }

        private string _statusMessage = "Enter a patient ID to begin.";
        public string StatusMessage
        {
            get => _statusMessage;
            private set => SetProperty(ref _statusMessage, value);
        }

        // --- Commands ---------------------------------------------------

        public RelayCommand LoadPatientCommand { get; }
        public RelayCommand CalculateCommand { get; }

        // --- Operations ---------------------------------------------------

        private async Task LoadPatientAsync()
        {
            IsBusy = true;
            StatusMessage = $"Opening patient '{PatientId}'...";
            ResetCalculationOutputs();
            StructureSets.Clear();
            Structures.Clear();
            SelectedStructureSet = null;
            SelectedStructure = null;

            try
            {
                var patientIdSnapshot = PatientId;
                var sets = await _esapiService.OpenPatientAndGetStructureSetsAsync(patientIdSnapshot);

                foreach (var s in sets)
                    StructureSets.Add(s);

                StatusMessage = $"Loaded {sets.Count} structure set(s) for patient '{patientIdSnapshot}'. Select one below.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Could not load patient: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task LoadStructuresAsync(string structureSetId)
        {
            IsBusy = true;
            StatusMessage = $"Loading structures for '{structureSetId}'...";

            try
            {
                var structures = await _esapiService.GetStructuresAsync(structureSetId);
                foreach (var s in structures)
                    Structures.Add(s);

                StatusMessage = $"Loaded {structures.Count} structure(s). Select the implant contour.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Could not load structures: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task CalculateAsync()
        {
            if (SelectedStructureSet == null || SelectedStructure == null)
            {
                StatusMessage = "Select a structure set and structure first.";
                return;
            }

            if (!double.TryParse(DiameterMmText, NumberStyles.Float, CultureInfo.InvariantCulture, out double diameterMm) || diameterMm <= 0)
            {
                StatusMessage = "Enter a valid, positive implant diameter in mm.";
                return;
            }

            IsBusy = true;
            StatusMessage = "Calculating mean HU (this can take a few seconds)...";
            ResetCalculationOutputs();

            try
            {
                var structureSetId = SelectedStructureSet.Id;
                var structureId = SelectedStructure.Id;

                var stats = await _esapiService.ComputeStructureStatisticsAsync(structureSetId, structureId);

                MeanHu = stats.MeanHu;
                FovMm = stats.FovMm;

                Result = ImplantMaterialClassifier.Classify(stats.MeanHu, diameterMm, stats.FovMm);

                StatusMessage = Math.Abs(stats.FovXMm - stats.FovYMm) > 1.0
                    ? $"Done. Note: scan FOV is non-square (X={stats.FovXMm:0} mm, Y={stats.FovYMm:0} mm) - averaged for lookup."
                    : "Done.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Calculation failed: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void ResetCalculationOutputs()
        {
            MeanHu = null;
            FovMm = null;
            Result = null;
        }

        public void Dispose() => _esapiService?.Dispose();
    }
}
