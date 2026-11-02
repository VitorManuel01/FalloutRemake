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
        await CheckArroyoElderDialogueCommit(action,text);
        foreach (var area in _campaign!.Areas) MapLayoutWriter.Verify(area.Map);
        GD.Print("ARROYO_ELDER_CONSULTATION_OK: local original MSG/INT identity, 5 reply nodes including repeat Node004 (117), area-11 option 118 disabled for metarule(17,11)==0, supported choices and reaction-only text bridge, inventory/quest branches disabled, immediate (123) and Node013 (121->153) repeat exits to Node999, modal guard, cancel/restart/end, old revision/button/draft callbacks rejected including world K/R, transactional CommitElderDialogue with GVAR_TALKED_TO_ELDER(531)=1 and reaction locals, exact unchanged checkpoint/XP/items/globals/Temple/MAP.");
    }
    private async Task CheckArroyoElderDialogueCommit(Button action,ArroyoElderText text)
    {
        if (_campaign!.TalkedToElder!=0 || _campaign.ElderDialogue!=null)
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
        if (_elderWindow!=null || InspectorOpen || _campaign.TalkedToElder!=1
            || _campaign.ElderDialogue is not {Complete:true,Procedure:"Node999",Reaction:22} committed
            || !committed.Choices.SequenceEqual(new[] {11868,16348})
            || committed.Local(0)!=22 || committed.Local(1)!=1 || committed.Local(2)!=1 || committed.Local(3)!=-10 || committed.Local(4)!=1
            || committed.Global(531)!=1)
            throw new InvalidDataException("Confirmed elder dialogue did not record GVAR 531=1 and reaction locals on WorldSession.");
        staleCommit();
        action.EmitSignal(Button.SignalName.Pressed);
        await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
        window=_elderWindow ?? throw new InvalidDataException("Elder window did not reopen in repeat mode after commit.");
        commit=(Button)window.FindChild("ElderCommitWorld",true,false);
        if (_elderConsultation is not {Repeat:true,Complete:false,Procedure:"Node004"} || window.ReplyText!=text.Message(117) || commit.Visible)
            throw new InvalidDataException("Committed elder session did not reopen in repeat Node004 without duplicate commit.");
        window.ChoiceRequested!(14006);
        if (!_elderConsultation.Complete || commit.Visible || !commit.Disabled)
            throw new InvalidDataException("Repeat elder completion offered a duplicate session commit.");
        window.CommitElderDialogueRequested!();
        if (_campaign.ElderDialogue?.Reaction!=22 || _campaign.TalkedToElder!=1)
            throw new InvalidDataException("Repeat elder consultation altered the committed first-talk state.");
        window.EmitSignal(Window.SignalName.CloseRequested);
        SaveCheckpoint(); byte[] savedCommitted=CheckpointFile.Encode(_campaign.Capture(_mapCatalog!.Source.Sha256));
        ResetWalker(); LoadCheckpoint();
        SelectObject(_layout!.Objects.Single(o=>o.Source.Key==ArroyoElderSource.Match(_campaign.Current.Map)!.Key));
        if (!savedCommitted.SequenceEqual(CheckpointFile.Encode(_campaign.Capture(_mapCatalog.Source.Sha256)))
            || _campaign.TalkedToElder!=1 || _campaign.ElderDialogue?.Reaction!=22 || _campaign.Experience!=625)
            throw new InvalidDataException("v10 checkpoint failed to persist and restore SavedElderState, XP and doors.");
        action.EmitSignal(Button.SignalName.Pressed);
        await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
        if (_elderWindow?.Visible!=true || _elderConsultation is not {Repeat:true,Procedure:"Node004"} || _elderWindow.ReplyText!=text.Message(117))
            throw new InvalidDataException("Restored v10 SavedElderState did not open repeat Node004.");
        _elderWindow.EmitSignal(Window.SignalName.CloseRequested);
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
