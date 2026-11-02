using System;
using System.IO;
using FalloutRemake.Geometry;
using FalloutRemake.Import;
using FalloutRemake.Runtime;
using Godot;

namespace FalloutRemake.Preview;

public partial class TemplePreview
{
    private ArroyoElderWindow? _elderWindow;
    private ArroyoElderConsultation? _elderConsultation;
    private Guid? _elderRepeatToken;
    private string? ElderConsultationBlockedReason()
    {
        if (_campaign?.CurrentMap!=4 || _campaign.Location.Elevation!=0 || _elevation!=0)
            return "Volte à elevação do personagem em Arroyo.";
        var source=ArroyoElderSource.Match(_campaign.Current.Map);
        if (source==null || _selected?.Source.Key!=source.Key || !VisibleObject(_selected)) return "Selecione a anciã original.";
        if (_walker?.Moving!=false) return "Termine ou pare a caminhada para consultar a conversa.";
        if (FalloutGrid.Distance(_campaign.Location.Hex,source.Header.Tile)!=1) return "Aproxime o personagem de um hex vizinho da anciã.";
        if (_campaign.ArroyoGlobals==null) return "A consulta requer a chegada verificada em Arroyo.";
        return _directory==null || _objectText==null ? "Os textos originais não estão disponíveis." : null;
    }
    private void RefreshElderConsultationAction(bool visible)
    {
        string? blocked=ElderConsultationBlockedReason();
        _hud.ElderConsultationAction(visible,blocked==null && !InspectorOpen,blocked);
        _hud.ElderConsultationRequested=OpenElderConsultation;
        _hud.CommitElderDialogueRequested=()=>_elderWindow?.CommitElderDialogueRequested?.Invoke();
    }
    private void CloseElderConsultation()
    {
        _elderConsultation?.Cancel(); _elderConsultation=null;
        if (_elderWindow!=null) {_elderWindow.Hide(); _elderWindow.QueueFree(); _elderWindow=null;}
        RefreshArroyoElderGuidance();
    }
    private void OpenElderConsultation()
    {
        if (InspectorOpen) return;
        string? blocked=ElderConsultationBlockedReason();
        if (blocked!=null) {NavigationStatus(blocked);return;}
        try
        {
            var data=new GameData(_directory!);
            var profile=ArroyoElderConsultationProfile.Create(_campaign!.Current.Map,data.Read);
            var text=new ArroyoElderText(profile,MessageTextFile.Read(data.Read($"text/{_objectText!.Language}/dialog/ahelder.msg")),_objectText.Language);
            var world=_campaign; var position=world.Location; var token=world.TempleToken;
            bool repeat=_elderRepeatToken==token || world.ElderDialogue!=null || world.ElderQuestDialogue!=null;
            bool questMode=world.ElderQuestDialogue!=null;
            ClearRouteHover(); GetTree().Root.GuiEmbedSubwindows=true;
            var dialog=new ArroyoElderWindow {Size=new(800,570),Theme=_hud.GetChild<Control>(0).Theme};
            _elderWindow=dialog;
            bool Active()=>_elderWindow==dialog && _campaign==world && world.TempleToken==token
                && world.Location==position && ElderConsultationBlockedReason()==null;
            void Refresh()
            {
                var current=_elderConsultation!; long revision=current.Revision;
                if (current.Complete) _elderRepeatToken=token; else if (!repeat) _elderRepeatToken=null;
                bool canCommit=current.Complete && (current.QuestMode
                    ? !current.Repeat
                        ? world.ElderDialogue==null && world.ElderQuestDialogue==null
                        : world.ElderQuestDialogue!=null && world.ElderQuestRepeatDialogue==null
                    : !current.Repeat
                        ? world.ElderDialogue==null && world.ElderQuestDialogue==null
                        : world.ElderDialogue!=null && world.ElderRepeatDialogue==null);
                bool canToggle=(world.ElderDialogue==null || world.ElderRepeatDialogue!=null)
                    && (world.ElderQuestDialogue==null || world.ElderQuestRepeatDialogue!=null);
                dialog.ChoiceRequested=offset=>
                {
                    if (!Active() || _elderConsultation!=current || !current.Choose(offset,revision)) return;
                    Refresh();
                };
                dialog.CommitElderDialogueRequested=()=>
                {
                    if (!Active() || _elderConsultation!=current || !current.Complete) return;
                    try
                    {
                        if (current.QuestMode)
                        {
                            if (!current.Repeat && (world.ElderDialogue!=null || world.ElderQuestDialogue!=null)) return;
                            if (current.Repeat && (world.ElderQuestDialogue==null || world.ElderQuestRepeatDialogue!=null)) return;
                            var questProfile=ArroyoElderQuestProfile.Create(world.Current.Map,data.Read);
                            if (!current.Repeat)
                            {
                                var projection=questProfile.ProjectFirst(current.Choices);
                                var committed=world.CommitElderQuestDialogue(questProfile,projection,_walker?.Moving==true,world.SessionToken,token);
                                _elderRepeatToken=token;
                                CloseElderConsultation();
                                NavigationStatus($"Missão da anciã aplicada na sessão · GVAR 452={committed.VicKnown} · L7={(committed.QuestGranted ? 1 : 0)}");
                            }
                            else
                            {
                                var projection=questProfile.ProjectRepeat(world.ElderQuestDialogue!,current.Choices);
                                var committed=world.CommitElderQuestRepeatDialogue(questProfile,projection,_walker?.Moving==true,world.SessionToken,token);
                                _elderRepeatToken=token;
                                CloseElderConsultation();
                                NavigationStatus($"Conversa repetida de missão aplicada · reação {committed.Reaction} · GVAR 452={committed.VicKnown}");
                            }
                            return;
                        }
                        if (!current.Repeat && (world.ElderDialogue!=null || world.ElderQuestDialogue!=null)) return;
                        if (current.Repeat && (world.ElderDialogue==null || world.ElderRepeatDialogue!=null)) return;
                        var stateProfile=ArroyoElderStateProfile.Create(world.Current.Map,data.Read);
                        if (!current.Repeat)
                        {
                            var projection=stateProfile.Project(current.Choices);
                            world.CommitElderDialogue(stateProfile,projection,_walker?.Moving==true,world.SessionToken,token);
                            _elderRepeatToken=token;
                            CloseElderConsultation();
                            NavigationStatus("Conversa da anciã aplicada na sessão · GVAR_TALKED_TO_ELDER=1");
                        }
                        else
                        {
                            var projection=stateProfile.ProjectRepeat(world.ElderDialogue!,current.Choices);
                            var committed=world.CommitElderRepeatDialogue(stateProfile,projection,_walker?.Moving==true,world.SessionToken,token);
                            _elderRepeatToken=token;
                            CloseElderConsultation();
                            NavigationStatus($"Conversa repetida da anciã aplicada na sessão · reação {committed.Reaction}");
                        }
                    }
                    catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException)
                    {
                        dialog.Notice("Não foi possível aplicar a conversa da anciã. A sessão foi preservada.");
                        GD.Print("ELDER_COMMIT_REJECTED: "+error.Message);
                    }
                };
                dialog.Refresh(current,text,canCommit,world.ElderDialogue!=null || world.ElderQuestDialogue!=null,canToggle);
            }
            void Restart()
            {
                if (!Active()) return;
                _elderConsultation?.Cancel();
                _elderConsultation=new(profile,repeat,questMode,world.ElderQuestState?.QuestGranted==true);
                Refresh();
            }
            dialog.RestartRequested=Restart;
            dialog.QuestModeToggleRequested=()=>
            {
                if (!Active()) return;
                questMode=!questMode;
                Restart();
            };
            void Close() {if (_elderWindow==dialog) CloseElderConsultation();}
            dialog.Confirmed+=Close; dialog.Canceled+=Close; dialog.CloseRequested+=Close;
            _hud.AddChild(dialog); Restart(); dialog.PopupCenteredClamped(new(800,570),.92f); RefreshArroyoElderGuidance();
        }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException)
        {
            CloseElderConsultation(); NavigationStatus("Consulta indisponível: os dados locais não correspondem ao trecho verificado.");
            GD.Print("ELDER_CONSULTATION_UNAVAILABLE: "+error.Message);
        }
    }
}
