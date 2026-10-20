using System;
using System.IO;
using System.Threading;

namespace FalloutRemake.Runtime;

internal static class AtomicFileReplacement
{
    // Windows may briefly refuse replacement while another process holds a file handle.
    // Keep the original intact and retry only the rename, for at most 85ms of waiting.
    internal static void Replace(string temporary, string destination, Action<string, string, bool>? move = null, Action<int>? wait = null)
    {
        move ??= File.Move; wait ??= Thread.Sleep;
        int[] delays = { 10, 25, 50 };
        for (int attempt = 0; ; attempt++)
        {
            try { move(temporary, destination, true); return; }
            catch (Exception error) when (attempt < delays.Length
                && error is IOException or UnauthorizedAccessException
                && (error.HResult & 0xFFFF) is 5 or 32 or 33)
            { wait(delays[attempt]); }
        }
    }
}
