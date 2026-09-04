# Implant Material Identifier (standalone ESAPI executable)

A stand-alone ESAPI WPF application (MVVM) that:

1. Opens a patient by ID.
2. Lets you pick a structure set, then a structure (the contoured metal implant).
3. Computes the **mean HU** of that structure by iterating the CT voxels it contains.
4. Computes the **scan FOV** from the CT image's resolution and matrix size.
5. Takes a user-entered **implant diameter (mm)**.
6. Bilinearly interpolates Stainless Steel and Titanium reference tables (diameter × FOV)
   and reports which material's expected HU is closer to the measured mean HU, flagging the
   result as unreliable if it isn't within **±1000 HU** of either curve.

## Project layout

```
ImplantMaterialID.sln
ImplantMaterialID/
  ImplantMaterialID.csproj
  App.xaml / App.xaml.cs / App.config
  Views/MainWindow.xaml(.cs)              <- View
  ViewModels/MainViewModel.cs             <- ViewModel (+ RelayCommand, ViewModelBase)
  Models/                                 <- reference tables, interpolation, classification (pure C#, no ESAPI)
  Services/EsapiPatientService.cs         <- EsapiPatientServiceCore: all the real ESAPI calls live here
  Services/EsapiStaThread.cs              <- the dedicated single STA thread ESAPI runs on
  Services/StaEsapiPatientService.cs      <- marshals every call onto that thread; this is what the UI talks to
  Services/FakeEsapiPatientService.cs     <- canned data for UI development without Eclipse
  Services/LaunchArguments.cs             <- parses --patient/--structureset (see "Launching from Eclipse" below)
ImplantMaterialID.EclipseLauncher/
  ImplantMaterialID.EclipseLauncher.csproj
  Script.cs                               <- Eclipse binary plugin that launches the exe above
```

## Threading model

ESAPI 18.x's guidance is explicit: stand-alone executables must create `Application` on a
single STA thread, and must never touch ESAPI objects from a worker thread, `Task`, background
thread, async continuation, or PLINQ. An earlier version of this app violated that by calling
into ESAPI via `Task.Run(...)` from the ViewModel, and it crashed exactly as that rule
predicts: Varian's native `vmod` layer detects the cross-thread access and hard-aborts with
`Reason = Failed assertion` / `Atomic access violation` rather than risking silent corruption.

The fix, reflected in the current code:

- **`EsapiPatientServiceCore`** (in `EsapiPatientService.cs`) holds all the real ESAPI calls,
  as plain synchronous methods. It does no thread-marshaling itself - it trusts its caller to
  only ever invoke it from one consistent thread. Nothing else in the app should reference
  this class directly.
- **`EsapiStaThread`** is a small dedicated background thread, created with
  `ApartmentState.STA`, that pulls delegates off a queue and runs them one at a time, forever.
  This is the *one* thread `Application.CreateApplication()` and every subsequent ESAPI call
  runs on, for the lifetime of the app.
- **`StaEsapiPatientService`** is what the UI actually talks to. It implements the async
  `IEsapiPatientService` contract, and every method body is just "marshal this call onto the
  ESAPI thread and await the result." The ViewModel can `await` these calls normally - it
  doesn't need to know ESAPI's threading rules exist.

This keeps the UI responsive during the slow mean-HU voxel loop (it runs on its own thread,
not the UI thread) while still giving ESAPI the single consistent thread it requires - it just
isn't the WPF UI thread. `EsapiPatientServiceCore` and `StaEsapiPatientService` only ever pass
plain data (DTOs, exceptions) across the thread boundary, never live ESAPI objects, so there's
nothing left for the two threads to disagree about.

**This was validated, not just reasoned about.** Before wiring it back into the app, this
threading model was tested against a stub ESAPI surface that simulates the same assertion
Varian's `vmod` layer performs - it throws if any stub `Application`/`Patient`/`Structure`/
`Image` object is touched from a thread other than the one that "created" it. Coverage
included: the normal load → select → calculate flow; confirming `await` continuations
correctly return to the UI thread (using a WPF-Dispatcher-like `SynchronizationContext`, so the
test is representative of the real app rather than an artifact of running in a console); a
missing-patient error surfacing as a normal .NET exception rather than a crash; twenty
overlapping calls from different caller threads all completing without tripping the simulated
assertion; and `Dispose()` completing cleanly. All passed with zero simulated assertion
failures - the real, non-stub code path is unchanged from what was tested.

