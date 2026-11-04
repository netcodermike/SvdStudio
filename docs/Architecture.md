# Architecture

## Projects and dependencies

`SvdStudio` targets `net8.0-windows` with WPF enabled. It references `SvdStudio.Core`, which targets `net8.0` without UI dependencies. `SvdStudio.TestRunner` is a `net8.0` console application that references the core. The inspected projects contain no third-party NuGet package references.

## MVVM organization

- **Model:** `Matrix`, `MatrixParser`, `PresetMatrices`, `SvdAlgorithm`, `SvdResult`, and `TruncatedSvdResult` implement numerical behavior.
- **View models:** `MainViewModel` coordinates input and results. `MatrixDisplayViewModel` prepares matrix cells and export commands. `TruncatedSvdViewModel`, `Geometric2DViewModel`, and `ImageCompressionViewModel` hold tab state.
- **Views:** `MainWindow.xaml`, reusable matrix/plot controls, and converters render the state.
- **Infrastructure:** `ObservableObject` implements property notifications; `RelayCommand` implements command binding.

`MainWindow.xaml.cs` initializes the view and sets `DataContext`. Custom-control code-behind handles drawing and layout. The project follows MVVM, but view models also use WPF types, clipboard access, and file/message dialogs. They are not a platform-independent presentation layer, and there is no dependency-injection composition root.

## Computation path

1. A command parses input into a real `Matrix`.
2. `SvdAlgorithm.Decompose` validates finite entries and optional rank tolerance.
3. Tall/square inputs are bidiagonalized directly; wide inputs are transposed and their factors mapped back.
4. Householder transformations are accumulated into U and V; implicit shifted QR steps produce singular values and vectors.
5. `SvdResult` computes rank, condition estimate, reconstruction, and orthogonality diagnostics.
6. The main view model creates display models, the pseudoinverse, spectrum items, and approximation controls, then notifies the UI.

Image approximation has its own matrix and SVD state. Moving its rank slider reuses the decomposition.

## Current engineering limits

Commands execute synchronously. There is no cancellation or progress reporting. Dense matrix storage and explicit full factors limit practical sizes. The reported compute time stops before construction of `SvdResult`, so it excludes diagnostics and subsequent display preparation. The console runner exercises core behavior, not WPF bindings or dialogs.
