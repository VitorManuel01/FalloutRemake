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
    }
    private static void Assert(bool value) {if (!value) throw new Exception("Elder consultation assertion failed.");}
    private static void Reject(Action action) {try {action();} catch (InvalidDataException) {return;} throw new Exception("Invalid elder consultation text accepted.");}
}
