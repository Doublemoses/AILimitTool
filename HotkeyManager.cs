using AILimitTool;
using Microsoft.VisualBasic.FileIO;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using System.Xml.Linq;
using static AILimitTool.AILimitLink;
using static AILimitTool.MainWindow;

namespace AILimitTool
{
    class HotkeyManager : IDisposable
    {
        static MainWindow? mainWindow;

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private static LowLevelKeyboardProc _proc = HookCallback;
        private static IntPtr _hookID = IntPtr.Zero;
        private bool disposed = false;

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll")]
        static extern int GetForegroundWindow();

        [DllImport("user32.dll")]
        static extern int GetWindowText(int hWnd, StringBuilder text, int count);

        [Flags]
        public enum Modifiers
        {
            None    = 0,
            Control = 1,
            Alt     = 2,
            Shift   = 4,
        }

        public HotkeyManager(MainWindow mainoptions)
        {
            mainWindow = mainoptions;
            LoadHotkeys();
            _hookID = SetHook(_proc);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposed)
            { return; }

            if (disposing)
            {

            }

            disposed = true;
        }

        //test stuff
        private static IntPtr SetHook(LowLevelKeyboardProc proc)
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule)
            {
                return SetWindowsHookEx(WH_KEYBOARD_LL, proc, GetModuleHandle(curModule.ModuleName), 0);
            }
        }

        static public Dictionary<(int, ModifierKeys), GameOptions> hotkeyList = new Dictionary<(int, ModifierKeys), GameOptions>(); // (hotkey, modifiers), option

        public Dictionary<(int, ModifierKeys), GameOptions> GetHotkeyDictionary()
        {
            return hotkeyList;
        }

        Dictionary<string, GameOptions> textCompare = new Dictionary<string, GameOptions>()
        {
            { "Immortal",           GameOptions.Immortal },
            { "LockTargetHP",       GameOptions.LockTargetHP },
            { "LockSync",           GameOptions.LockSync },
            { "PlayerSpeed",        GameOptions.PlayerSpeed },
            { "TargetInfo",         GameOptions.TargetInfo },
            { "InfiniteDew",        GameOptions.InfiniteDew },
            { "AddCrystals",        GameOptions.AddCrystals },
            { "TeleportQucksave",   GameOptions.TeleportQucksave },
            { "TeleportQuickload",  GameOptions.TeleportQuickload },
            { "NextTab",            GameOptions.NextTab }
        };

        // Improve this, for now use
        // https://learn.microsoft.com/en-us/windows/win32/inputdev/virtual-key-codes
        public void LoadHotkeys()
        {
            try
            {
                string line;
                var assembly = Assembly.GetExecutingAssembly();
                string resource = Assembly.GetExecutingAssembly().GetManifestResourceNames().Single(str => str.EndsWith("hotkeys.tsv"));

                using (StreamReader sr = new StreamReader(assembly.GetManifestResourceStream(resource)))
                {
                    while ((line = sr.ReadLine()) != null)
                    {
                        string[] split = line.Split("\t");
                        int hotkey = 0;
                        int modifiers = 0;
                        GameOptions option = 0;

                        if (split.Length > 1)
                        {

                            if (split[1] != "")
                            {
                                hotkey = Convert.ToInt32(split[1], 16);

                                if (split.Length == 3)
                                {
                                    try { modifiers = Convert.ToInt32(split[2]); }
                                    catch { }
                                }
                                if (textCompare.TryGetValue(split[0], out option))
                                    hotkeyList.TryAdd(((int)hotkey, (ModifierKeys)modifiers), option);
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                MessageBox.Show("item id list fail?");
            }

        }



        public (int hotkey, ModifierKeys) GetOptionHotkey(uint option)
        {
            (int key, ModifierKeys modifier) returnHotkey;


            /*if (hotkeyList.ContainsKey(option))
            {
                hotkeyList.TryGetValue(option, out returnHotkey);
                return returnHotkey;
            }*/
            return (0, ModifierKeys.None);
        }

        private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode < 0) { return CallNextHookEx(_hookID, nCode, wParam, lParam); }

            if (nCode >= 0 && wParam == (IntPtr)WM_KEYDOWN)
            {
                int handle = GetForegroundWindow();
                StringBuilder sb = new StringBuilder(7);
                GetWindowText(handle, sb, 11);

                int vkCode = Marshal.ReadInt32(lParam);

                int modifiers = GetCurrentModifiers();

                if (sb.ToString() == "AILIMIT")
                {
                    GameOptions option;

                    if (hotkeyList.TryGetValue((vkCode, (ModifierKeys)modifiers), out option))
                        mainWindow.ActionHotkey((uint)option);
                }

            }
            return CallNextHookEx(_hookID, nCode, wParam, lParam);
        }

        private static int GetCurrentModifiers()
        {
            int modifiers = 0;
            if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
                modifiers += (int)ModifierKeys.Shift;
            if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
                modifiers += (int)ModifierKeys.Control;
            if (Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt))
                modifiers += (int)ModifierKeys.Alt;
            return modifiers;
        }
    }
}
