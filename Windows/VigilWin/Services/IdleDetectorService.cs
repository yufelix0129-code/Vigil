using System.Runtime.InteropServices;

namespace VigilWin.Services;

public sealed class IdleDetectorService
{
    public TimeSpan GetIdleTime()
    {
        var info = new LastInputInfo
        {
            Size = (uint)Marshal.SizeOf<LastInputInfo>()
        };

        if (!GetLastInputInfo(ref info))
        {
            return TimeSpan.Zero;
        }

        var idleMilliseconds = Math.Max(0, Environment.TickCount64 - info.Time);
        return TimeSpan.FromMilliseconds(idleMilliseconds);
    }

    public bool IsIdle(TimeSpan threshold)
    {
        return GetIdleTime() >= threshold;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetLastInputInfo(ref LastInputInfo info);

    [StructLayout(LayoutKind.Sequential)]
    private struct LastInputInfo
    {
        public uint Size;
        public uint Time;
    }
}
