using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using FalloutRemake.Import;

namespace FalloutRemake.Runtime;

/// <summary>Audited first-entry Speech XP and karma/reputation subset, neutral character below the first level threshold.</summary>
public sealed class ArroyoRewardProfile
{
    public const int BaseExperience = 600;
    public const string MapHash = "0EDCDFF2AFB6FAC7E8203CE9EAE8BA4663D37F3BE112D3EF4713AF3093D8D52A";
    public const string ProgramHash = "DB21D0CEA597C67D8597D7B98FEB252823DDF27D747FE9C710DB17720E319D48";
    public string Fingerprint { get; }
    public string EntryGlobalsFingerprint => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Fingerprint + "|entry-globals-neutral-v1")));
    public SavedArroyoGlobals EntryGlobals() => new(EntryGlobalsFingerprint,15,65,41);
    internal ArroyoRewardProfile() => Fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
        MapHash + "|" + ProgramHash + "|" + TempleSpeechProfile.GlobalHash + "|arroyo-first-entry-speech-neutral-v1")));
    internal bool Matches(MapDocument map) => map.Source is { Path:"maps/arvillag.map",Size:216200,Sha256:MapHash }
        && map.Prefix.Header.MapIndex == 4 && map.Prefix.Header.ScriptIndex == 27 && map.MapProgram?.Sha256 == ProgramHash;
    public static ArroyoRewardProfile Create(MapDocument map,Func<string,GameResource> read)
    {
        var profile = new ArroyoRewardProfile();
        var program = IntProgram.Read(read("scripts/arvillag.int"));
        var globals = GameGlobalFile.Read(read("data/vault13.gam"));
        var metadata = ScriptListMetadata.Read(read("scripts/scripts.lst"),26);
        if (!profile.Matches(map) || program.Source is not {Size:5188,Sha256:ProgramHash}
            || globals.Source.Sha256 != TempleSpeechProfile.GlobalHash || globals.Entries[10].Value != 0
            || globals.Entries[0].Value != 0 || globals.Entries[47].Value != 50 || Enumerable.Range(37,9).Any(i => globals.Entries[i].Value != 0)
            || metadata.Source.Sha256 != TempleInitialGateProfile.ListHash || metadata.ProgramPath != "scripts/arvillag.int")
            throw new InvalidDataException("Unknown Arroyo first-entry reward sources.");
        void Span(int start,params (ushort,int?)[] expected)
        {
            if (!program.Instructions.Where(i => i.Offset >= start).Take(expected.Length).Select(i => (i.Opcode,i.OperandBits)).SequenceEqual(expected))
                throw new InvalidDataException("Arroyo first-entry reward instructions changed.");
        }
        Span(2370,(0xC001,3318),(0xC001,14),(0xC001,0),(0x810B,null),(0x802F,null));
        Span(2440,(0xC001,0),(0xC001,0),(0x80C5,null),(0xC001,15),(0x8039,null),(0x80C6,null));
        for (int i=0;i<9;i++) Span(2488+14*i,(0xC001,37+i),(0xC001,0),(0x80C6,null));
        // The neutral profile yields karma 15: all four positive tiers fail, then Wanderer succeeds.
        for (int i=0;i<4;i++) Span(2614+46*i,(0xC001,2660+46*i),(0xC001,0),(0x80C5,null),
            (0xC001,1000-250*i),(0x8036,null),(0x802F,null),(0xC001,37+i),(0xC001,1),(0x80C6,null),(0xC001,3004),(0x8004,null));
        Span(2798,(0xC001,2846),(0xC001,0),(0x80C5,null),(0xC001,250),(0x8046,null),(0x8038,null),(0x802F,null),
            (0xC001,41),(0xC001,1),(0x80C6,null),(0xC001,3004),(0x8004,null));
        Span(3004,(0xC001,47),(0xC001,47),(0x80C5,null),(0xC001,15),(0x8039,null),(0x80C6,null));
        Span(3164,(0xC001,3270),(0xC001,10),(0x80C5,null),(0xC001,2),(0x8033,null),(0x802F,null),
            (0xC001,300),(0xC001,300),(0x8039,null),(0xC001,0),(0x8031,null),(0xC001,0),(0x8032,null),(0x80A1,null));
        return profile;
    }
}
