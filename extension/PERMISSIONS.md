# ImageShot – permissions

ImageShot asks for as little as possible. It has **no permanent access to websites**. It may only
touch a page after you start a capture yourself, and only that one tab.

## Required permissions

| Permission | Why it is needed |
|---|---|
| `activeTab` | Gives temporary access to the **current tab only**, and only after you start a capture: a toolbar/popup click, a context-menu item or a keyboard shortcut (Alt+Shift+S / Alt+Shift+A). ImageShot needs it to call `chrome.tabs.captureVisibleTab` and to inject the selection overlay and scroll helpers with `chrome.scripting.executeScript`. The access ends when the tab navigates or closes. Chrome shows no install warning for it. |
| `scripting` | Runs the capture helpers in the page: the "Select area" overlay, scrolling for "Full page" (window or inner scroll box, scroll-snap off, hiding sticky/fixed bars) and putting the page back afterwards. Without host access it only works on the tab that `activeTab` unlocked. |
| `contextMenus` | Adds the right-click menu **ImageShot → Visible area / Select area / Full page / All tabs**. |
| `storage` | Keeps your settings in `chrome.storage.sync` (resolution, format, after-capture mode, auto close, "All tabs" scope, download folder). It also keeps a short-lived note in `chrome.storage.session` of which clipboard window shows which capture, so that capture can be deleted when the window closes. |
| `downloads` | Saves images to `Downloads/<folder>/` with `chrome.downloads.download`. It is used by "Save as file" in the clipboard window (blob URL) and by "Save directly" (data URL from the service worker). |
| `clipboardWrite` | Lets the clipboard window put the image on the system clipboard, including "Copy directly", which copies as soon as the window opens without another click. |

## Optional permission (asked for at runtime)

| Permission | Why it is needed |
|---|---|
| `<all_urls>` (optional host permission) | Needed **only for "All tabs"**. That mode switches through your other tabs and captures each one, and `activeTab` only covers the tab you are on. Chrome asks the first time you choose "All tabs": straight from the popup click, or from a small ImageShot window when you start it from the context menu (a service worker cannot show the prompt itself). If you say no, nothing is captured. You can remove the permission at any time under `chrome://extensions` → ImageShot → Details → Site access. |

## Permissions that were removed in 2.0

* **`<all_urls>` host permission (always on)**: replaced by `activeTab`, with `<all_urls>` kept only as the optional permission above. This removes the "Read and change all your data on all websites" install warning.
* **`tabs`**: no longer needed. `chrome.tabs.query/get/update` work without it. The title and URL of the current tab come with `activeTab`. For "All tabs", the granted host permission already exposes each tab's title, which is used for the file names. Pages Chrome never lets extensions capture (e.g. `chrome://`) are reported as "Unknown tab". Dropping it removes the "Read your browsing history" warning.

## Data

Captures are kept in the extension's own IndexedDB (not synced, never uploaded) only while the
clipboard window is open. They are deleted when it closes, and anything older than one hour is
purged whenever the extension starts. ImageShot makes no network requests.
