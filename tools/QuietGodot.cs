using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

// Process isolation for validation on Windows. No desktop switch or input injection.
public static class QuietGodot
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct StartupInfo
    {
        public int cb; public string reserved, desktop, title;
        public int x, y, width, height, charsX, charsY, fill, flags;
        public short show, reservedBytes; public IntPtr reservedData, input, output, error;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessInfo { public IntPtr process, thread; public int processId, threadId; }
    [StructLayout(LayoutKind.Sequential)]
    private struct SecurityAttributes { public int length; public IntPtr descriptor; [MarshalAs(UnmanagedType.Bool)] public bool inherit; }
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateDesktopW(string name, IntPtr device, IntPtr mode, int flags, uint access, IntPtr security);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool CloseDesktop(IntPtr desktop);
    [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr OpenInputDesktop(int flags, bool inherit, uint access);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool GetUserObjectInformationW(IntPtr handle, int index, StringBuilder value, int length, out int needed);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateFileW(string name, uint access, uint share, ref SecurityAttributes security, uint creation, uint flags, IntPtr template);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CreateProcessW(string app, StringBuilder command, IntPtr processSecurity, IntPtr threadSecurity,
        bool inherit, uint flags, IntPtr environment, string directory, ref StartupInfo startup, out ProcessInfo process);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool CloseHandle(IntPtr handle);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern uint WaitForSingleObject(IntPtr handle, uint milliseconds);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool GetExitCodeProcess(IntPtr handle, out uint code);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool TerminateProcess(IntPtr handle, uint code);

    public sealed class Result
    {
        public string Desktop, InputDesktopBefore, InputDesktopAfter, StandardOutput, StandardError;
        public int ProcessId, ExitCode; public long ElapsedMilliseconds; public bool TimedOut;
    }
    private static string InputDesktopName()
    {
        IntPtr handle = OpenInputDesktop(0, false, 1); // DESKTOP_READOBJECTS.
        if (handle == IntPtr.Zero) return "unavailable";
        try
        {
            var name = new StringBuilder(256); int needed;
            return GetUserObjectInformationW(handle, 2, name, name.Capacity * 2, out needed) ? name.ToString() : "unavailable";
        }
        finally { CloseDesktop(handle); }
    }
    private static string Quote(string argument)
    {
        if (argument.IndexOfAny(new[] { '\0', '\r', '\n' }) >= 0) throw new ArgumentException("Invalid process argument.");
        var result = new StringBuilder("\""); int slashes = 0;
        foreach (char c in argument)
        {
            if (c == '\\') { slashes++; continue; }
            if (c == '"') { result.Append('\\', slashes * 2 + 1); result.Append(c); }
            else { result.Append('\\', slashes); result.Append(c); }
            slashes = 0;
        }
        result.Append('\\', slashes * 2); result.Append('"'); return result.ToString();
    }
    private static IntPtr FileHandle(string path, bool output)
    {
        var security = new SecurityAttributes { length = Marshal.SizeOf(typeof(SecurityAttributes)), inherit = true };
        IntPtr handle = CreateFileW(path, output ? 0x40000000u : 0x80000000u, 3, ref security, output ? 2u : 3u, 0x80, IntPtr.Zero);
        if (handle == new IntPtr(-1)) throw new Win32Exception(Marshal.GetLastWin32Error());
        return handle;
    }
    public static Result Run(string executable, string directory, string[] arguments, string output, string error, int timeoutSeconds)
    {
        if (Environment.OSVersion.Platform != PlatformID.Win32NT) throw new PlatformNotSupportedException("Windows only.");
        executable = Path.GetFullPath(executable); directory = Path.GetFullPath(directory);
        if (!File.Exists(executable) || !Directory.Exists(directory) || timeoutSeconds < 1 || timeoutSeconds > 900)
            throw new ArgumentException("Executable, project directory or timeout is invalid.");
        var result = new Result { Desktop = "CodexFallout-" + Guid.NewGuid().ToString("N"), InputDesktopBefore = InputDesktopName(),
            StandardOutput = Path.GetFullPath(output), StandardError = Path.GetFullPath(error) };
        // READOBJECTS | CREATEWINDOW | ENUMERATE | WRITEOBJECTS; no SWITCHDESKTOP permission.
        IntPtr desktop = CreateDesktopW(result.Desktop, IntPtr.Zero, IntPtr.Zero, 0, 0xC3, IntPtr.Zero);
        if (desktop == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error(), "Cannot create isolated desktop. Nothing launched.");
        IntPtr input = IntPtr.Zero, stdout = IntPtr.Zero, stderr = IntPtr.Zero; ProcessInfo process = new ProcessInfo();
        var watch = Stopwatch.StartNew();
        try
        {
            stdout = FileHandle(result.StandardOutput, true); stderr = FileHandle(result.StandardError, true); input = FileHandle("NUL", false);
            var startup = new StartupInfo { cb = Marshal.SizeOf(typeof(StartupInfo)), desktop = "WinSta0\\" + result.Desktop,
                flags = 0x100, input = input, output = stdout, error = stderr };
            var command = new StringBuilder(Quote(executable));
            foreach (string argument in arguments) command.Append(' ').Append(Quote(argument));
            // CREATE_NO_WINDOW affects the console; the private desktop isolates Godot's GUI windows.
            if (!CreateProcessW(executable, command, IntPtr.Zero, IntPtr.Zero, true, 0x08004000, IntPtr.Zero, directory, ref startup, out process))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Cannot launch on isolated desktop. No normal-desktop fallback.");
            result.ProcessId = process.processId;
            uint waited = WaitForSingleObject(process.process, (uint)timeoutSeconds * 1000);
            if (waited == 0x102)
            {
                result.TimedOut = true;
                if (!TerminateProcess(process.process, 124)) throw new Win32Exception(Marshal.GetLastWin32Error());
                WaitForSingleObject(process.process, 5000);
            }
            else if (waited != 0) throw new Win32Exception(Marshal.GetLastWin32Error());
            uint exitCode;
            if (!GetExitCodeProcess(process.process, out exitCode)) throw new Win32Exception(Marshal.GetLastWin32Error());
            result.ExitCode = unchecked((int)exitCode);
            result.ElapsedMilliseconds = watch.ElapsedMilliseconds; result.InputDesktopAfter = InputDesktopName();
            return result;
        }
        finally
        {
            if (process.process != IntPtr.Zero)
            {
                uint pending;
                if (GetExitCodeProcess(process.process, out pending) && pending == 259)
                { TerminateProcess(process.process, 124); WaitForSingleObject(process.process, 5000); }
            }
            if (process.thread != IntPtr.Zero) CloseHandle(process.thread);
            if (process.process != IntPtr.Zero) CloseHandle(process.process);
            if (input != IntPtr.Zero) CloseHandle(input);
            if (stdout != IntPtr.Zero) CloseHandle(stdout);
            if (stderr != IntPtr.Zero) CloseHandle(stderr);
            CloseDesktop(desktop);
        }
    }
}