The `Models` folder has **no dependency on ESAPI or WPF** and was unit-tested standalone
(bilinear interpolation against known table values, boundary clamping, and the ±1000 HU
tolerance logic including the exact-1000 boundary) before being wired into the app.

## Before you can build

ESAPI's DLLs aren't on NuGet - they ship with your Eclipse/ARIA installation. The project
references them directly from the installed location via the `EsapiInstallDir` MSBuild
property at the top of `ImplantMaterialID.csproj`:

```xml
<EsapiInstallDir>C:\Program Files (x86)\Varian\RTM\18.1\esapi\API\</EsapiInstallDir>
```

This is set for ESAPI 18.1 (.NET Framework 4.8, matching the `TargetFramework` already set
below it). If you build on a different workstation, or move to a different ESAPI version
later, this is the only line you should need to change - update the path (and
`TargetFramework` too, if the new version needs a different one) and both references will
pick it up automatically.

The project is set to build for **x64 only** (`Platforms`/`PlatformTarget` in the `.csproj`)
- ESAPI's assemblies are 64-bit only, so `Any CPU` or `x86` builds will fail at runtime with
a `BadImageFormatException`.

If Visual Studio prompts about missing binding redirects the first time you build against
the real DLLs, let it auto-generate them into `App.config`.

## Deployment / clinical approval checklist

This is a standalone executable, not an Eclipse plug-in script, so it needs its own
sign-off path:

- **Script/executable approval.** Most ARIA/Eclipse configurations require a stand-alone
  ESAPI executable (or its checksum) to be approved before it's allowed to open patient
  data - typically under *Eclipse > Tools > Script Approval* or an equivalent admin tool.
  Check this with whoever administers script approval at your site; the exact workflow
  varies by ARIA version.
- **Login.** `Application.CreateApplication()` (used in `EsapiPatientService`) is the
  standard entry point for a stand-alone app and will prompt interactively for Varian
  credentials if your Windows identity isn't already trusted. If your site instead requires
  explicit service credentials, switch to the
  `Application.CreateApplication(string userId, SecureString password)` overload.
- **Where it runs.** It needs to run on a machine that can reach your Varian database/App
  services the same way an Eclipse workstation does.

## Launching from Eclipse (optional binary plugin)

The exe can be run entirely on its own (patient ID typed in by hand, as described above), but it
can also be launched *from inside Eclipse*, pre-populated with whichever patient/structure set is
already open there, via a small companion Eclipse binary plugin:
`ImplantMaterialID.EclipseLauncher/Script.cs`.

**How it works.** The plugin implements ESAPI's standard compiled-script contract
(`VMS.TPS.Script.Execute(ScriptContext context)`) and does only one thing: it reads
`context.Patient?.Id` and `context.StructureSet?.Id` - whatever is currently open in Eclipse,
either of which may be null - and starts `ImplantMaterialID.exe` as a new process with those as
command-line arguments:

```
ImplantMaterialID.exe --patient MRN12345 --structureset CT_1
```

