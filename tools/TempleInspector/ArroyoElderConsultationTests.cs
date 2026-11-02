using FalloutRemake.Import;
using FalloutRemake.Runtime;

internal static class ArroyoElderConsultationTests
{
    public static void Run(Action<string,Action> check)
    {
        check("elder text consultation follows original edges and refuses unsupported inventory branches without advancing",()=>
        {
            var profile=new ArroyoElderConsultationProfile(); var consultation=new ArroyoElderConsultation(profile);
            Assert(consultation.Current.Reply==103 && !consultation.Choose(11836,0) && consultation.Revision==0);
            Assert(consultation.Choose(11804,0) && consultation.Current.Reply==127 && !consultation.Choose(14632,1));
            Assert(consultation.Choose(14664,1) && consultation.Current.Reply==135 && consultation.Choose(16348,2)
                && consultation.Complete && !consultation.Choose(16348,3));
            var other=new ArroyoElderConsultation(profile);
            Assert(other.Choose(11804,0) && other.Choose(14696,1) && other.Current.Reply==152
                && !other.Choose(17542,2) && other.Choose(17510,2) && other.Complete);
            var direct=new ArroyoElderConsultation(profile); Assert(direct.Choose(11868,0) && direct.Current.Reply==135);
            var repeatDirect=new ArroyoElderConsultation(profile,true);
            Assert(repeatDirect.Repeat && repeatDirect.Procedure=="Node004" && repeatDirect.Current.Reply==117
                && !repeatDirect.Choose(13814,0) && !repeatDirect.Choose(13870,0) && !repeatDirect.Choose(13974,0) && !repeatDirect.Choose(13942,0)
                && repeatDirect.Revision==0 && repeatDirect.Choose(14006,0) && repeatDirect.Complete && repeatDirect.Procedure=="Node999");
            var repeatNode013=new ArroyoElderConsultation(profile,true);
            Assert(repeatNode013.Choose(13902,0) && repeatNode013.Procedure=="Node013" && repeatNode013.Current.Reply==152
                && !repeatNode013.Choose(17542,1) && repeatNode013.Choose(17510,1) && repeatNode013.Complete && repeatNode013.Procedure=="Node999");
        });
        check("elder consultation refuses stale revisions closed drafts foreign choices and mutation of returned metadata",()=>
        {
            var profile=new ArroyoElderConsultationProfile(); var consultation=new ArroyoElderConsultation(profile);
            profile.Node("Node001").Choices[0]=new(11804,1,"End","foreign",0);
            profile.MessageIds[0]=-1;
            Assert(!consultation.Choose(999,0) && !consultation.Choose(11804,1) && consultation.Current.Reply==103);
            Assert(consultation.Choose(11804,0) && !consultation.Choose(14696,0) && consultation.Current.Reply==127);
            consultation.Cancel(); Assert(!consultation.Choose(14696,1) && consultation.Closed);
            Assert(profile.Node("Node001").Choices[0].Next=="Node006" && profile.MessageIds[0]==103);
        });
        check("elder original messages require one actor language complete IDs and owned copies",()=>
        {
            var profile=new ArroyoElderConsultationProfile();
            var entries=profile.MessageIds.ToDictionary(id=>id,id=>new MessageText(id,"","Synthetic "+id));
            var file=new MessageTextFile(new("text/english/dialog/ahelder.msg","synthetic",0,"hash"),entries,0);
            var text=new ArroyoElderText(profile,file,"english"); entries[103]=new(103,"","changed");
            Assert(text.Message(103)=="Synthetic 103");
            Reject(()=>new ArroyoElderText(profile,file,"other"));
            Reject(()=>new ArroyoElderText(profile,file with {Source=file.Source with {Path="text/english/dialog/other.msg"}},"english"));
            entries.Remove(104); Reject(()=>new ArroyoElderText(profile,file,"english"));
            Reject(()=>text.Message(999));
        });
        check("elder quest consultation traverses Node015 Node007 and Node016 and enforces L7 post-quest Node004 and Node009 L10 boundaries",()=>
        {
            var profile=new ArroyoElderConsultationProfile();
            var directAccept=new ArroyoElderConsultation(profile,repeat:false,questMode:true);
            Assert(directAccept.QuestMode && !directAccept.QuestCompleted && directAccept.Current.Reply==103
                && directAccept.Choose(11836,0) && directAccept.Procedure=="Node015" && directAccept.Current.Reply==159
                && !directAccept.Choose(17902,1) && directAccept.Revision==1
                && directAccept.Choose(17934,1) && directAccept.Complete && directAccept.Procedure=="Node016"
                && directAccept.Current.Reply==163 && directAccept.Current.Choices.Length==0);
            var acceptWithReminder=new ArroyoElderConsultation(profile,repeat:false,questMode:true);
            Assert(acceptWithReminder.Choose(11804,0) && acceptWithReminder.Choose(14632,1)
                && acceptWithReminder.Procedure=="Node015" && acceptWithReminder.Choose(17870,2)
                && acceptWithReminder.Procedure=="Node007" && acceptWithReminder.Current.Reply==131
                && !acceptWithReminder.Choose(15794,3) && acceptWithReminder.Revision==3
                && acceptWithReminder.Choose(15826,3) && acceptWithReminder.Complete && acceptWithReminder.Procedure=="Node016"
                && acceptWithReminder.Current.Reply==163);
            var repeatPreQuest=new ArroyoElderConsultation(profile,repeat:true,questMode:true,questCompleted:false);
            Assert(repeatPreQuest.Repeat && repeatPreQuest.QuestMode && !repeatPreQuest.QuestCompleted
                && !repeatPreQuest.Choose(13814,0) && !repeatPreQuest.Choose(13942,0)
                && repeatPreQuest.Choose(13902,0) && repeatPreQuest.Procedure=="Node013"
                && repeatPreQuest.Choose(17542,1) && repeatPreQuest.Procedure=="Node015"
                && repeatPreQuest.Choose(17870,2) && repeatPreQuest.Procedure=="Node007"
                && repeatPreQuest.Choose(15826,3) && repeatPreQuest.Complete && repeatPreQuest.Procedure=="Node016");
            var repeatPostQuest=new ArroyoElderConsultation(profile,repeat:true,questMode:true,questCompleted:true);
            Assert(repeatPostQuest.Repeat && repeatPostQuest.QuestMode && repeatPostQuest.QuestCompleted
                && repeatPostQuest.Current.Choices.Select(c=>c.Offset).SequenceEqual(new[] {13814,13942,13974,14006})
                && !repeatPostQuest.Choose(13814,0) && !repeatPostQuest.Choose(13942,0) && !repeatPostQuest.Choose(13870,0) && !repeatPostQuest.Choose(13902,0)
                && repeatPostQuest.Choose(13974,0) && repeatPostQuest.Procedure=="Node007" && repeatPostQuest.Current.Reply==131
                && !repeatPostQuest.Choose(15794,1) && repeatPostQuest.Choose(15826,1)
                && repeatPostQuest.Complete && repeatPostQuest.Procedure=="Node016" && repeatPostQuest.Current.Reply==163);
        });
    }
    private static void Assert(bool value) {if (!value) throw new Exception("Elder consultation assertion failed.");}
    private static void Reject(Action action) {try {action();} catch (InvalidDataException) {return;} throw new Exception("Invalid elder consultation text accepted.");}
}
