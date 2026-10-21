using System.Text.Json;
using FalloutRemake.Runtime;

internal static class ArroyoElderStateTests
{
    private static int Local(ElderDetachedProjection plan,int index)=>plan.Scripts.Blocks.Single(b=>b.Scope==ScriptVariableScope.ScriptLocal).Values[index];
    public static void Run(Action<string,Action> check)
    {
        check("detached elder opening writes only its bounded source locals and talk flag with neutral post-entry globals",()=>
        {
            var profile=new ArroyoElderStateProfile(); var initial=profile.CreateInitial().Capture(); var opening=profile.Project(Array.Empty<int>());
            Assert(opening.Procedure=="Node001" && !opening.Complete && opening.Reaction==32
                && opening.Scripts.Blocks[0].Values.SequenceEqual(new[] {32,0,1,0,1,0,0,0,0,0})
                && initial.Blocks[0].Values.All(i=>i==0));
            var game=opening.Scripts.Blocks.Single(b=>b.Scope==ScriptVariableScope.GameGlobal);
            int Global(int i)=>game.Values[Array.IndexOf(game.Indices,i)];
            Assert(Global(531)==1 && Global(619)==1 && Global(10)==2 && Global(0)==15 && Global(47)==65
                && Global(41)==1 && game.Indices.Length==16);
            var storage=profile.CreateInitial();
            Assert(!storage.TryRead(ScriptVariableScope.ScriptLocal,profile.ActorKey,10,out _)
                && storage.Apply(new[] {new ScriptVariableWrite(ScriptVariableScope.ScriptLocal,profile.ActorKey,10,1)},storage.SessionToken,0)==ScriptWriteStatus.OutOfRange);
            var foreign=storage.Capture(); foreign.Blocks[0]=foreign.Blocks[0] with {Key="temple-actor"};
            Reject(()=>storage.Restore(foreign));
        });
        check("elder direct path applies minus10 while Node006a then Node008 applies two adjustments and Node013 minus20",()=>
        {
            var profile=new ArroyoElderStateProfile();
            var direct=profile.Project(new[] {11868,16348});
            var bridge=profile.Project(new[] {11804,14664,16348});
            var alternative=profile.Project(new[] {11804,14696,17510});
            Assert(direct.Complete && direct.Procedure=="Node999" && direct.Reaction==22 && Local(direct,3)==-10 && Local(direct,1)==1);
            Assert(bridge.Complete && bridge.Reaction==12 && Local(bridge,3)==-20 && Local(bridge,1)==1);
            Assert(alternative.Complete && alternative.Reaction==12 && Local(alternative,3)==-20 && Local(alternative,1)==1);
            Assert(profile.Project(new[] {11804}).Reaction==32 && Local(profile.Project(new[] {11804}),1)==0);
        });
        check("elder detached plans reject unavailable out-of-order and post-end choices and own every returned array",()=>
        {
            var profile=new ArroyoElderStateProfile();
            foreach (var path in new[] {new[] {11836},new[] {14696},new[] {11804,14632},new[] {11868,17510},new[] {11868,16348,11868},new[] {1,2,3,4}})
                Reject(()=>profile.Project(path));
            var choices=new[] {11868,16348};var plan=profile.Project(choices);
            string before=JsonSerializer.Serialize(profile.Project(choices));
            choices[0]=11836; plan.Choices[0]=-1;plan.Scripts.Blocks[0].Values[0]=999;
            var independent=profile.CreateInitial();
            independent.Apply(new[] {new ScriptVariableWrite(ScriptVariableScope.ScriptLocal,profile.ActorKey,0,999)},independent.SessionToken,0);
            Assert(before==JsonSerializer.Serialize(profile.Project(new[] {11868,16348})));
            Reject(()=>profile.Project(null!));
        });
    }
    private static void Assert(bool value) {if (!value) throw new Exception("Elder detached state assertion failed.");}
    private static void Reject(Action action) {try {action();} catch (InvalidDataException) {return;} throw new Exception("Invalid elder detached state path accepted.");}
}
