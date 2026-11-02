using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FalloutRemake.Import;
using FalloutRemake.Runtime;
using Godot;

namespace FalloutRemake.Preview;

public partial class TemplePreview
{
    private async Task CheckArroyoElderConsultation()
    {
        byte[] State()=>CheckpointFile.Encode(_campaign!.Capture(_mapCatalog!.Source.Sha256));
        byte[] before=State();
        void Unchanged() {if (!before.SequenceEqual(State())) throw new InvalidDataException("Detached elder consultation changed the active world.");}
        var data=new GameData(_directory!); var profile=ArroyoElderConsultationProfile.Create(_map!,data.Read);
        var text=new ArroyoElderText(profile,MessageTextFile.Read(data.Read($"text/{_objectText!.Language}/dialog/ahelder.msg")),_objectText.Language);
        var action=(Button)_hud.FindChild("ElderConsultationAction",true,false);
        if (!action.Visible || action.Disabled) throw new InvalidDataException("Elder consultation action unavailable within reach.");
        action.EmitSignal(Button.SignalName.Pressed);
        await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
        if (_elderWindow?.Visible!=true || !InspectorOpen || _elderWindow.ReplyText!=text.Message(103))
            throw new InvalidDataException("Elder original opening is not displayed in its consultation.");
        var window=_elderWindow; var oldChoice=window.ChoiceRequested!;
        var oldButton=(Button)window.FindChild("ElderChoice11804",true,false);
        if (!((Button)window.FindChild("ElderChoice11836",true,false)).Disabled)
            throw new InvalidDataException("Unaudited inventory branch was enabled in the text consultation.");
        oldChoice(11836); Unchanged();
        if (_elderConsultation!.Revision!=0) throw new InvalidDataException("Unsupported elder branch advanced the consultation.");
        _hud.ArroyoElderRequested!();
        if (_walker!.Moving) throw new InvalidDataException("Elder modal allowed another movement action.");
        oldButton.EmitSignal(Button.SignalName.Pressed);
        if (window.ReplyText!=text.Message(127)) throw new InvalidDataException("Original Elder Node006 response was not selected.");
        oldChoice(11804);
        if (_elderConsultation.Revision!=1) throw new InvalidDataException("Stale elder choice advanced a later node.");
        window.ChoiceRequested!(14664);
        if (window.ReplyText!=text.Message(135)) throw new InvalidDataException("Elder reaction-only bridge failed to show its original reply.");
        Unchanged();
        window.ChoiceRequested!(16348);
        if (!_elderConsultation.Complete) throw new InvalidDataException("Original elder end target did not finish the text consultation.");
        if (_capture!=null) await Capture(Path.ChangeExtension(_capture,"arroyo-elder-consultation-end.png"));
        var oldDraft=_elderConsultation;
        window.RestartRequested!();
        if (!oldDraft.Closed || _elderConsultation==oldDraft || window.ReplyText!=text.Message(103))
            throw new InvalidDataException("Elder restart retained the old draft.");
        // An old node button must retain its old callback even when the current node's offset matches again.
        var restartButton=(Button)window.FindChild("ElderChoice11804",true,false);
        window.RestartRequested!(); restartButton.EmitSignal(Button.SignalName.Pressed);
        if (_elderConsultation!.Revision!=0) throw new InvalidDataException("Old elder button advanced a restarted consultation.");
        if (_capture!=null) await Capture(Path.ChangeExtension(_capture,"arroyo-elder-consultation.png"));
        window.ChoiceRequested!(11804); window.ChoiceRequested!(14696);
        if (window.ReplyText!=text.Message(152)) throw new InvalidDataException("Elder alternative original response was not displayed.");
        window.ChoiceRequested!(17542);
        if (_elderConsultation.Revision!=2) throw new InvalidDataException("Unsupported inventory continuation advanced the draft.");
        window.ChoiceRequested!(17510); Unchanged();
        var stale=window.ChoiceRequested!; var restart=window.RestartRequested!;
        window.EmitSignal(Window.SignalName.CloseRequested);
        stale(11804); restart();
        if (_elderWindow!=null || InspectorOpen) throw new InvalidDataException("Elder close retained a live modal or callback.");
        Unchanged();
        await CheckArroyoElderRepeatConsultation(action,text,Unchanged);
        OpenElderConsultation(); var draft=_elderConsultation!; stale=_elderWindow!.ChoiceRequested!;
        SaveCheckpoint(); LoadCheckpoint(); stale(11804); stale(13902);
        if (_elderWindow!=null || !draft.Closed || _walker!.Moving) throw new InvalidDataException("World load retained the elder consultation draft.");
        Unchanged();
        var selected=_selected!; ClearSelection(); OpenElderConsultation();
        if (_elderWindow!=null) throw new InvalidDataException("Elder conversation opened without the original actor selected.");
        SelectObject(selected);
        await CheckArroyoElderQuestConsultation(action,text,Unchanged);
        await CheckArroyoElderDialogueCommit(action,text);
        foreach (var area in _campaign!.Areas) MapLayoutWriter.Verify(area.Map);
        GD.Print("ARROYO_ELDER_CONSULTATION_OK: local original MSG/INT identity, 8 reply/closing nodes including repeat Node004 (117) and quest/reminder Node015 (159), Node007 (131) and Node016 (163), area-11 option 118 disabled for metarule(17,11)==0, Node009 options (120/133/161) disabled for out-of-range L10, supported choices and reaction-only text bridge, modal guard, cancel/restart/end, old revision/button/draft callbacks rejected including world K/R, transactional CommitElderDialogue and CommitElderQuestDialogue with GVAR_TALKED_TO_ELDER(531)=1, GVAR_DEN_VIC_KNOWN(452)=2, L7=1, 152 caps + PID 267 flask projection and reaction locals, exact unchanged checkpoint/XP/items/globals/Temple/MAP.");
    }
    private async Task CheckArroyoElderQuestConsultation(Button action,ArroyoElderText text,Action unchanged)
    {
        action.EmitSignal(Button.SignalName.Pressed);
        await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
        var window=_elderWindow ?? throw new InvalidDataException("Elder window did not open for quest consultation check.");
        window.QuestModeToggleRequested!();
        if (_elderConsultation is not {Repeat:false,QuestMode:true,QuestCompleted:false,Complete:false,Procedure:"Node001"}
            || window.ReplyText!=text.Message(103)
            || ((Button)window.FindChild("ElderChoice11836",true,false)).Disabled)
            throw new InvalidDataException("Quest mode toggle did not enable first-talk Node015 acceptance (11836).");
        window.ChoiceRequested!(11836);
        if (_elderConsultation.Procedure!="Node015" || window.ReplyText!=text.Message(159)
            || !((Button)window.FindChild("ElderChoice17902",true,false)).Disabled)
            throw new InvalidDataException("Quest Node015 did not display message 159 or failed to disable Node009 (17902).");
        window.ChoiceRequested!(17902);
        if (_elderConsultation.Revision!=1) throw new InvalidDataException("Disabled Node009 choice 17902 advanced Node015.");
        window.ChoiceRequested!(17870);
        if (_elderConsultation.Procedure!="Node007" || window.ReplyText!=text.Message(131)
            || !((Button)window.FindChild("ElderChoice15794",true,false)).Disabled)
            throw new InvalidDataException("Quest reminder Node007 did not display message 131 or failed to disable Node009 (15794).");
        window.ChoiceRequested!(15794);
        if (_elderConsultation.Revision!=2) throw new InvalidDataException("Disabled Node009 choice 15794 advanced Node007.");
        window.ChoiceRequested!(15826);
        if (!_elderConsultation.Complete || _elderConsultation.Procedure!="Node016" || window.ReplyText!=text.Message(163))
            throw new InvalidDataException("Quest closing Node016 did not display message 163 or mark completion.");
        unchanged();
        _hud.CommitElderDialogueRequested!();
        if (_elderWindow!=null || InspectorOpen || _campaign!.TalkedToElder!=1 || _campaign.VicKnown!=2
            || !_campaign.ElderQuestGranted || _campaign.ElderCapsTransferred!=152 || _campaign.ElderFlaskGranted!=1
            || _campaign.ElderQuestDialogue is not {Complete:true,Procedure:"Node016",Reaction:22,ClosingMessage:163} firstQuest
            || firstQuest.Local(3)!=-10 || firstQuest.Local(7)!=1 || firstQuest.Global(452)!=2 || firstQuest.Global(531)!=1)
            throw new InvalidDataException("Confirmed quest consultation did not commit Node015/Node007/Node016 state to WorldSession.");
        action.EmitSignal(Button.SignalName.Pressed);
        await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
        window=_elderWindow ?? throw new InvalidDataException("Elder window did not reopen in post-quest repeat mode.");
        if (_elderConsultation is not {Repeat:true,QuestMode:true,QuestCompleted:true,Complete:false,Procedure:"Node004"}
            || window.ReplyText!=text.Message(117)
            || window.FindChild("ElderChoice13870",true,false)!=null
            || window.FindChild("ElderChoice13902",true,false)!=null
            || !((Button)window.FindChild("ElderChoice13942",true,false)).Disabled
            || ((Button)window.FindChild("ElderChoice13974",true,false)).Disabled)
            throw new InvalidDataException("Post-quest Node004 (L7=1) did not replace 119/121 with disabled 120 (Node009) and enabled 122 (Node007).");
        window.ChoiceRequested!(13942);
        if (_elderConsultation.Revision!=0) throw new InvalidDataException("Disabled post-quest Node009 option 13942 advanced Node004.");
        window.ChoiceRequested!(13974); window.ChoiceRequested!(15826);
        if (!_elderConsultation.Complete || _elderConsultation.Procedure!="Node016" || window.ReplyText!=text.Message(163))
            throw new InvalidDataException("Post-quest reminder route 122->134 did not finish in Node016.");
        _hud.CommitElderDialogueRequested!();
        if (_elderWindow!=null || _campaign.ElderQuestRepeatDialogue is not {Complete:true,Procedure:"Node016",Reaction:12,CapsTransferred:152,FlaskGranted:1} repeatQuest
            || repeatQuest.Local(3)!=-20 || repeatQuest.Local(7)!=1 || _campaign.ElderQuestState?.Reaction!=12)
            throw new InvalidDataException("Confirmed post-quest repeat reminder did not update reaction to 12 while preserving single item grant.");
        ResetWalker(); LoadCheckpoint();
        SelectObject(_layout!.Objects.Single(o=>o.Source.Key==ArroyoElderSource.Match(_campaign.Current.Map)!.Key));
        unchanged();
    }
    private async Task CheckArroyoElderDialogueCommit(Button action,ArroyoElderText text)
    {
        if (_campaign!.TalkedToElder!=0 || _campaign.ElderDialogue!=null || _campaign.ElderRepeatDialogue!=null)
            throw new InvalidDataException("Elder dialogue was marked committed before confirmation.");
        action.EmitSignal(Button.SignalName.Pressed);
        await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
        var window=_elderWindow ?? throw new InvalidDataException("Elder window did not open for commit check.");
        var commit=(Button)window.FindChild("ElderCommitWorld",true,false);
        if (commit.Visible || !commit.Disabled || _elderConsultation is not {Repeat:false,Complete:false,Procedure:"Node001"})
            throw new InvalidDataException("Elder commit action was enabled before completing the first dialogue.");
        window.CommitElderDialogueRequested!();
        if (_campaign.TalkedToElder!=0 || _campaign.ElderDialogue!=null || _elderWindow!=window)
            throw new InvalidDataException("Incomplete elder consultation committed to the active world.");
        window.ChoiceRequested!(11868); window.ChoiceRequested!(16348);
        if (!_elderConsultation.Complete || !commit.Visible || commit.Disabled || _campaign.TalkedToElder!=0 || _campaign.ElderDialogue!=null)
            throw new InvalidDataException("Completed first elder consultation did not offer transactional confirmation without premature mutation.");
        var staleCommit=window.CommitElderDialogueRequested!;
        _hud.CommitElderDialogueRequested!();
        if (_elderWindow!=null || InspectorOpen || _campaign.TalkedToElder!=1 || _campaign.ElderRepeatDialogue!=null
            || _campaign.ElderDialogue is not {Complete:true,Procedure:"Node999",Reaction:22} committed
            || !committed.Choices.SequenceEqual(new[] {11868,16348})
            || committed.Local(0)!=22 || committed.Local(1)!=1 || committed.Local(2)!=1 || committed.Local(3)!=-10 || committed.Local(4)!=1
            || committed.Global(531)!=1)
            throw new InvalidDataException("Confirmed elder dialogue did not record GVAR 531=1 and reaction locals on WorldSession.");
        staleCommit();
        SaveCheckpoint(); byte[] savedCommitted=CheckpointFile.Encode(_campaign.Capture(_mapCatalog!.Source.Sha256));
        ResetWalker(); LoadCheckpoint();
        SelectObject(_layout!.Objects.Single(o=>o.Source.Key==ArroyoElderSource.Match(_campaign.Current.Map)!.Key));
        if (!savedCommitted.SequenceEqual(CheckpointFile.Encode(_campaign.Capture(_mapCatalog.Source.Sha256)))
            || _campaign.TalkedToElder!=1 || _campaign.ElderDialogue?.Reaction!=22 || _campaign.ElderRepeatDialogue!=null || _campaign.Experience!=625)
            throw new InvalidDataException("v10 checkpoint failed to persist and restore SavedElderState, XP and doors.");
        action.EmitSignal(Button.SignalName.Pressed);
        await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
        window=_elderWindow ?? throw new InvalidDataException("Elder window did not reopen in repeat mode after commit.");
        commit=(Button)window.FindChild("ElderCommitWorld",true,false);
        if (_elderConsultation is not {Repeat:true,Complete:false,Procedure:"Node004"} || window.ReplyText!=text.Message(117) || commit.Visible || !commit.Disabled)
            throw new InvalidDataException("Committed elder session did not reopen in repeat Node004 with commit disabled while incomplete.");
        window.CommitElderDialogueRequested!();
        if (_campaign.ElderRepeatDialogue!=null || _campaign.ElderState?.Reaction!=22)
            throw new InvalidDataException("Incomplete repeat elder consultation committed to the active world.");
        window.ChoiceRequested!(13902); window.ChoiceRequested!(17510);
        if (!_elderConsultation.Complete || !commit.Visible || commit.Disabled || _campaign.ElderRepeatDialogue!=null || _campaign.ElderState?.Reaction!=22)
            throw new InvalidDataException("Completed repeat elder consultation did not offer transactional confirmation without premature mutation.");
        var staleRepeatCommit=window.CommitElderDialogueRequested!;
        _hud.CommitElderDialogueRequested!();
        if (_elderWindow!=null || InspectorOpen || _campaign.TalkedToElder!=1
            || _campaign.ElderDialogue?.Reaction!=22
            || _campaign.ElderRepeatDialogue is not {Complete:true,Procedure:"Node999",Reaction:2} repeatCommitted
            || !repeatCommitted.Choices.SequenceEqual(new[] {13902,17510})
            || repeatCommitted.Local(0)!=2 || repeatCommitted.Local(1)!=0 || repeatCommitted.Local(3)!=-30
            || _campaign.ElderState?.Reaction!=2)
            throw new InvalidDataException("Confirmed repeat elder dialogue did not update reaction and repeat choices on WorldSession.");
        staleRepeatCommit();
        action.EmitSignal(Button.SignalName.Pressed);
        await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
        window=_elderWindow ?? throw new InvalidDataException("Elder window did not reopen after repeat commit.");
        commit=(Button)window.FindChild("ElderCommitWorld",true,false);
        window.ChoiceRequested!(14006);
        if (!_elderConsultation!.Complete || commit.Visible || !commit.Disabled)
            throw new InvalidDataException("Second repeat elder completion offered a duplicate repeat commit.");
        window.CommitElderDialogueRequested!();
        if (_campaign.ElderRepeatDialogue?.Reaction!=2 || _campaign.ElderState?.Reaction!=2)
            throw new InvalidDataException("Subsequent repeat elder consultation altered the committed repeat state.");
        window.EmitSignal(Window.SignalName.CloseRequested);
        SaveCheckpoint(); byte[] savedRepeat=CheckpointFile.Encode(_campaign.Capture(_mapCatalog.Source.Sha256));
        ResetWalker(); LoadCheckpoint();
        SelectObject(_layout!.Objects.Single(o=>o.Source.Key==ArroyoElderSource.Match(_campaign.Current.Map)!.Key));
        if (!savedRepeat.SequenceEqual(CheckpointFile.Encode(_campaign.Capture(_mapCatalog.Source.Sha256)))
            || _campaign.TalkedToElder!=1 || _campaign.ElderDialogue?.Reaction!=22
            || _campaign.ElderRepeatDialogue is not {Reaction:2} restoredRepeat
            || !restoredRepeat.Choices.SequenceEqual(new[] {13902,17510})
            || _campaign.ElderState?.Reaction!=2 || _campaign.Experience!=625)
            throw new InvalidDataException("Checkpoint failed to persist and restore repeat SavedElderState.");
    }
    private async Task CheckArroyoElderRepeatConsultation(Button action,ArroyoElderText text,Action unchanged)
    {
        action.EmitSignal(Button.SignalName.Pressed);
        await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
        if (_elderWindow?.Visible!=true || !InspectorOpen || _elderConsultation is not {Repeat:true,Complete:false,Procedure:"Node004",Revision:0}
            || _elderWindow.ReplyText!=text.Message(117))
            throw new InvalidDataException("Reopened elder consultation did not open repeat Node004 (message 117).");
        var window=_elderWindow;
        Button ChoiceButton(int offset,int message)
        {
            var button=window.FindChild("ElderChoice"+offset,true,false) as Button
                ?? throw new InvalidDataException($"Missing repeat elder choice button {offset}.");
            if (button.Text!=text.Message(message)) throw new InvalidDataException($"Repeat elder choice {offset} text mismatch.");
            return button;
        }
        var area11=ChoiceButton(13814,118);
        var preQuest119=ChoiceButton(13870,119);
        var preQuest121=ChoiceButton(13902,121);
        var node007=ChoiceButton(13974,122);
        var directExit=ChoiceButton(14006,123);
        if (!area11.Disabled || !preQuest119.Disabled || !node007.Disabled || preQuest121.Disabled || directExit.Disabled
            || window.FindChild("ElderChoice13942",true,false)!=null)
            throw new InvalidDataException("Repeat Node004 choice availability violated metarule(17,11)==0 or pre-quest L7==0 guards.");
        area11.EmitSignal(Button.SignalName.Pressed);
        window.ChoiceRequested!(13814); window.ChoiceRequested!(13870); window.ChoiceRequested!(13974);
        if (_elderConsultation.Revision!=0 || _elderConsultation.Procedure!="Node004")
            throw new InvalidDataException("Disabled repeat elder option advanced Node004.");
        unchanged();
        directExit.EmitSignal(Button.SignalName.Pressed);
        if (!_elderConsultation.Complete || _elderConsultation.Procedure!="Node999" || _elderConsultation.Revision!=1)
            throw new InvalidDataException("Repeat option 123 did not finish cleanly in Node999.");
        unchanged();
        var finishedRepeat=_elderConsultation;
        window.RestartRequested!();
        if (!finishedRepeat.Closed || _elderConsultation==finishedRepeat
            || _elderConsultation is not {Repeat:true,Complete:false,Procedure:"Node004",Revision:0}
            || window.ReplyText!=text.Message(117))
            throw new InvalidDataException("Repeat elder restart did not return to Node004.");
        ChoiceButton(14006,123).EmitSignal(Button.SignalName.Pressed);
        if (!_elderConsultation.Complete || _elderConsultation.Procedure!="Node999")
            throw new InvalidDataException("Restarted repeat option 123 did not finish in Node999.");
        window.EmitSignal(Window.SignalName.CloseRequested);
        if (_elderWindow!=null || InspectorOpen) throw new InvalidDataException("Repeat elder close retained modal after direct exit.");
        unchanged();
        action.EmitSignal(Button.SignalName.Pressed);
        await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
        if (_elderWindow?.Visible!=true || _elderWindow.ReplyText!=text.Message(117) || _elderConsultation is not {Repeat:true,Procedure:"Node004"})
            throw new InvalidDataException("Subsequent elder reopen did not remain on repeat Node004.");
        window=_elderWindow;
        ((Button)window.FindChild("ElderChoice13902",true,false)).EmitSignal(Button.SignalName.Pressed);
        if (window.ReplyText!=text.Message(152) || _elderConsultation.Procedure!="Node013" || _elderConsultation.Complete)
            throw new InvalidDataException("Repeat option 121 did not transition to Node013.");
        if (!((Button)window.FindChild("ElderChoice17542",true,false)).Disabled)
            throw new InvalidDataException("Repeat Node013 enabled the unsupported Node015 branch.");
        window.ChoiceRequested!(17542);
        if (_elderConsultation.Revision!=1 || _elderConsultation.Complete)
            throw new InvalidDataException("Unsupported Node013 branch advanced repeat consultation.");
        ((Button)window.FindChild("ElderChoice17510",true,false)).EmitSignal(Button.SignalName.Pressed);
        if (!_elderConsultation.Complete || _elderConsultation.Procedure!="Node999" || _elderConsultation.Revision!=2)
            throw new InvalidDataException("Repeat Node013 exit (17510) did not finish cleanly in Node999.");
        unchanged();
        window.EmitSignal(Window.SignalName.CloseRequested);
        if (_elderWindow!=null || InspectorOpen) throw new InvalidDataException("Repeat elder close retained modal after Node013 exit.");
        unchanged();
    }
}
