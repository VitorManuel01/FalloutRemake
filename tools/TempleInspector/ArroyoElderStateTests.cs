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
        check("elder repeat Node004 preserves first-talk reaction on immediate exit and applies minus20 through Node013",()=>
        {
            var profile=new ArroyoElderStateProfile();
            var direct=profile.Project(new[] {11868,16348});
            var bridge=profile.Project(new[] {11804,14664,16348});
            var alternative=profile.Project(new[] {11804,14696,17510});
            var directOpening=profile.ProjectRepeat(direct,Array.Empty<int>());
            var directExit=profile.ProjectRepeat(direct,new[] {14006});
            var directNode013=profile.ProjectRepeat(direct,new[] {13902});
            var directNode013End=profile.ProjectRepeat(direct,new[] {13902,17510});
            Assert(directOpening.Procedure=="Node004" && !directOpening.Complete && directOpening.Reaction==22
                && directOpening.Scripts.Blocks[0].Values.SequenceEqual(new[] {22,1,1,-10,1,0,0,0,0,0}));
            Assert(directExit.Complete && directExit.Procedure=="Node999" && directExit.Reaction==22
                && Local(directExit,3)==-10 && Local(directExit,1)==1 && Local(directExit,7)==0);
            Assert(directNode013.Procedure=="Node013" && !directNode013.Complete && directNode013.Reaction==2
                && Local(directNode013,3)==-30 && Local(directNode013,1)==0);
            Assert(directNode013End.Complete && directNode013End.Procedure=="Node999" && directNode013End.Reaction==2
                && directNode013End.Scripts.Blocks[0].Values.SequenceEqual(new[] {2,0,1,-30,1,0,0,0,0,0}));
            foreach (var first in new[] {bridge,alternative})
            {
                var opening=profile.ProjectRepeat(first,Array.Empty<int>());
                var exit=profile.ProjectRepeat(first,new[] {14006});
                var node013End=profile.ProjectRepeat(first,new[] {13902,17510});
                Assert(opening.Procedure=="Node004" && !opening.Complete && opening.Reaction==12
                    && Local(opening,3)==-20 && Local(opening,1)==1 && Local(opening,7)==0);
                Assert(exit.Complete && exit.Procedure=="Node999" && exit.Reaction==12
                    && Local(exit,3)==-20 && Local(exit,1)==1);
                Assert(node013End.Complete && node013End.Procedure=="Node999" && node013End.Reaction==-8
                    && node013End.Scripts.Blocks[0].Values.SequenceEqual(new[] {-8,0,1,-40,1,0,0,0,0,0}));
            }
        });
        check("elder repeat Node004 refuses area-11 post-quest inventory and unbounded quest branches plus incomplete or tampered first talks",()=>
        {
            var profile=new ArroyoElderStateProfile();
            var direct=profile.Project(new[] {11868,16348});
            foreach (var path in new[] {
                new[] {13814}, // 118 -> Node005: guarded by metarule(17, 11) != 0 (Area 11 unknown/0)
                new[] {13870}, // 119 -> Node015: unsupported item/caps/quest write (L7=1, G452=2)
                new[] {13942}, // 120 -> Node009: guarded by L7!=0 and reads out-of-range L10
                new[] {13974}, // 122 -> Node007: writes G452=2 outside neutral globals
                new[] {13902,17542}, // 121 -> Node013 -> 154 -> Node015: unsupported inventory branch
                new[] {17510},new[] {11804},new[] {14006,14006},new[] {13902,14006},new[] {13902,17510,14006},new[] {1,2,3}})
                Reject(()=>profile.ProjectRepeat(direct,path));
            Reject(()=>profile.ProjectRepeat(null!,Array.Empty<int>()));
            Reject(()=>profile.ProjectRepeat(direct,null!));
            Reject(()=>profile.ProjectRepeat(profile.Project(Array.Empty<int>()),Array.Empty<int>()));
            Reject(()=>profile.ProjectRepeat(profile.Project(new[] {11804}),Array.Empty<int>()));
            Reject(()=>profile.ProjectRepeat(profile.ProjectRepeat(direct,new[] {14006}),Array.Empty<int>()));
            Reject(()=>profile.ProjectRepeat(direct with {Profile="foreign"},Array.Empty<int>()));
            Reject(()=>profile.ProjectRepeat(direct with {Reaction=32},Array.Empty<int>()));
            var tamperedL7=profile.Project(new[] {11868,16348}); tamperedL7.Scripts.Blocks[0].Values[7]=1;
            Reject(()=>profile.ProjectRepeat(tamperedL7,Array.Empty<int>()));
            var choices=new[] {13902,17510}; var repeatPlan=profile.ProjectRepeat(direct,choices);
            string before=JsonSerializer.Serialize(profile.ProjectRepeat(direct,choices));
            choices[0]=13814; repeatPlan.Choices[0]=-1; repeatPlan.Scripts.Blocks[0].Values[0]=999;
            Assert(before==JsonSerializer.Serialize(profile.ProjectRepeat(direct,new[] {13902,17510})));
        });
    }
    private static void Assert(bool value) {if (!value) throw new Exception("Elder detached state assertion failed.");}
    private static void Reject(Action action) {try {action();} catch (InvalidDataException) {return;} throw new Exception("Invalid elder detached state path accepted.");}
}
