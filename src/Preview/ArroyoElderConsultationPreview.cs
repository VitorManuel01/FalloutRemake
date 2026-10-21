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
            ClearRouteHover(); GetTree().Root.GuiEmbedSubwindows=true;
            var dialog=new ArroyoElderWindow {Size=new(800,570),Theme=_hud.GetChild<Control>(0).Theme};
            _elderWindow=dialog;
            bool Active()=>_elderWindow==dialog && _campaign==world && world.TempleToken==token
                && world.Location==position && ElderConsultationBlockedReason()==null;
            void Refresh()
            {
                var current=_elderConsultation!; long revision=current.Revision;
                dialog.ChoiceRequested=offset=>
                {
                    if (!Active() || _elderConsultation!=current || !current.Choose(offset,revision)) return;
                    Refresh();
                };
                dialog.Refresh(current,text);
            }
            void Restart()
            {
                if (!Active()) return;
                _elderConsultation?.Cancel(); _elderConsultation=new(profile); Refresh();
            }
            dialog.RestartRequested=Restart;
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
