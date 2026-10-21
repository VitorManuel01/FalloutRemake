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
        OpenElderConsultation(); var draft=_elderConsultation!; stale=_elderWindow!.ChoiceRequested!;
        SaveCheckpoint(); LoadCheckpoint(); stale(11804);
        if (_elderWindow!=null || !draft.Closed || _walker!.Moving) throw new InvalidDataException("World load retained the elder consultation draft.");
        Unchanged();
        var selected=_selected!; ClearSelection(); OpenElderConsultation();
        if (_elderWindow!=null) throw new InvalidDataException("Elder conversation opened without the original actor selected.");
        SelectObject(selected);
        foreach (var area in _campaign!.Areas) MapLayoutWriter.Verify(area.Map);
        GD.Print("ARROYO_ELDER_CONSULTATION_OK: local original MSG/INT identity, 4 reply nodes, supported choices and reaction-only text bridge, inventory branches disabled, modal guard, cancel/restart/end, old revision/button/draft callbacks rejected including world K/R, exact unchanged checkpoint/XP/items/globals/Temple/MAP; live dialogue effects remain disabled.");
    }
}
