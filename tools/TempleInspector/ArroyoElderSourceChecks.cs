using FalloutRemake.Import;
using FalloutRemake.Runtime;

/// <summary>Real-source refusal checks with private byte copies. Never edits a file in the installation.</summary>
internal static class ArroyoElderSourceChecks
{
    public static object Run(GameData data,MapDocument map)
    {
        var results=new List<object>();
        void Reject(string name,Action create)
        {
            try {create();}
            catch (InvalidDataException error) {results.Add(new {Name=name,Refused=true,Reason=error.Message});return;}
            throw new InvalidDataException("Changed elder source was accepted: "+name);
        }
        GameResource Changed(GameResource resource)
        {
            byte[] copy=resource.Bytes.ToArray();copy[^1]^=1;return resource with {Bytes=copy};
        }
        foreach (string path in new[] {"scripts/ahelder.int","scripts/scripts.lst","data/vault13.gam"})
            Reject(path,()=>ArroyoElderStateProfile.Create(map,p=>p==path ? Changed(data.Read(p)) : data.Read(p)));
        var proto=map.Prototypes.Single(p=>p.Pid==16777229);byte[] original=proto.RawBytes.ToArray();
        var altered=proto.RawBytes.ToArray();altered[48]^=1;
        var candidate=map with {Prototypes=map.Prototypes.Select(p=>p==proto ? p with {RawBytes=altered} : p).ToArray()};
        Reject("changed prototype snapshot",()=>ArroyoElderStateProfile.Create(candidate,data.Read));
        int reads=0;
        Reject("program changes between text validation and state validation",()=>ArroyoElderStateProfile.Create(map,p=>
            p=="scripts/ahelder.int" && ++reads>1 ? Changed(data.Read(p)) : data.Read(p)));
        if (!proto.RawBytes.SequenceEqual(original)) throw new InvalidDataException("Refusal checks changed the original prototype.");
        MapLayoutWriter.Verify(map);
        _=ArroyoElderStateProfile.Create(map,data.Read);
        return new {Status="ARROYO_ELDER_SOURCE_REFUSALS_OK",Count=results.Count,Checks=results,
            OriginalSourcesPreserved=true,FinalOriginalProfileAccepted=true,FilesModified=false};
    }
}
