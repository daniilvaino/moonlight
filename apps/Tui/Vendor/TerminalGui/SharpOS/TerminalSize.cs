// Where the screen size comes from — the one thing SharpOSDriver cannot get the
// same way on both hosts. Output is ANSI through Console.Write either way, so
// this is the entire difference between running on the kernel and on a terminal.

using System;
#if SHARPOS
using SharpOS.AppSdk;
#endif

namespace Terminal.Gui
{
    internal static class TerminalSize
    {
        public static bool TryGet(out int cols, out int rows)
        {
#if SHARPOS
            return AppConsole.TryGetSize(out cols, out rows);
#else
            // COLUMNS/LINES win: the usual way to state a size when the console
            // cannot be asked for one, and what makes a redirected run testable.
            if (FromEnvironment(out cols, out rows)) return true;

            try
            {
                cols = Console.WindowWidth;
                rows = Console.WindowHeight;
            }
            catch (Exception)
            {
                // Redirected output, or no console at all.
                cols = 0;
                rows = 0;
            }

            return cols > 0 && rows > 0;
        }

        private static bool FromEnvironment(out int cols, out int rows)
        {
            cols = 0;
            rows = 0;

            return int.TryParse(Environment.GetEnvironmentVariable("COLUMNS"), out cols)
                && int.TryParse(Environment.GetEnvironmentVariable("LINES"), out rows)
                && cols > 0 && rows > 0;
#endif
        }
    }
}
