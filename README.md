# ImageShot

Screenshots in every program on Windows – plus a browser extension for full web pages.

| Part | Folder | Tech |
|---|---|---|
| Windows app | `windows/` | C# / WinForms, .NET Framework 4.8 (built into Windows 10/11) |
| Browser extension | `extension/` | Chrome/Edge/Ecosia, Manifest V3 |

## Build & test

Every push is built and tested on real Windows by GitHub Actions (`.github/workflows/ci.yml`):

* `dotnet build` + xUnit tests (stitching, HDR conversion, file names)
* `ImageShot.exe --selftest` – runs capture, save, clipboard, stitching and opens every window inside the real program
* Playwright tests for the extension in Chromium

Locally: `dotnet build windows/src/ImageShot/ImageShot.csproj` and `dotnet test windows/tests/ImageShot.Tests`.

See `docs/PROGRESS.md` for the roadmap and status.
