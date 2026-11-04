# Validation record

Prepared November 3, 2026 on Windows x64. These are local results, not hosted CI results.

## Results

| Check | Result |
| --- | --- |
| Original numerical runner | 114 passed, 0 failed |
| Prepared numerical runner | 251 passed, 0 failed |
| Release WPF build | Passed; see final verification below |
| GUI interaction and visual smoke test | Not performed during preparation |
| GitHub Actions hosted run | Pending first push |

The original and initial prepared runs used the installed .NET SDK 10.0.403 while targeting .NET 8. Final repository verification uses the included `global.json` to select the installed .NET 8 SDK; its result is recorded in `verification-summary.txt`.

The runner contains assertions across named scenarios rather than 251 independent test methods. Reconstruction and orthogonality checks generally use an absolute 1e-10 threshold. Coverage includes full/economy dimensions, square/tall/wide matrices, rank deficiency, Hilbert input, deterministic random inputs, parser examples, approximation monotonicity, and pseudoinverse identities.

Added checks exercise zero matrices in both modes, 1×4 and 4×1 shapes, non-finite input rejection, invalid tolerances, rank-deficient rectangular pseudoinverses, theoretical tail residuals, and the default tolerance's absolute floor. The iteration-limit exception was reviewed but no deterministic non-converging finite test case was constructed.

## Limits of the evidence

- Passing examples do not establish correctness for every real matrix or extreme magnitude.
- There is no independent LAPACK/reference-library comparison or performance benchmark.
- Norms, energy sums, determinant products, and intermediate arithmetic may overflow or underflow.
- Relative-error/energy displays use absolute small-value cutoffs; tiny nonzero inputs can show zero relative error or 100% retained energy by convention.
- The parser is permissive and does not implement complete JSON or quoted CSV grammar.
- Summary reports contain formatted text. The misleading CSV filter was removed during preparation; matrix clipboard CSV export is separate.
- No source history was available to establish complete third-party provenance.

## Manual smoke checklist before a binary release

1. Launch on a Windows machine with the .NET 8 Desktop Runtime.
2. Compute `[3 0; 0 1]`; inspect singular values 3 and 1 and the ellipse.
3. Check tall, wide, zero, rank-deficient, and malformed inputs; toggle full/economy mode.
4. Move approximation sliders and inspect residuals and rank-1 modes.
5. Copy each matrix format and import a small text matrix.
6. Load an image; check grayscale rendering and rank changes.
7. Save a text report and open it.
8. Resize the window and inspect all tabs for clipping and binding errors.
