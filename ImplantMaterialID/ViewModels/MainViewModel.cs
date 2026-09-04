using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
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

        // Set by InitializeFromLaunchAsync, consumed and cleared the next time LoadPatientAsync
        // finishes loading structure sets. Not used at all for a normal manually-triggered load.
        private string _pendingAutoSelectStructureSetId;

        /// <summary>
        /// Pre-populates and loads a patient (and, if given, auto-selects a structure set) on
        /// behalf of the Eclipse plugin launcher - see MainWindow's constructor. If
        /// <paramref name="structureSetId"/> doesn't match any structure set this patient turns
        /// out to have (or is null, e.g. nothing was open in Eclipse), the user simply picks one
        /// manually, exactly as they would with no launch arguments at all.
        /// </summary>
        public async Task InitializeFromLaunchAsync(string patientId, string structureSetId)
        {
            if (string.IsNullOrWhiteSpace(patientId))
                return;

            PatientId = patientId;
            _pendingAutoSelectStructureSetId = structureSetId;
            await LoadPatientAsync();
        }

        private async Task LoadPatientAsync()
        {
            IsBusy = true;
            StatusMessage = $"Opening patient '{PatientId}'...";
            ResetCalculationOutputs();
            StructureSets.Clear();
            Structures.Clear();
            SelectedStructureSet = null;
            SelectedStructure = null;

            // Consumed up front (not just on success) so a failed load never leaves a stale
            // auto-select id around to affect a later, unrelated manual retry.
            var autoSelectId = _pendingAutoSelectStructureSetId;
            _pendingAutoSelectStructureSetId = null;

            try
            {
                var patientIdSnapshot = PatientId;
                var sets = await _esapiService.OpenPatientAndGetStructureSetsAsync(patientIdSnapshot);

                foreach (var s in sets)
                    StructureSets.Add(s);

                if (string.IsNullOrWhiteSpace(autoSelectId))
                {
                    StatusMessage = $"Loaded {sets.Count} structure set(s) for patient '{patientIdSnapshot}'. Select one below.";
                }
                else
                {
                    var match = sets.FirstOrDefault(s => string.Equals(s.Id, autoSelectId, StringComparison.OrdinalIgnoreCase));
                    if (match != null)
                    {
                        StatusMessage = $"Loaded {sets.Count} structure set(s) for patient '{patientIdSnapshot}'. Auto-selected structure set '{match.Id}' from Eclipse.";

                        // Set the backing field directly and await LoadStructuresAsync ourselves,
                        // rather than going through the SelectedStructureSet setter (which fires
                        // LoadStructuresAsync fire-and-forget) - that would let this method's own
                        // `finally` clear IsBusy before the structure load it just kicked off has
                        // actually finished. Structures/SelectedStructure are already cleared from
                        // the top of this method, so there's nothing else the setter would do.
                        _selectedStructureSet = match;
                        OnPropertyChanged(nameof(SelectedStructureSet));
                        await LoadStructuresAsync(match.Id);
                    }
                    else
                    {
                        StatusMessage = $"Loaded {sets.Count} structure set(s) for patient '{patientIdSnapshot}'. " +
                            $"The structure set open in Eclipse ('{autoSelectId}') wasn't found here - select one below.";
                    }
                }
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
