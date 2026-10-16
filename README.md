# ImageShot

Screenshots in every program on Windows – plus a browser extension for full web pages.

| Part | Folder | Tech |
|---|---|---|
| Windows app | `windows/` | C# / WinForms, .NET Framework 4.8 (built into Windows 10/11) |
| Browser extension | `extension/` | Chrome/Edge, Manifest V3 |
| Installer | `installer/` | Inno Setup 6, per-user, no admin rights |

## Features

**Windows app** – PrtScn opens a toolbar: region, window, screen or all screens. Push the edge of a selection to
scroll and stitch long content. Works with HDR, mixed DPI, multiple monitors and full-screen games (DirectX).
Editor with arrow, rectangle, ellipse, highlighter, text, step numbers, blur/pixelate and crop; copy text (OCR,
offline); pin to screen; recent captures; delayed capture; file-name templates; light/dark; da/en/de/es.

**Extension** – visible area, region (with scrolling), full page (also scroll boxes), all tabs; crop and draw
before copying or saving; da/en/de/es.

## Build, test, release

Every push is built and tested on real Windows by GitHub Actions (`.github/workflows/ci.yml`):

* `dotnet build` + xUnit tests (stitching, HDR conversion, file names, update parsing)
* `ImageShot.exe --selftest` – capture, DirectX, clipboard, stitching, editor, OCR, history, keyboard hook, and
  screenshots of every window (pushed to branch `ci-shots-windows`)
* installer + portable zip + extension zip (`build/package.ps1`), silent install/uninstall test; files on branch `ci-build`
* Playwright tests for the extension in Chromium

Release: bump `<Version>` in `windows/src/ImageShot/ImageShot.csproj` and `extension/manifest.json`, add
`docs/releases/<version>.md`, then `git tag v<version> && git push origin v<version>` (`.github/workflows/release.yml`).
Code signing switches on automatically when the secrets in `docs/SIGNING.md` exist.

Store texts: `docs/store/`. Privacy policy: `docs/PRIVACY.md`. Roadmap and log: `docs/PROGRESS.md`.
