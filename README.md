# SvdStudio

[![Build and verify](https://github.com/netcodermike/SvdStudio/actions/workflows/build.yml/badge.svg)](https://github.com/netcodermike/SvdStudio/actions/workflows/build.yml)

Explore singular value decomposition in a Windows desktop application built with WPF and .NET 8.

SvdStudio decomposes a real matrix as **A = U Σ Vᵀ**, then connects the factors to singular-value plots, low-rank approximations, pseudoinverses, 2D geometry, and grayscale image approximation. The numerical engine is written in C# and has no WPF dependency.

## Quick start

On Windows, install the [.NET 8 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/8.0). An IDE is optional. Run these commands from the repository root:

```powershell
git clone https://github.com/netcodermike/SvdStudio.git
cd SvdStudio
dotnet restore SvdStudio.sln
dotnet build SvdStudio.sln -c Release --no-restore
dotnet run --project SvdStudio/SvdStudio.csproj -c Release --no-build
```

Try `[3 0; 0 1]`: its singular values are 3 and 1, its determinant magnitude is 3, and its rank-1 approximation retains 90% of its squared Frobenius energy. Open the 2D tab to see the unit circle become an ellipse.

## Features

| Area | What you can do |
| --- | --- |
| Input | Paste whitespace, CSV, TSV, MATLAB-style rows, or numeric nested arrays; import a text file; select presets; generate random matrices. |
| Factorization | Switch between economy and full SVD; inspect A, U, Σ, V, Vᵀ, and the reconstruction in heatmaps. |
| Diagnostics | Inspect numerical rank, condition estimate, norms, determinant magnitude for square matrices, reconstruction error, and orthogonality errors. |
| Spectrum | Explore singular values, squared-energy contributions, cumulative retained energy, and the pseudoinverse. |
| Low-rank approximation | Change k, inspect the approximation and residual, and explore individual rank-1 terms. |
| 2D geometry | For a 2×2 matrix, inspect the circle-to-ellipse transformation and singular directions. |
| Image studio | Approximate procedural patterns or imported grayscale images with a selectable rank. |
| Export | Copy individual matrices as CSV, LaTeX, MATLAB, or JSON; save a text decomposition report. |

For general matrices, squared singular-value energy is not automatically statistical variance explained. The PCA interpretation requires appropriate data centering and conventions.

## Input examples

All of these describe the same matrix:

```text
1 2 3
4 5 6
```

```text
1,2,3
4,5,6
```

```text
[1 2 3; 4 5 6]
```

```json
[[1, 2, 3], [4, 5, 6]]
```

Use decimal points and equal row lengths. The parser accepts numeric array syntax but is not a general JSON or quoted-CSV validator. The solver rejects NaN and infinity.

## Documentation

- [Functionality and theory guide (PDF)](docs/SvdStudio-Functionality-and-Theory.pdf)
- [Editable guide source](docs/Functionality-and-Theory.md)
- [Architecture](docs/Architecture.md)
- [Validation results and limitations](docs/Validation.md)
- [GitHub publication guide](docs/GitHub-Publishing.md)
- [Changes made during preparation](CHANGELOG.md)

## Repository layout

```text
SvdStudio.sln
SvdStudio/             WPF views, controls, converters, and view models
SvdStudio.Core/        Matrix operations, parsing, SVD, and derived results
SvdStudio.TestRunner/  Console verification suite
docs/                 User guide, theory, validation, and publication instructions
.github/workflows/    Windows build and numerical verification
```

The three projects must remain siblings because the application and test runner reference `../SvdStudio.Core/SvdStudio.Core.csproj`.

## Numerical behavior

The engine uses Householder bidiagonalization and implicit shifted QR iteration. Wide matrices are handled through transposition. This is a managed educational implementation; it does not call LAPACK.

For p = min(m,n), economy SVD returns U of size m×p, Σ of size p×p, and Vᵀ of size p×n. Full SVD returns m×m, m×n, and n×n respectively. Economy mode keeps p columns even when numerical rank is lower.

The current default rank threshold is:

```text
tau = max(m,n) * 2.2204460492503131e-16 * max(sigma_max, 1)
rank = count(sigma_i > tau)
```

The absolute floor means very small, otherwise full-rank matrices can be classified as rank zero. The core API accepts an explicit non-negative finite `customTolerance`; the UI uses the default. The same threshold controls pseudoinverse truncation and whether the condition estimate is reported as infinity.

Accuracy depends on scale, dimensions, and conditioning. No universal absolute error below 1e-14 is promised. Inspect residuals and rescale inputs when appropriate. Extreme finite magnitudes can still overflow or underflow intermediate calculations.

## Verify

```powershell
dotnet run --project SvdStudio.TestRunner/SvdStudio.TestRunner.csproj -c Release
```

Local preparation on November 3, 2026: **251 assertions passed, 0 failed**. The original suite had 114 assertions. This is a console test runner: use `dotnet run`, not `dotnet test`. The GitHub workflow builds the solution and executes this runner on Windows. Its first hosted run occurs after publication.

## Image studio limits

Procedural images are 48×48. Imported images request a decode width of 64 pixels and are converted to 8-bit grayscale. The displayed storage comparison counts `k(m+n+1)` factor values versus `mn` pixel values; it does not measure PNG/JPEG bytes, metadata, or floating-point storage costs. At high k the factors may contain more values than the original matrix. The studio previews approximations and does not write a compressed image file.

Calculations run synchronously; large inputs can make the UI unresponsive. The random generator limits each dimension to 30, but pasted input has no comparable size limit.

## License

[MIT](LICENSE). See [reference and provenance notes](docs/References.md) for numerical background and the limits of the source review.
