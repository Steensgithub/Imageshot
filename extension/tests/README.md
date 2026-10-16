# ImageShot extension – tests

End-to-end tests with [Playwright](https://playwright.dev) driving a real (headed) Chromium under Xvfb,
with the unpacked extension loaded from the parent folder.

| Test | What it checks |
|---|---|
| `static-check.js` | No browser. The four locale files have the same keys and placeholders, every key used in the code exists, no hard-coded Danish text, and the manifest rules hold (2.0.0, `default_locale`, no `host_permissions`, `<all_urls>` optional, no `tabs`). Also checks that every permission is documented in `PERMISSIONS.md`. |
| `run.js` | Without a gesture nothing can be captured. *Full page* on `long`, `app` (inner scroll box), `body` (scroll-snap) and `fixed` (fixed header): exact heights, no empty or black rows, uniform row spacing, header only once, and page state restored afterwards. Then end to end: real `Alt+Shift+S` → IndexedDB Blob → clipboard window with an object URL → record deleted when the window closes. Also covers "Save directly" (data URL download) and purging records older than one hour. |
| `crop.js` | Crop editor on a capture seeded through `skbStore`: drag, resize, move, Enter, double-click, Esc, Enter on "Cancel", Ctrl+Z, cropping again from the original, pixel-exact sizes and colours. "Save" downloads from an object URL into `Downloads/ImageShot/`. Also checks that the record is removed on close and the empty state for an unknown id. |
| `region.js` | *Select area*: edge auto-scroll in the window and in a scroll box, mouse wheel, a short selection with an exact size, and a fixed top bar. The stitched image has no seams (uniform row spacing) and does not repeat the fixed bar. |
| `esc.js` | Esc after edge auto-scroll cancels, restores the scroll position, styles and markers, and a new selection can start. |
| `tabs.js` | *All tabs* with host access granted: every tab captured, titles readable without the `tabs` permission, original tab re-activated, and the pending-request hand-over used by the popup and the permission window. |
| `locale.js` | Starts Chromium with `--lang=de`, then `da`, `es` and `en`. Checks the popup (German: "Sichtbarer Bereich", "Auflösung"), the clipboard window, the permission window and the context-menu titles. |

## Run

Requirements: Node 18+, Python 3, `xvfb-run`, and the X11 libraries `libX11`/`libXtst` (present wherever Chromium
runs under Xvfb).

```bash
cd tests
npm install                 # installs playwright 1.48.2
npx playwright install chromium   # only if you have no Chromium of your own
./run-all.sh                # or: npm test
```

`run-all.sh` starts `python3 -m http.server` on port 8765 (`TEST_PORT`) in `tests/site`. It runs `node --check`
on every JS file and the static check, then runs every browser test under
`xvfb-run -a -s "-screen 0 1280x900x24"`. It prints `PASS`/`FAIL` per test and exits non-zero if anything failed.

Environment variables:

* `CHROME_PATH`: the Chromium binary to use. Default: the newest `/opt/pw-browsers/chromium-*/chrome-linux*/chrome`, else Playwright's bundled browser. Branded Google Chrome ignores `--load-extension`, so use Chromium.
* `NODE_PATH`: where `playwright` is, if not installed in `tests/node_modules`.
* `TEST_OUT`: folder for the result images (`full-*.png`, `region-*.png`, `crop-*.png`). Default: `$TMPDIR/imageshot-tests`.

To run one test by hand, start the site server first:

```bash
(cd site && python3 -m http.server 8765 &)
CHROME_PATH=/path/to/chromium xvfb-run -a -s "-screen 0 1280x900x24" node region.js
```

## How the tests get a user gesture

The extension has **no** permanent host access. It relies on `activeTab`, which Chrome grants only after a real
user gesture. Playwright's synthetic key events never reach Chrome's extension shortcuts, so `lib.js` uses
`xkeys.py` to press `Alt+Shift+S` through the X11 XTest extension. That is a genuine shortcut, the same path a user
takes. To grant access without capturing, `grantActiveTab()` briefly stubs the service worker's `run()`. Then
the tests call the capture functions (`captureFull`, `captureRegion`, …) directly in the service worker.

Chrome's permission prompt for the optional `<all_urls>` permission cannot be clicked from automation.
`tabs.js` therefore loads a temporary copy of the extension in which that permission is already granted.

Notes: `page.waitForFunction` is blocked by the CSP of extension pages, so the tests poll with `page.$eval`.
The browser runs headed (`headless: false`, `viewport: null`, `--window-size=1000,700`).