`App.xaml.cs` parses these (`Services/LaunchArguments.cs`) and hands them to
`MainViewModel.InitializeFromLaunchAsync`, which loads that patient automatically and, if the
structure set ID matches one this patient actually has, selects it too - exactly the same
`LoadPatientCommand`/`SelectedStructureSet` flow a manual user would drive, just triggered
programmatically. If nothing was open in Eclipse (or the structure set ID doesn't match), that
field is simply left for the user to fill in by hand - there is no special-case UI, it behaves
exactly like a normal manual launch from that point on.

The plugin does **not** and cannot share Eclipse's live ESAPI `Application`/`Patient` objects with
the launched process - live objects can't cross a process boundary. That's not a limitation in
practice: the launched exe re-authenticates and re-opens the patient itself via its own
`Application.CreateApplication()`, which is normally silent (see "Login" above) since it inherits
the same Windows identity Eclipse already trusted.

**Deployment.** Build both projects and copy `ImplantMaterialID.exe` (with its dependencies) and
`ImplantMaterialID.EclipseLauncher.dll` to the same folder - the plugin looks for the exe next to
itself by default. If your site deploys the exe somewhere else (e.g. a shared network path used
by every workstation), point the plugin at it with the `IMPLANTMATERIALID_EXE_PATH` environment
variable instead of keeping them side by side.

**Approval.** This plugin is a separate compiled binary from the standalone exe, so it needs its
own sign-off under *Eclipse > Tools > Script Approval* (or your site's equivalent) in addition to
- not instead of - the exe's own approval described above. Once approved, add it to Eclipse's
script list like any other binary plugin and run it from the Scripts menu.

## Validating version-specific ESAPI behaviour

These are the two places this kind of ESAPI code most commonly breaks across versions or
sites (separate from the threading fix above, which is already validated) - flagged in code
comments too:

1. **`Patient.StructureSets`** (in `EsapiPatientService.GetAllStructureSets`) is a direct
   convenience property added in later ESAPI versions. If it's not available in yours,
   replace that method with a traversal through
   `patient.Courses -> course.PlanSetups -> planSetup.StructureSet`, de-duplicated by
   `StructureSet.UID`.
2. **`Image.GetVoxels(int z, int[,] buffer)` index order** (in
   `EsapiPatientService.ComputeMeanHu`) - whether the buffer is indexed `[x, y]` or `[y, x]`
   has differed across ESAPI versions. Test the mean-HU calculation against a structure of
   known, uniform composition first (e.g. contour a small sphere in a water-equivalent
   region and confirm you get ~0 HU, or contour a phantom insert of known HU). If the
   number comes back wildly wrong (e.g. air/background values for a structure you know is
   metal), swap the two indices used when reading `buffer[i, j]` and when allocating it.

The mean-HU routine restricts the voxel search to each slice's contour bounding box (with a
1-pixel margin) rather than scanning the whole image, for performance, then tests true
containment with an in-process point-in-polygon test against the structure's own contour
points.

## Reference tables (`Models/MaterialReferenceData.cs`)

Transcribed from the site's own scanner characterisation tables.

**Stainless steel (HU)**

| Dia (mm) | 700mm FOV | 550mm FOV | 400mm FOV |
|---:|---:|---:|---:|
| 4  | 13243 | 21325 | 23983 |
| 6  | 15691 | 20786 | 21824 |
| 8  | 16093 | 19587 | 20034 |
| 10 | 16040 | 18218 | 18632 |
| 12 | 15349 | 17197 | 17476 |
| 14 | 14964 | 16230 | 16530 |
| 16 | 13763 | 14773 | 14989 |

**Titanium (HU)**

| Dia (mm) | 700mm FOV | 550mm FOV | 400mm FOV |
|---:|---:|---:|---:|
| 4  | 7075 | 11393 | 12812 |
| 6  | 8383 | 11104 | 11659 |
| 8  | 8597 | 10464 | 10703 |
| 10 | 8569 | 9733  | 9954  |
| 12 | 8200 | 9187  | 9336  |
| 14 | 7994 | 8670  | 8831  |
| 16 | 7352 | 7892  | 8007  |

These are only valid for the scanner/reconstruction/HU-calibration setup they were measured
on. To re-characterise on a different scanner or kernel, update the arrays in
`MaterialReferenceData.cs` - nothing else needs to change, since `BilinearInterpolator` reads
the axis arrays' lengths directly.

**Interpolation & decision logic** (`Models/BilinearInterpolator.cs`,
`Models/ImplantMaterialClassifier.cs`):

- Standard bilinear interpolation (two nested linear interpolations across diameter, then
  FOV), so an exact hit on a tabulated diameter/FOV reproduces the table value exactly.
- Diameters or FOVs outside the tabulated range are **clamped to the nearest edge**, not
  extrapolated, and this is flagged as a warning in the result - linear extrapolation of a
  CT-number-vs-size curve isn't reliable outside the characterised range.
- The material whose interpolated HU is numerically closer to the measured mean HU is
  reported as the best match. If that best-match difference still exceeds the **1000 HU**
  tolerance, the result is reported as **Indeterminate** rather than forcing a guess, with a
  warning explaining why (bad contour, metal artefact, wrong diameter entered, or a material
  outside these two tables).
- The 1000 HU tolerance is a named constant
  (`ImplantMaterialClassifier.DefaultAgreementToleranceHu`) and is also an optional parameter
  on `Classify(...)`, so it can be loosened/tightened per call without touching the logic.

## Running without Eclipse

`Views/MainWindow.xaml.cs` constructs `new StaEsapiPatientService()` in its constructor. Swap
that for `new FakeEsapiPatientService()` to click through the whole flow (patient → structure
set → structure → calculate) with canned data while setting up ESAPI access or demoing the
tool. `FakeEsapiPatientService` has no real ESAPI objects, so it doesn't need (and doesn't
use) the dedicated STA thread.
