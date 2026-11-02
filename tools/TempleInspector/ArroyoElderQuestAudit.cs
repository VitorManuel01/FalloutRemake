using FalloutRemake.Import;
using FalloutRemake.Runtime;

/// <summary>Read-only audit of Node015, Node007, Node016 and post-quest Node004 (L7==1).</summary>
internal static class ArroyoElderQuestAudit
{
    public static object Create(GameData data,MapDocument map,IntProgram program)
    {
        var questProfile=ArroyoElderQuestProfile.Create(map,data.Read);
        var actor=map.AllObjects.Single(o=>o.Offset==126588);
        var protoCatalog=new PrototypeCatalog(new ResourceCatalog(data.Read));
        var caps=protoCatalog.Get(ArroyoElderQuestProfile.CapsPid);
        var flask=protoCatalog.Get(ArroyoElderQuestProfile.FlaskPid);
        var directPreQuest=questProfile.ProjectFirst(new[] {11868,16348});
        var directAccept=questProfile.ProjectFirst(new[] {11836,17934});
        return new
        {
            Status="ARROYO_ELDER_QUEST_REMINDER_AUDIT_OK",
            questProfile.Fingerprint,
            Items=new
            {
                Caps=new {caps.Pid,caps.Type,caps.Subtype,caps.Fid,caps.ScriptId,caps.BaseWeight,caps.InventoryFid,caps.MessageId,caps.Resource,
                    ElderInventoryKey=questProfile.CapsItemKey,ElderOffset=ArroyoElderQuestProfile.ElderCapsOffset,ElderQuantity=ArroyoElderQuestProfile.ElderCapsAmount},
                Flask=new {flask.Pid,flask.Type,flask.Subtype,flask.Fid,flask.ScriptId,flask.BaseWeight,flask.InventoryFid,flask.MessageId,flask.Resource,
                    CreatedBy="create_object_sid(267, 0, 0, -1) at 17682..17700; add_mult_objs_to_inven(dude_obj, item, 1) at 17702..17734",
                    Quantity=ArroyoElderQuestProfile.FlaskQuantity},
                ElderInventory=actor.Inventory.Select(e=>new {e.Quantity,e.Object.Key,e.Object.Offset,e.Object.Header.Id,e.Object.Header.Pid,e.Object.Header.Sid}).ToArray()
            },
            Procedures=new
            {
                Node015=new {Index=31,BodyOffset=17626,ReplyMessage=159,SetsLocal7=1,SetsGlobal452=2,SetsGlobal619=1,
                    Options=new[] {(17870,160,"Node007",true),(17902,161,"Node009",false),(17934,162,"Node016",true)}},
                Node007=new {Index=23,BodyOffset=15222,ReplyMessage=131,ReactionDelta=-10,SetsGlobal452=2,SetsGlobal619=1,
                    Options=new[] {(15794,133,"Node009",false),(15826,134,"Node016",true)}},
                Node016=new {Index=32,BodyOffset=17960,ClosingMessage=163,Opcode="0x8120 (gsay_message)",SetsGlobal452=2,SetsGlobal619=1},
                Node009Boundary=new {Index=25,BodyOffset=16374,OutOfRangeLocal=10,DeclaredLocals=10,BlockedChoiceOffsets=new[] {13942,15794,17902}},
                Node014Note="Procedure 30 (offset 17568) is the hostile float_msg(155..158) branch, not a quest dialogue node"
            },
            FirstTalkProjections=new
            {
                DirectAccept=directAccept,
                DirectAcceptWithReminder=questProfile.ProjectFirst(new[] {11836,17870,15826}),
                Node006Accept=questProfile.ProjectFirst(new[] {11804,14632,17934}),
                Node006AcceptWithReminder=questProfile.ProjectFirst(new[] {11804,14632,17870,15826}),
                Node013Accept=questProfile.ProjectFirst(new[] {11804,14696,17542,17934}),
                Node013AcceptWithReminder=questProfile.ProjectFirst(new[] {11804,14696,17542,17870,15826})
            },
            RepeatTalkProjections=new
            {
                PreQuestAccept=questProfile.ProjectRepeat(directPreQuest,new[] {13870,17934}),
                PreQuestAcceptWithReminder=questProfile.ProjectRepeat(directPreQuest,new[] {13870,17870,15826}),
                PreQuestNode013Accept=questProfile.ProjectRepeat(directPreQuest,new[] {13902,17542,17934}),
                PreQuestNode013AcceptWithReminder=questProfile.ProjectRepeat(directPreQuest,new[] {13902,17542,17870,15826}),
                PreQuestReminderOnly=questProfile.ProjectRepeat(directPreQuest,new[] {13974,15826}),
                PostQuestOpening=questProfile.ProjectRepeat(directAccept,Array.Empty<int>()),
                PostQuestImmediateExit=questProfile.ProjectRepeat(directAccept,new[] {14006}),
                PostQuestReminder=questProfile.ProjectRepeat(directAccept,new[] {13974,15826})
            },
            AppliedToWorld=false
        };
    }
}
