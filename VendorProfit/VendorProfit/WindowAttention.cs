using System.Runtime.InteropServices;

namespace VendorProfit;

public static partial class WindowAttention
{
    [LibraryImport("user32.dll")]
    private static partial nint GetForegroundWindow();

    [LibraryImport("user32.dll")]
    private static partial uint GetWindowThreadProcessId(nint window, out uint process);

    public static uint? ForegroundProcess()
    {
        nint window = GetForegroundWindow();
        if (window == 0) return null;
        uint thread = GetWindowThreadProcessId(window, out uint process);
        // Check this call's result directly, not a potentially stale Win32 last-error value.
        return thread != 0 && process != 0 ? process : null;
    }

    public static bool FlashIfInactive(uint gameProcess, Func<uint?> foregroundProcess, Action flash)
    {
        uint? active = foregroundProcess();
        if (gameProcess == 0 || active is null or 0 || active == gameProcess) return false;
        flash();
        return true;
    }
}
