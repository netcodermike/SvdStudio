# Publish SvdStudio on GitHub

The prepared folder is a complete repository candidate. It has not been uploaded, and it contains no Git history. Use its root (the folder containing `SvdStudio.sln` and `README.md`), not just the inner WPF project.

## 1. Review locally

Open the README, license, PDF, and validation notes. Run:

```powershell
dotnet restore SvdStudio.sln
dotnet build SvdStudio.sln -c Release --no-restore
dotnet run --project SvdStudio.TestRunner/SvdStudio.TestRunner.csproj -c Release --no-build
dotnet run --project SvdStudio/SvdStudio.csproj -c Release --no-build
```

The owner selected MIT. The license credits “SvdStudio contributors”; you may replace that holder with your preferred copyright name before the first commit.

## 2. Create a local commit

Install [Git for Windows](https://git-scm.com/downloads/win) if necessary. Open PowerShell in the prepared root:

```powershell
git init -b main
git add .
git status --short
git diff --cached --stat
git commit -m "Initial public version of SvdStudio"
```

Check the staged list: all three projects, docs, solution, license, ignore rules, and workflow should be present; `bin`, `obj`, and personal files should be absent. If Git requests a name/email, set your preferred commit identity. A GitHub-provided no-reply email can keep your personal email out of commit metadata.

## 3. Choose one publication route

### GitHub CLI

With [GitHub CLI](https://cli.github.com/) installed:

```powershell
gh auth login
gh repo create SvdStudio --public --source=. --remote=origin --push --description "Interactive WPF studio for singular value decomposition, low-rank approximation, and matrix geometry."
```

The second command creates a public repository and uploads the committed files. If the repository name is already taken in your account, select another name.

### GitHub website plus Git

Create a new public repository named `SvdStudio` on GitHub. Leave automatic README, license, and .gitignore initialization unchecked because the local project already contains them. Copy the repository's HTTPS URL, then run the following after replacing `YOUR-ACCOUNT`:

```powershell
git remote add origin https://github.com/YOUR-ACCOUNT/SvdStudio.git
git push -u origin main
```

Authenticate through Git's credential manager when prompted. Do not put access tokens in repository files or remote URLs. The CLI and website routes are alternatives; do not create the same repository twice.

## 4. Check the public result

Confirm the README and PDF links work, the MIT license is detected, and the Actions tab's **Build and verify** run passes. Suggested topics: `wpf`, `csharp`, `linear-algebra`, `svd`, `numerical-methods`, `education`.

## 5. Optional downloadable Windows build

After the manual smoke checklist passes, publish a framework-dependent build:

```powershell
dotnet publish SvdStudio/SvdStudio.csproj -c Release --self-contained false -o artifacts/win-framework-dependent
Compress-Archive -Path artifacts/win-framework-dependent/* -DestinationPath artifacts/SvdStudio-Windows.zip
```

Users need the .NET 8 Desktop Runtime. Keep all published files together. This is not a self-contained installer, and no signed binary or installer was prepared here. Create a release on GitHub (for example `v0.1.0`) and attach the ZIP and guide PDF after checking them. Build output belongs in release assets, not source control.

These commands follow [GitHub's official local-code publication guide](https://docs.github.com/en/migrations/importing-source-code/using-the-command-line-to-import-source-code/adding-locally-hosted-code-to-github).
