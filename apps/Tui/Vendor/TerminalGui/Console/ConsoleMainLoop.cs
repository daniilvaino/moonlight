// Main loop driver for a real terminal. The SharpOS counterpart reads keys from
// the kernel's input service; this one reads them from System.Console. Same
// shape, different source — see SharpOSMainLoop.cs.

using System;
using System.Threading;

namespace Terminal.Gui
{
    public class ConsoleMainLoop : IMainLoopDriver
    {
        private readonly SharpOSDriver _driver;
        private MainLoop _mainLoop = null!;

        private Action<KeyEvent>? _keyHandler;
        private Action<KeyEvent>? _keyDownHandler;
        private Action<KeyEvent>? _keyUpHandler;

        private ConsoleKeyInfo _pending;
        private bool _hasPending;

        private int _lastCols;
        private int _lastRows;

        public ConsoleMainLoop(ConsoleDriver driver)
        {
            _driver = driver as SharpOSDriver
                ?? throw new ArgumentException(
                    "ConsoleMainLoop works with SharpOSDriver.", nameof(driver));
        }

        void IMainLoopDriver.Setup(MainLoop mainLoop)
        {
            _mainLoop = mainLoop;
            TerminalSize.TryGet(out _lastCols, out _lastRows);

            // Ctrl+C is a key here, not a request to die: the application binds it.
            try { Console.TreatControlCAsInput = true; } catch (Exception) { }

            // Without this the box-drawing characters leave as '?' on Windows.
            try { Console.OutputEncoding = System.Text.Encoding.UTF8; } catch (Exception) { }
        }

        void IMainLoopDriver.Wakeup()
        {
            // EventsPending polls rather than blocking; nothing to wake.
        }

        bool IMainLoopDriver.EventsPending(bool wait)
        {
            CheckResize();

            if (_hasPending) return true;
            if (ReadKey()) return true;
            if (_mainLoop.Timeouts.Count > 0 || _mainLoop.IdleHandlers.Count > 0) return true;
            if (!wait) return false;

            Thread.Sleep(10);

            // Re-checked after sleeping: work posted from another thread while we
            // were asleep is ready now, and only looking for keys would leave it
            // sitting until the next turn.
            return _hasPending
                || ReadKey()
                || _mainLoop.Timeouts.Count > 0
                || _mainLoop.IdleHandlers.Count > 0;
        }

        void IMainLoopDriver.MainIteration()
        {
            if (!_hasPending) return;

            ConsoleKeyInfo key = _pending;
            _hasPending = false;

            // System.Console reports presses only; the release is synthesised so
            // the library sees the pair it expects.
            KeyEvent ev = ConsoleKeyMap.ToKeyEvent(key);
            _keyDownHandler?.Invoke(ev);
            _keyHandler?.Invoke(ev);
            _keyUpHandler?.Invoke(ev);
        }

        internal void SetKeyHandlers(Action<KeyEvent> keyHandler,
            Action<KeyEvent> keyDownHandler, Action<KeyEvent> keyUpHandler)
        {
            _keyHandler = keyHandler;
            _keyDownHandler = keyDownHandler;
            _keyUpHandler = keyUpHandler;
        }

        private bool ReadKey()
        {
            // Throws when stdin is redirected; then there are no keys to read.
            try
            {
                if (!Console.KeyAvailable) return false;
            }
            catch (Exception)
            {
                return false;
            }

            _pending = Console.ReadKey(intercept: true);
            _hasPending = true;
            return true;
        }

        private void CheckResize()
        {
            if (!TerminalSize.TryGet(out int cols, out int rows)) return;
            if (cols == _lastCols && rows == _lastRows) return;

            _lastCols = cols;
            _lastRows = rows;
            _driver.OnResized(cols, rows);
        }
    }
}
