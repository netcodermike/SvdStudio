# References and provenance notes

The documentation explains standard mathematics in original wording. References supply background; they are not dependencies or endorsements.

- [LAPACK Users' Guide: Singular Value Decomposition](https://www.netlib.org/lapack/lug/node32.html) - factorization and application background.
- [LAPACK Users' Guide: Further Details on SVD Error Bounds](https://www.netlib.org/lapack/lug/node97.html) - why accuracy should be described relative to matrix scale and dimensions. Those bounds do not independently certify this implementation.
- [Ankur Moitra, Algorithmic Aspects of Machine Learning, MIT course notes](https://ocw.mit.edu/courses/18-409-algorithmic-aspects-of-machine-learning-spring-2015/2af5365a3f0d24cc2ee9f787bbab14e9_MIT18_409S15_bookex.pdf) - Theorem 2.1.2 on low-rank approximation.
- [JAMA, NIST and The MathWorks](https://math.nist.gov/javanumerics/jama/) - a public-domain numerical reference implementation. The solver's variable names and four-case QR iteration structure resemble JAMA's SVD implementation. This is an inference from source structure; the available files do not establish the actual derivation history. No JAMA code was downloaded into this package during preparation.
- [GitHub: adding locally hosted code](https://docs.github.com/en/migrations/importing-source-code/using-the-command-line-to-import-source-code/adding-locally-hosted-code-to-github) - publication workflow.
- [Official setup-dotnet action](https://github.com/actions/setup-dotnet) - SDK setup in CI.

The repository uses the MIT license selected by the owner. That choice does not establish ownership of any previously incorporated third-party material. Preserve any applicable original notices if provenance is later identified. The inspected project files contain no external NuGet package references.
