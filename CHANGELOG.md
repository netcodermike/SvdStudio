# Changelog

## Public-sharing preparation - 2026-11-03

- Assemble the WPF app, numerical core, and test runner in a common repository root.
- Add a solution, .NET 8 SDK selection, ignore rules, MIT license, Windows CI workflow, and publication guide.
- Replace machine-specific README instructions and unsupported precision claims.
- Document the implemented rank threshold, energy conventions, and image studio limits.
- Reject non-finite matrix entries and invalid custom rank tolerances at the solver boundary.
- Throw on iteration-limit exhaustion instead of returning an unconverged factorization.
- Restrict geometric visualization to 2×2 matrices; the previous larger-matrix view mixed a top-left submatrix ellipse with singular vectors of the full matrix.
- Correct wording in the in-app theory reference and qualify report export behavior.
- Extend verification from 114 to 251 assertions, covering zero matrices, single-row/single-column matrices, invalid inputs, all four Penrose identities on rank-deficient rectangular examples, and the truncated-SVD tail error formula.

These changes are in the prepared copy. The original source folders were not edited.
