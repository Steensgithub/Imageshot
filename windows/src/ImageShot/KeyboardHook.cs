using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace ImageShot
{
    // Fanger PrtScn-genveje, før Windows ser dem. Nødvendigt på Windows 11, hvor PrtScn som standard
    // åbner Skærmklip (RegisterHotKey fejler så). Kører i sin egen tråd med egen beskedløkke, så
    // tastaturet aldrig hakker, mens ImageShot arbejder.
    sealed class KeyboardHook : IDisposable
    {
        const int WH_KEYBOARD_LL = 13, WM_KEYDOWN = 0x100, WM_KEYUP = 0x101, WM_SYSKEYDOWN = 0x104, WM_SYSKEYUP = 0x105, WM_QUIT = 0x12;
        const int VK_SNAPSHOT = 0x2C, VK_SHIFT = 0x10, VK_CONTROL = 0x11, VK_MENU = 0x12, VK_LWIN = 0x5B, VK_RWIN = 0x5C;

        delegate IntPtr HookProc(int code, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        struct KBDLLHOOKSTRUCT { public int vkCode, scanCode, flags, time; public IntPtr extra; }

        [StructLayout(LayoutKind.Sequential)]
        struct MSG { public IntPtr hwnd; public int message; public IntPtr wParam, lParam; public int time, x, y; }

        [DllImport("user32.dll", SetLastError = true)] static extern IntPtr SetWindowsHookEx(int id, HookProc proc, IntPtr hMod, uint threadId);
        [DllImport("user32.dll")] static extern bool UnhookWindowsHookEx(IntPtr h);
        [DllImport("user32.dll")] static extern IntPtr CallNextHookEx(IntPtr h, int code, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")] static extern int GetMessage(out MSG m, IntPtr hwnd, uint min, uint max);
        [DllImport("user32.dll")] static extern bool PostThreadMessage(uint threadId, int msg, IntPtr w, IntPtr l);
        [DllImport("user32.dll")] static extern short GetAsyncKeyState(int vk);
        [DllImport("kernel32.dll")] static extern uint GetCurrentThreadId();
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern IntPtr GetModuleHandle(string name);

        readonly Control invoker;                 // tråden, der skal udføre optagelsen (UI-tråden)
        readonly Action<int> onHotkey;            // modtager tilstandsnummer (1..4)
        volatile Dictionary<Keys, int> map = new Dictionary<Keys, int>(); // genvej → tilstand
        HookProc proc;                            // holdes i live (ellers fjerner GC den)
        IntPtr hook;
        uint threadId;
        Thread thread;
        bool swallowUp;                           // slug også "tast sluppet" for en fanget PrtScn

        public bool Active { get { return hook != IntPtr.Zero; } }

        public KeyboardHook(Control invoker, Action<int> onHotkey)
        {
            this.invoker = invoker;
            this.onHotkey = onHotkey;
        }

        // Kun genveje med PrtScn håndteres her; andre klares af RegisterHotKey.
        public void SetHotkeys(Dictionary<Keys, int> hotkeys)
        {
            map = new Dictionary<Keys, int>(hotkeys);
        }

        public bool Start()
        {
            if (thread != null) return Active;
            using (ManualResetEvent ready = new ManualResetEvent(false))
            {
                thread = new Thread(delegate ()
                {
                    threadId = GetCurrentThreadId();
                    proc = Callback;
                    using (Process p = Process.GetCurrentProcess())
                    using (ProcessModule m = p.MainModule)
                        hook = SetWindowsHookEx(WH_KEYBOARD_LL, proc, GetModuleHandle(m.ModuleName), 0);
                    if (hook == IntPtr.Zero) ErrorLog.Info("Tastaturhook kunne ikke installeres: " + Marshal.GetLastWin32Error());
                    ready.Set();
                    MSG msg;
                    while (GetMessage(out msg, IntPtr.Zero, 0, 0) > 0) { }
                    if (hook != IntPtr.Zero) UnhookWindowsHookEx(hook);
                    hook = IntPtr.Zero;
                });
                thread.IsBackground = true;
                thread.Name = "ImageShot tastaturhook";
                thread.Start();
                ready.WaitOne(3000);
            }
            return Active;
        }

        static bool Down(int vk) { return (GetAsyncKeyState(vk) & 0x8000) != 0; }

        IntPtr Callback(int code, IntPtr wParam, IntPtr lParam)
        {
            if (code >= 0)
            {
                KBDLLHOOKSTRUCT k = (KBDLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(KBDLLHOOKSTRUCT));
                int msg = wParam.ToInt32();
                if (k.vkCode == VK_SNAPSHOT && (k.flags & 0x10) == 0) // 0x10 = indsprøjtet input (ignoreres)
                {
                    if (msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN)
                    {
                        if (!Down(VK_LWIN) && !Down(VK_RWIN)) // Win+PrtScn er Windows' egen genvej
                        {
                            Keys key = Keys.PrintScreen;
                            if (Down(VK_CONTROL)) key |= Keys.Control;
                            if (Down(VK_MENU)) key |= Keys.Alt;
                            if (Down(VK_SHIFT)) key |= Keys.Shift;
                            int mode;
                            if (map.TryGetValue(key, out mode))
                            {
                                swallowUp = true;
                                try { invoker.BeginInvoke(onHotkey, mode); } catch { }
                                return new IntPtr(1); // Windows får ikke tasten (Skærmklip åbner ikke)
                            }
                        }
                    }
                    else if ((msg == WM_KEYUP || msg == WM_SYSKEYUP) && swallowUp)
                    {
                        swallowUp = false;
                        return new IntPtr(1);
                    }
                }
            }
            return CallNextHookEx(hook, code, wParam, lParam);
        }

        public void Dispose()
        {
            if (thread == null) return;
            PostThreadMessage(threadId, WM_QUIT, IntPtr.Zero, IntPtr.Zero);
            thread.Join(2000);
            thread = null;
        }
    }
}
