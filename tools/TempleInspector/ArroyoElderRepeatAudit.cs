using System.Text;
using FalloutRemake.Import;
using FalloutRemake.Runtime;

/// <summary>Read-only repeat-opening dependencies. Area knowledge is not the world's set of entered MAP IDs.</summary>
internal static class ArroyoElderRepeatAudit
{
    public static object Create(GameData data,IntProgram program)
    {
        if (program.Source.Sha256!=ArroyoElderSource.ProgramHash) throw new InvalidDataException("Unknown elder repeat source.");
        var resource=data.Read("data/city.txt");
        var citySource=ResourceIdentity.From(resource);
        if (citySource is not {Path:"data/city.txt",Size:17983,Sha256:"953B218E60F5BD281A06870D35B13A45CA6953629F325748E55DCF9B03797231"})
            throw new InvalidDataException("Unknown city source for the elder repeat audit.");
        var fields=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase); bool selected=false;
        using var reader=new StringReader(Encoding.Latin1.GetString(resource.Bytes));
        while (reader.ReadLine() is { } raw)
        {
            string line=raw.Split(';',2)[0].Trim();
            if (line.StartsWith('[')) {selected=line.Equals("[Area 11]",StringComparison.OrdinalIgnoreCase);continue;}
            if (!selected || line.Length==0) continue;
            int equals=line.IndexOf('=');
            if (equals<1 || !fields.TryAdd(line[..equals].Trim(),line[(equals+1)..].Trim()))
                throw new InvalidDataException("Ambiguous original area-11 metadata.");
        }
        string Field(string key)=>fields.TryGetValue(key,out var value) ? value : throw new InvalidDataException("Missing area-11 field: "+key);
        void Span(int start,params (ushort,int?)[] expected)
        {
            var found=program.Instructions.Where(i=>i.Offset>=start).Take(expected.Length).ToArray();
            if (found.FirstOrDefault()?.Offset!=start || !found.Select(i=>(i.Opcode,i.OperandBits)).SequenceEqual(expected))
                throw new InvalidDataException($"Elder repeat instructions changed at {start}.");
        }
        Span(9344,(0xC001,11),(0x80BC,null),(0xC001,4),(0xC001,3),(0xC001,19),(0x80DE,null),(0x811C,null),
            (0xC001,9396),(0x800D,null),(0xC001,0),(0xC001,19),(0x8005,null));
        Span(13740,(0xC001,11),(0xC001,117),(0x811E,null));
        Span(13754,(0xC001,13816),(0xC001,17),(0xC001,11),(0x810B,null),(0xC001,0),(0x8034,null),(0x802F,null));
        Span(13816,(0xC001,13912),(0xC001,7),(0x80C1,null),(0xC001,0),(0x8033,null),(0x802F,null));
        var options=IntDialogueOptions.Inspect(program).Where(c=>c.OwningProcedures.Contains(19)).ToArray();
        if (!options.Select(c=>(c.Message,c.TargetName,c.Intelligence,c.MessageList)).SequenceEqual(new[] {
            (118,(string?)"Node005",(int?)4,11),(119,(string?)"Node015",(int?)4,11),(121,(string?)"Node013",(int?)4,11),
            (120,(string?)"Node009",(int?)4,11),(122,(string?)"Node007",(int?)4,11),(123,(string?)"Node999",(int?)4,11)}))
            throw new InvalidDataException("Elder repeat options changed.");
        return new {Status="ARROYO_ELDER_REPEAT_AUDIT_OK",CitySource=citySource,
            Area=new {Index=11,Name=Field("area_name"),StartState=Field("start_state"),InitialVisitedState=0,
                VisitedOrigin="wmAreaSlotInit initializes zero; wmAreaInit does not read a visitedState key from city.txt"},
            NativeMetarule=new {Rule=17,Parameter=11,Handler="wmAreaVisitedState: returns visitedState only when area is visible/known and visited; otherwise 0",
                DistinctFrom="WorldSession.EnteredMaps records MAP IDs; Area 11 is a separate identity and state"},
            Opening=new {Procedure="Node004",ReplyMessage=117,Conditions="village; no refusal or time/IQ override; L4 already 1",Choices=options,
                AreaOption="message118 only if metarule(17,11) != 0",
                BeforeQuest="L7==0: messages119 and121 plus122 and123; 119 reaches item/currency branch Node015",
                AfterQuest="L7!=0: message120 replaces119/121; message118 still depends on area knowledge;122/123 remain"},
            RuntimeEnabled=false,OriginalReconstruction="source-only dependency audit; no world or assets changed"};
    }
}
