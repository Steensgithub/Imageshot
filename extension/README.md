# ImageShot – browser extension

Screenshots for Chrome, Edge, Ecosia and other Chromium browsers. Capture the **visible area**, a
**selected area**, the **full page** or **all tabs**, then copy the image to the clipboard or save it as a file.
The extension is part of **ImageShot** (Windows app + browser extension).

Languages: English, Dansk, Deutsch, Español. The browser's language is used automatically.
Other languages: [Dansk](README.da.md) · [Deutsch](README.de.md) · [Español](README.es.md)

## Install

1. Unzip the archive so you have the `extension` folder.
2. Type `chrome://extensions` in the address bar (or Menu → Extensions → Manage extensions).
3. Turn on **Developer mode** (top right).
4. Click **Load unpacked** and choose the `extension` folder.
5. Click the puzzle icon in the toolbar and pin **ImageShot**.
   Leave the folder where it is: the extension runs from it.

## Use

* **Right-click** a page → **ImageShot** → *Visible area* · *Select area* · *Full page* · *All tabs*
* Or click the **ImageShot icon** in the toolbar and choose the capture there.
* **Shortcuts:** `Alt+Shift+S` (visible area), `Alt+Shift+A` (select area).
  You can change them under `chrome://extensions/shortcuts`.
* **Select area:** drag with the mouse. `Esc` or a right-click cancels.
  Push the mouse against the top or bottom edge while dragging and the page (or the web app's scroll box)
  scrolls by itself, so the selection can be taller than the screen. The mouse wheel works too.
  The pieces are stitched into one image.

### Settings (click the icon)

* **Resolution:** the slider controls image size and quality. 70 = 87 % of the screen resolution, quality 85.
* **File format:** WebP (small files), PNG (lossless) or JPEG.
* **After capture:**
  * Show clipboard – choose copy or save
  * Copy straight to the clipboard
  * Save straight to a file
* **Close the clipboard automatically** when the image has been copied or saved. The option is in the settings
  and at the bottom of the clipboard window.
* **“All tabs” includes:** the tabs in the current window or in all windows.
* **Folder in Downloads:** e.g. `ImageShot` (subfolders with `/` are allowed).
  Files are named `ImageShot YYYY-MM-DD HH.mm.ss`.

### Crop

Click **Crop** in the clipboard window and drag a frame around what you want to keep. Then adjust the edges
and corners, or drag inside the frame to move it. `Enter` (or a double-click) applies, `Esc` cancels.
**Undo crop** or `Ctrl+Z` goes back. Cropping always starts from the original image, so there is no extra
quality loss. Copy and Save use the cropped image.

## Tips

* When you copy, the image always goes on the clipboard as PNG, because the clipboard only accepts PNG.
  Saved files use the format you chose.
* "Copy straight to the clipboard" with *All tabs* shows the clipboard window instead, because the
  clipboard can only hold one image at a time.
* *Full page* on web apps where the content scrolls inside a box instead of the window (e.g. Gmail, Saxo,
  Notion): ImageShot finds the box and scrolls through it. The image gets the top bar at the top, the whole
  content of the box and the bottom bar at the bottom. Areas next to the box (e.g. a menu) are included once.
* The first time you use *All tabs*, Chrome asks for access to all websites. ImageShot needs that to capture
  tabs other than the current one. See [PERMISSIONS.md](PERMISSIONS.md).

## Limitations

* The browser's own pages (`chrome://`, the Chrome Web Store, other extensions) cannot be captured.
  The icon then shows a red "!", and hovering it shows the reason.
* *Full page*: fixed top bars are hidden after the first piece so they are not repeated. Very long pages are
  cut off at 30,000 px. WebP is limited to 16,383 px, so longer images are saved as PNG.
* Lists that load more while you scroll (infinite feeds) are captured only as far as they had loaded
  when the capture started.
* *All tabs* briefly switches between the tabs while capturing and then returns to the tab you were on.
  Minimized windows cannot be captured.
* Captures are kept only while the clipboard window is open (and never more than one hour).

## Privacy and permissions

ImageShot works offline and sends nothing anywhere. Captures stay in the extension's local storage until the
clipboard window closes. Details on every permission: [PERMISSIONS.md](PERMISSIONS.md).

## Development

Automated tests (Playwright) are in [`tests/`](tests/README.md). Leave out the `tests/` folder when you
package the extension for the store.
