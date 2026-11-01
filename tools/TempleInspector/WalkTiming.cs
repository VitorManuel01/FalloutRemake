using System.Buffers.Binary;
using FalloutRemake.Import;

/// <summary>
/// Game time a walked hex costs the dude, from the original walk FRM and CE _object_move: one frame per 1000 / fps ms; each
/// frame adds its offset, and the dude enters the next hex once the offset passes the hex step (_off_tile: 16/32 px across,
/// dword_51D984: 12 px down), keeping the remainder. Outside combat game time runs at 1 tick per 100 ms (_script_chk_timed_events).
/// </summary>
internal static class WalkTiming
{
    static readonly int[] StepX = { 16, 32, 16, -16, -32, -16 }, StepY = { -12, 0, 12, 12, 0, -12 };

    public sealed record Timing(string Art, int FramesPerSecond, int FramesPerDirection, double MillisecondsPerHex, double TicksPerHex);

    /// <summary>Tribal dude art (CE art.cc defaults): hmwarr (male) and hfprim (female); walk is "a" (no weapon) + "b" (ANIM_WALK).</summary>
    public static Timing ForGender(GameData data, int gender) => Measure(data, gender == 1 ? "hfprim" : "hmwarr");

    public static Timing Measure(GameData data, string art)
    {
        byte[] frm = data.Read($"art/critters/{art}ab.frm").Bytes.ToArray();
        int U16(int at) => BinaryPrimitives.ReadUInt16BigEndian(frm.AsSpan(at, 2));
        int I16(int at) => BinaryPrimitives.ReadInt16BigEndian(frm.AsSpan(at, 2));
        int fps = U16(4) is int f and > 0 ? f : 10, frames = U16(8);
        if (frames < 1) throw new InvalidDataException($"{art}ab.frm has no frames.");
        double totalFrames = 0, totalHexes = 0;
        for (int rotation = 0; rotation < 6; rotation++)
        {
            // Per-frame offsets of this direction: each frame is width, height, size, offset x, offset y, then its pixels.
            int at = 62 + (int)BinaryPrimitives.ReadUInt32BigEndian(frm.AsSpan(34 + 4 * rotation, 4));
            var dx = new int[frames]; var dy = new int[frames];
            for (int frame = 0; frame < frames; frame++)
            {
                int size = (int)BinaryPrimitives.ReadUInt32BigEndian(frm.AsSpan(at + 4, 4));
                dx[frame] = I16(at + 8); dy[frame] = I16(at + 10); at += 12 + size;
            }
            // A long straight walk: count frames until 60 hexes are entered.
            int x = 0, y = 0, hexes = 0, calls = 0, current = 0;
            while (hexes < 60 && calls < 100000)
            {
                if (calls > 0) current = (current + 1) % frames;
                calls++; x += dx[current]; y += dy[current];
                int sx = StepX[rotation], sy = StepY[rotation];
                if (sx > 0 && sx <= x || sx < 0 && sx >= x || sy > 0 && sy <= y || sy < 0 && sy >= y) { x -= sx; y -= sy; hexes++; }
            }
            if (hexes < 60) throw new InvalidDataException($"{art}ab.frm never leaves its hex in direction {rotation}.");
            totalFrames += calls; totalHexes += hexes;
        }
        double ms = totalFrames / totalHexes * (1000 / fps);
        return new(art, fps, frames, Math.Round(ms, 1), Math.Round(ms / 100, 2));
    }
}
