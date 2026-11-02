using System.Text.Json;
using FalloutRemake.Geometry;
using FalloutRemake.Import;
using FalloutRemake.Runtime;

internal static class ArroyoElderStateTests
{
    private static int Local(ElderDetachedProjection plan,int index)=>plan.Scripts.Blocks.Single(b=>b.Scope==ScriptVariableScope.ScriptLocal).Values[index];
    private static (WorldSession World,int AdjacentHex,int ExitHex) SessionFixture()
    {
        var (temple,profile,original)=WorldTempleSpeechTests.Fixture();
        var exit=WorldTests.Map(3,4).ObjectLayers[0].Objects[0];
        exit=exit with {Key=temple.Source.Sha256+":422028",ContainerElevation=2,Header=exit.Header with {Tile=13326,Elevation=2},
            State=new Dictionary<string,int> {["destinationMap"]=4,["destinationElevation"]=0,["destinationTile"]=20517,["destinationRotation"]=0}};
        temple=temple with {ObjectLayers=temple.ObjectLayers.Select(l=>l.Elevation==2 ? l with {Objects=l.Objects.Append(exit).ToArray()} : l).ToArray()};
        var village=WorldTests.Map(4,3);
        var template=village.ObjectLayers[0].Objects[0];
        var elder=template with {Key=ArroyoRewardProfile.MapHash+":126588",Offset=126588,State=new Dictionary<string,int>(),
            Header=template.Header with {Id=20,Pid=16777229,Fid=16777247,Tile=18915,Elevation=0,Rotation=2,Sid=67108867,ScriptIndex=10,Flags=536870912}};
        village=village with {
            Source=village.Source with {Path="maps/arvillag.map",Size=216200,Sha256=ArroyoRewardProfile.MapHash},
            Prefix=village.Prefix with {Header=village.Prefix.Header with {ScriptIndex=27,EnteringTile=20517}},
            MapProgram=new("scripts/arvillag.int","synthetic verified profile",5188,ArroyoRewardProfile.ProgramHash),
            ObjectLayers=village.ObjectLayers.Select((l,i)=>l with {Objects=l.Objects.Select(o=>(o.Header.Pid>>24)!=5 ? o : o with {
                Header=o.Header with {Tile=20517},
                State=new Dictionary<string,int> {["destinationMap"]=3,["destinationElevation"]=2,["destinationTile"]=13326,["destinationRotation"]=0}})
                .Concat(i==0 ? new[] {elder} : Array.Empty<MapObject>()).ToArray()}).ToArray(),
            ScriptBindings=new[] {new ScriptBinding(67108867,10,new("scripts/ahelder.int","synthetic",20658,ArroyoElderSource.ProgramHash))}};
        var world=new WorldSession(id=>id==3 ? temple : id==4 ? village : WorldTests.Map(126,3),new[] {3,4,126},
            templeProfile:_=>profile,arroyoProfile:_=>new ArroyoRewardProfile(),elderProfile:_=>new ArroyoElderStateProfile());
        world.Current.Travel.Arrive(original.Location);
        world.CommitTempleSpeech(WorldTempleSpeechTests.Plan(world,profile),false);
        world.Current.Travel.Arrive(new(3,2,13326,0));
        Assert(world.UseExit(false).Status==ExitStatus.Ready && world.CurrentMap==4);
        var nav=world.Current.Doors.Navigation(0);
        int adjacent=Enumerable.Range(0,6).Select(d=>FalloutGrid.Neighbor(18915,d)!.Value).First(h=>nav.Walkable(h) && !nav.IsExit(h));
        world.Current.Travel.Arrive(new(4,0,adjacent,0));
        return (world,adjacent,20517);
    }
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
        check("WorldSession CommitElderDialogue sets GVAR_TALKED_TO_ELDER 531 to 1 records actor reaction locals and owns committed snapshots",()=>
        {
            var profile=new ArroyoElderStateProfile();
            foreach (var (route,reaction,delta) in new[] {
                (new[] {11868,16348},22,-10),
                (new[] {11804,14664,16348},12,-20),
                (new[] {11804,14696,17510},12,-20)})
            {
                var (world,_,_)=SessionFixture();
                Assert(world.TalkedToElder==0 && world.ElderDialogue==null && world.ElderState==null);
                long rev=world.TempleRevision;
                var committed=world.CommitElderDialogue(profile.Project(route),false,world.SessionToken,world.TempleToken);
                Assert(world.TalkedToElder==1 && world.TempleRevision==rev+1
                    && committed.Complete && committed.Procedure=="Node999" && committed.Reaction==reaction
                    && committed.Global(ArroyoElderStateProfile.TalkedToElderGlobal)==1
                    && committed.Local(0)==reaction && committed.Local(1)==1 && committed.Local(2)==1
                    && committed.Local(3)==delta && committed.Local(4)==1 && committed.Local(7)==0);
                string expected=JsonSerializer.Serialize(world.ElderDialogue);
                committed.Choices[0]=-1; committed.Scripts.Blocks[0].Values[0]=999;
                var readBack=world.ElderDialogue!; readBack.Choices[0]=-1; readBack.Scripts.Blocks[0].Values[0]=999;
                Assert(JsonSerializer.Serialize(world.ElderDialogue)==expected);
                world.Reset();
                Assert(world.TalkedToElder==0 && world.ElderDialogue==null);
            }
        });
        check("WorldSession CommitElderDialogue allows only one commit per session and rejects contradictory or violated session states atomically",()=>
        {
            var profile=new ArroyoElderStateProfile();
            var direct=profile.Project(new[] {11868,16348});
            var bridge=profile.Project(new[] {11804,14664,16348});
            var (world,adjacent,exitHex)=SessionFixture();
            byte[] baseline=CheckpointFile.Encode(world.Capture("catalog"));
            void Unchanged()
            {
                Assert(world.TalkedToElder==0 && world.ElderDialogue==null
                    && baseline.SequenceEqual(CheckpointFile.Encode(world.Capture("catalog"))));
            }
            var tamperedGlobal=profile.Project(new[] {11868,16348});
            var globalBlock=tamperedGlobal.Scripts.Blocks.Single(b=>b.Scope==ScriptVariableScope.GameGlobal);
            globalBlock.Values[Array.IndexOf(globalBlock.Indices,531)]=0;
            var tamperedLocal=profile.Project(new[] {11868,16348});
            tamperedLocal.Scripts.Blocks.Single(b=>b.Scope==ScriptVariableScope.ScriptLocal).Values[0]=32;
            foreach (var invalid in new ElderDetachedProjection?[] {
                null,
                profile.Project(Array.Empty<int>()),
                profile.Project(new[] {11804}),
                profile.ProjectRepeat(direct,new[] {14006}),
                direct with {Profile="foreign"},
                direct with {Procedure="Node001"},
                direct with {Complete=false},
                direct with {Reaction=12},
                direct with {Choices=new[] {11804,14664,16348}},
                tamperedGlobal,
                tamperedLocal})
            {
                Reject(()=>world.CommitElderDialogue(invalid!));
                Unchanged();
            }
            Reject(()=>world.CommitElderDialogue(new[] {11804})); Unchanged();
            Reject(()=>world.CommitElderDialogue(direct,moving:true)); Unchanged();
            Reject(()=>world.CommitElderDialogue(direct,sessionToken:Guid.NewGuid())); Unchanged();
            Reject(()=>world.CommitElderDialogue(direct,templeToken:Guid.NewGuid())); Unchanged();
            world.Current.Travel.Arrive(new(4,0,exitHex,0));
            Reject(()=>world.CommitElderDialogue(direct));
            Assert(world.UseExit(false).Status==ExitStatus.Ready && world.CurrentMap==3);
            Reject(()=>world.CommitElderDialogue(direct));
            Assert(world.UseExit(false).Status==ExitStatus.Ready && world.CurrentMap==4);
            world.Current.Travel.Arrive(new(4,0,20100,0));
            Reject(()=>world.CommitElderDialogue(direct));
            world.Current.Travel.Arrive(new(4,0,adjacent,0)); Unchanged();
            var unverified=new WorldSession(_=>world.Current.Map,new[] {4},initialMap:4);
            unverified.Current.Travel.Arrive(new(4,0,adjacent,0));
            Reject(()=>unverified.CommitElderDialogue(direct));
            Assert(unverified.TalkedToElder==0 && unverified.ElderDialogue==null);
            world.CommitElderDialogue(direct);
            string committedState=JsonSerializer.Serialize(world.ElderDialogue);
            long committedRev=world.TempleRevision;
            Reject(()=>world.CommitElderDialogue(direct));
            Reject(()=>world.CommitElderDialogue(bridge));
            Reject(()=>world.CommitElderDialogue(new[] {11868,16348}));
            Assert(world.TalkedToElder==1 && world.TempleRevision==committedRev
                && JsonSerializer.Serialize(world.ElderDialogue)==committedState);
        });
        check("WorldSession CommitElderRepeatDialogue applies repeat Node004 reaction updates and rejects premature duplicate or tampered repeat commits",()=>
        {
            var profile=new ArroyoElderStateProfile();
            foreach (var (firstRoute,firstReaction,repeatRoute,repeatReaction,repeatDelta,repeatBand) in new[] {
                (new[] {11868,16348},22,new[] {14006},22,-10,1),
                (new[] {11868,16348},22,new[] {13902,17510},2,-30,0),
                (new[] {11804,14664,16348},12,new[] {14006},12,-20,1),
                (new[] {11804,14664,16348},12,new[] {13902,17510},-8,-40,0)})
            {
                var (world,adjacent,exitHex)=SessionFixture();
                Reject(()=>world.CommitElderRepeatDialogue(repeatRoute));
                var firstCommitted=world.CommitElderDialogue(firstRoute);
                Assert(world.ElderRepeatDialogue==null && world.ElderState!.Reaction==firstReaction);
                long rev=world.TempleRevision;
                string firstBaseline=JsonSerializer.Serialize(world.ElderDialogue);
                Reject(()=>world.CommitElderRepeatDialogue(Array.Empty<int>()));
                Reject(()=>world.CommitElderRepeatDialogue(new[] {13902}));
                Reject(()=>world.CommitElderRepeatDialogue(new[] {13814}));
                Reject(()=>world.CommitElderRepeatDialogue(firstCommitted));
                Reject(()=>world.CommitElderRepeatDialogue(profile.ProjectRepeat(firstCommitted,repeatRoute) with {Reaction=99}));
                Reject(()=>world.CommitElderRepeatDialogue(repeatRoute,moving:true));
                Reject(()=>world.CommitElderRepeatDialogue(repeatRoute,sessionToken:Guid.NewGuid()));
                Reject(()=>world.CommitElderRepeatDialogue(repeatRoute,templeToken:Guid.NewGuid()));
                world.Current.Travel.Arrive(new(4,0,exitHex,0));
                Reject(()=>world.CommitElderRepeatDialogue(repeatRoute));
                world.Current.Travel.Arrive(new(4,0,adjacent,0));
                Assert(world.ElderRepeatDialogue==null && world.TempleRevision==rev && JsonSerializer.Serialize(world.ElderDialogue)==firstBaseline);
                var repeatCommitted=world.CommitElderRepeatDialogue(repeatRoute,false,world.SessionToken,world.TempleToken);
                Assert(world.TempleRevision==rev+1 && world.TalkedToElder==1
                    && world.ElderDialogue!.Reaction==firstReaction
                    && repeatCommitted.Complete && repeatCommitted.Procedure=="Node999" && repeatCommitted.Reaction==repeatReaction
                    && repeatCommitted.Choices.SequenceEqual(repeatRoute)
                    && repeatCommitted.Local(0)==repeatReaction && repeatCommitted.Local(1)==repeatBand && repeatCommitted.Local(3)==repeatDelta
                    && world.ElderState!.Reaction==repeatReaction && world.ElderState.Local(3)==repeatDelta);
                string expectedRepeat=JsonSerializer.Serialize(world.ElderRepeatDialogue);
                repeatCommitted.Choices[0]=-1; repeatCommitted.Scripts.Blocks[0].Values[0]=999;
                Assert(JsonSerializer.Serialize(world.ElderRepeatDialogue)==expectedRepeat);
                Reject(()=>world.CommitElderRepeatDialogue(repeatRoute));
                Reject(()=>world.CommitElderRepeatDialogue(new[] {14006}));
                world.Reset();
                Assert(world.TalkedToElder==0 && world.ElderDialogue==null && world.ElderRepeatDialogue==null && world.ElderState==null);
            }
        });
    }
    private static void Assert(bool value) {if (!value) throw new Exception("Elder detached state assertion failed.");}
    private static void Reject(Action action) {try {action();} catch (InvalidDataException) {return;} throw new Exception("Invalid elder detached state path accepted.");}
}
