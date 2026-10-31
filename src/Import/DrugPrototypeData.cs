using System;
using System.Buffers.Binary;
using System.Linq;

namespace FalloutRemake.Import;

/// <summary>
/// Serialized drug PRO fields (CE protoItemDataRead, ITEM_TYPE_DRUG / ProtoItemDrugData). Three stat slots with an
/// immediate amount and two delayed amounts; durations are game minutes. Stat -1 is an unused slot; stat -2 in the
/// first slot means "random amount between the first two amounts, applied to the second stat" (CE _perform_drug_effect).
/// </summary>
public sealed class DrugPrototypeData
{
    public const int Subtype = 2;
    // CE stat ids used by drug effects.
    public const int UnusedStat = -1, RandomStat = -2, CurrentHitPoints = 35;
    private readonly int[] _stats, _immediate, _first, _second;
    public ResourceIdentity Source { get; }
    public int Pid { get; }
    public int[] Stats => (int[])_stats.Clone();
    public int[] Immediate => (int[])_immediate.Clone();
    public int FirstDelayMinutes { get; }
    public int[] First => (int[])_first.Clone();
    public int SecondDelayMinutes { get; }
    public int[] Second => (int[])_second.Clone();
    public int AddictionChance { get; }
    public int WithdrawalEffect { get; }
    public int WithdrawalOnset { get; }

    private DrugPrototypeData(Prototype prototype)
    {
        byte[] b = prototype.RawBytes;
        int At(int offset) => BinaryPrimitives.ReadInt32BigEndian(b.AsSpan(offset, 4));
        int[] Three(int offset) => Enumerable.Range(0, 3).Select(i => At(offset + 4 * i)).ToArray();
        Source = prototype.Resource; Pid = prototype.Pid;
        _stats = Three(57); _immediate = Three(69);
        FirstDelayMinutes = At(81); _first = Three(85);
        SecondDelayMinutes = At(97); _second = Three(101);
        AddictionChance = At(113); WithdrawalEffect = At(117); WithdrawalOnset = At(121);
    }

    public static DrugPrototypeData Read(Prototype prototype) => new(WeaponPrototypeData.ItemBlock(prototype, Subtype, 125));
}
