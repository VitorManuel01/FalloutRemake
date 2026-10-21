using System.Buffers.Binary;
using FalloutRemake.Import;

internal static class CritterPrototypeTests
{
    private static Prototype Fixture(int size=416)
    {
        var bytes=new byte[size];
        void Put(int offset,int value)=>BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(offset,4),value);
        Put(0,0x0100000D); Put(8,0x0100001F); Put(28,67108874);
        Put(32,-1); Put(36,3); Put(40,1); Put(44,2);
        for (int i=0;i<35;i++) {Put(48+4*i,100+i);Put(188+4*i,-20+i);}
        for (int i=0;i<18;i++) Put(328+4*i,200+i);
        Put(400,1);Put(404,250);Put(408,2);if (size==416) Put(412,6);
        return PrototypeCatalog.Read(new("proto/critters/synthetic.pro","synthetic",bytes),0x0100000D);
    }
    public static void Run(Action<string,Action> check)
    {
        check("critter PRO reads on-disk header order 35 base plus 35 bonus stats 18 skills and scalar tail in big endian",()=>
        {
            var source=Fixture(); var data=CritterPrototypeData.Read(source);
            Assert(data.Source==source.Resource && data.Pid==0x0100000D && data.HeadFid==-1 && data.AiPacket==3 && data.Team==1 && data.Flags==2);
            Assert(data.BaseStats.SequenceEqual(Enumerable.Range(100,35)) && data.BonusStats.SequenceEqual(Enumerable.Range(-20,35))
                && data.Skills.SequenceEqual(Enumerable.Range(200,18)) && data.BaseStat(3)==103 && data.BonusStat(3)==-17
                && data.BodyType==1 && data.Experience==250 && data.KillType==2 && data.DamageType==6);
        });
        check("412-byte critter PRO preserves absent natural damage word while parsed arrays own their source snapshot",()=>
        {
            var source=Fixture(412); var data=CritterPrototypeData.Read(source);
            source.RawBytes[48]=255; data.BaseStats[3]=999;data.BonusStats[3]=999;data.Skills[0]=999;
            Assert(data.DamageType==null && data.BaseStat(3)==103 && data.BonusStat(3)==-17 && data.Skill(0)==200 && data.KillType==2);
        });
        check("critter PRO rejects changed fingerprint type length identity and stat pseudofields outside the serialized range",()=>
        {
            var source=Fixture(); var data=CritterPrototypeData.Read(source);
            Reject(()=>CritterPrototypeData.Read(source with {Type=0}));
            Reject(()=>CritterPrototypeData.Read(source with {Pid=source.Pid+1}));
            Reject(()=>CritterPrototypeData.Read(source with {Resource=source.Resource with {Sha256="foreign"}}));
            Reject(()=>CritterPrototypeData.Read(source with {RawBytes=source.RawBytes[..411]}));
            source.RawBytes[48]^=1;Reject(()=>CritterPrototypeData.Read(source));
            foreach (int index in new[] {-1,35,int.MaxValue}) {Reject(()=>data.BaseStat(index));Reject(()=>data.BonusStat(index));}
            Reject(()=>data.Skill(18));Reject(()=>data.Skill(-1));
        });
    }
    private static void Assert(bool value) {if (!value) throw new Exception("Critter source data assertion failed.");}
    private static void Reject(Action action) {try {action();} catch (InvalidDataException) {return;} throw new Exception("Invalid critter source data accepted.");}
}
