// ConsoleKeyInfo to the library's Key. The SharpOS map works from set-1 scan
// codes because the kernel reports those; System.Console has already done that
// translation, so this is a table over ConsoleKey plus the character.

using System;

namespace Terminal.Gui
{
    internal static class ConsoleKeyMap
    {
        public static KeyEvent ToKeyEvent(ConsoleKeyInfo info)
        {
            bool shift = (info.Modifiers & ConsoleModifiers.Shift) != 0;
            bool alt = (info.Modifiers & ConsoleModifiers.Alt) != 0;
            bool ctrl = (info.Modifiers & ConsoleModifiers.Control) != 0;

            Key key = Translate(info, shift, ctrl);

            if (shift && key < Key.CharMask) key |= Key.ShiftMask;
            if (alt) key |= Key.AltMask;

            return new KeyEvent(key, new KeyModifiers { Shift = shift, Ctrl = ctrl, Alt = alt });
        }

        private static Key Translate(ConsoleKeyInfo info, bool shift, bool ctrl)
        {
            switch (info.Key)
            {
                case ConsoleKey.Escape: return Key.Esc;
                case ConsoleKey.Backspace: return Key.Backspace;
                case ConsoleKey.Tab: return shift ? Key.BackTab : Key.Tab;
                case ConsoleKey.Enter: return Key.Enter;

                case ConsoleKey.UpArrow: return Key.CursorUp;
                case ConsoleKey.DownArrow: return Key.CursorDown;
                case ConsoleKey.LeftArrow: return Key.CursorLeft;
                case ConsoleKey.RightArrow: return Key.CursorRight;
                case ConsoleKey.Home: return Key.Home;
                case ConsoleKey.End: return Key.End;
                case ConsoleKey.PageUp: return Key.PageUp;
                case ConsoleKey.PageDown: return Key.PageDown;
                case ConsoleKey.Insert: return Key.InsertChar;
                case ConsoleKey.Delete: return Key.DeleteChar;

                case ConsoleKey.F1: return Key.F1;
                case ConsoleKey.F2: return Key.F2;
                case ConsoleKey.F3: return Key.F3;
                case ConsoleKey.F4: return Key.F4;
                case ConsoleKey.F5: return Key.F5;
                case ConsoleKey.F6: return Key.F6;
                case ConsoleKey.F7: return Key.F7;
                case ConsoleKey.F8: return Key.F8;
                case ConsoleKey.F9: return Key.F9;
                case ConsoleKey.F10: return Key.F10;
                case ConsoleKey.F11: return Key.F11;
                case ConsoleKey.F12: return Key.F12;
            }

            char ch = info.KeyChar;
            if (ch == 0) return Key.Unknown;

            // Ctrl+letter already arrives as the control code; say so explicitly
            // so a Quit binding on Key.C | CtrlMask matches.
            if (ctrl && ch >= 1 && ch <= 26) return (Key)ch | Key.CtrlMask;

            return (Key)ch;
        }
    }
}
