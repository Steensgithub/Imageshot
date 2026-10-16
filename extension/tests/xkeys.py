"""Press a key combination with the X11 XTest extension, e.g.  python3 xkeys.py Alt_L+Shift_L+s

Used by the tests to trigger the extension's keyboard shortcuts with real input events
(Playwright's synthetic key events never reach Chrome's shortcut handling).
Needs libX11 and libXtst (present wherever Xvfb/Chromium run) and $DISPLAY.
"""
import ctypes
import ctypes.util
import sys
import time


def load(name, fallback):
    return ctypes.cdll.LoadLibrary(ctypes.util.find_library(name) or fallback)


def main(combo):
    x11 = load('X11', 'libX11.so.6')
    xtst = load('Xtst', 'libXtst.so.6')
    x11.XOpenDisplay.restype = ctypes.c_void_p
    x11.XOpenDisplay.argtypes = [ctypes.c_char_p]
    x11.XStringToKeysym.restype = ctypes.c_ulong
    x11.XStringToKeysym.argtypes = [ctypes.c_char_p]
    x11.XKeysymToKeycode.argtypes = [ctypes.c_void_p, ctypes.c_ulong]
    x11.XFlush.argtypes = [ctypes.c_void_p]
    x11.XCloseDisplay.argtypes = [ctypes.c_void_p]
    xtst.XTestFakeKeyEvent.argtypes = [ctypes.c_void_p, ctypes.c_uint, ctypes.c_int, ctypes.c_ulong]

    display = x11.XOpenDisplay(None)
    if not display:
        sys.exit('xkeys: cannot open X display (run under xvfb-run)')
    codes = []
    for name in combo.split('+'):
        sym = x11.XStringToKeysym(name.encode())
        code = x11.XKeysymToKeycode(display, sym) if sym else 0
        if not code:
            sys.exit('xkeys: unknown key ' + name)
        codes.append(code)
    for code in codes:
        xtst.XTestFakeKeyEvent(display, code, 1, 0)
        x11.XFlush(display)
        time.sleep(0.03)
    for code in reversed(codes):
        xtst.XTestFakeKeyEvent(display, code, 0, 0)
        x11.XFlush(display)
        time.sleep(0.03)
    x11.XCloseDisplay(display)


if __name__ == '__main__':
    if len(sys.argv) != 2:
        sys.exit(__doc__)
    main(sys.argv[1])
