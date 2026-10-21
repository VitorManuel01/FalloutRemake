using System;
using System.Buffers.Binary;
using System.IO;

namespace FalloutRemake.Import;

/// <summary>Serialized critter PRO fields only. Does not calculate effective stats, load a character or apply combat rules.</summary>
public sealed class CritterPrototypeData
{
    private readonly int[] _baseStats,_bonusStats,_skills;
    public ResourceIdentity Source {get;}
    public int Pid {get;}
    public int HeadFid {get;}
    public int AiPacket {get;}
    public int Team {get;}
    public int Flags {get;}
    public int BodyType {get;}
    public int Experience {get;}
    public int KillType {get;}
    public int? DamageType {get;}
    public int[] BaseStats=>(int[])_baseStats.Clone();
    public int[] BonusStats=>(int[])_bonusStats.Clone();
    public int[] Skills=>(int[])_skills.Clone();
    public int BaseStat(int index)=>index is >=0 and <35 ? _baseStats[index] : throw new InvalidDataException("Stat outside the serialized PRO range.");
    public int BonusStat(int index)=>index is >=0 and <35 ? _bonusStats[index] : throw new InvalidDataException("Stat outside the serialized PRO range.");
    public int Skill(int index)=>index is >=0 and <18 ? _skills[index] : throw new InvalidDataException("Skill outside the serialized PRO range.");
    private CritterPrototypeData(Prototype prototype,byte[] bytes)
    {
        int At(int offset)=>BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(offset,4));
        Source=prototype.Resource; Pid=prototype.Pid; HeadFid=At(32); AiPacket=At(36); Team=At(40); Flags=At(44);
        _baseStats=new int[35]; _bonusStats=new int[35]; _skills=new int[18];
        for (int i=0;i<35;i++) {_baseStats[i]=At(48+4*i);_bonusStats[i]=At(188+4*i);}
        for (int i=0;i<18;i++) _skills[i]=At(328+4*i);
        BodyType=At(400); Experience=At(404); KillType=At(408); DamageType=bytes.Length==416 ? At(412) : null;
    }
    public static CritterPrototypeData Read(Prototype prototype)
    {
        if (prototype==null || prototype.Type!=1 || (prototype.Pid>>24)!=1 || prototype.RawBytes==null)
            throw new InvalidDataException("Expected a serialized critter prototype.");
        byte[] bytes=(byte[])prototype.RawBytes.Clone();
        var resource=new GameResource(prototype.Resource.Path,prototype.Resource.Source,bytes);
        var canonical=PrototypeCatalog.Read(resource,prototype.Pid);
        if (canonical.Resource!=prototype.Resource)
            throw new InvalidDataException("Critter prototype bytes differ from their source fingerprint.");
        return new(canonical,bytes);
    }
}
