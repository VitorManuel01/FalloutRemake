using System;
using System.IO;
using FalloutRemake.Geometry;
using FalloutRemake.Import;
using FalloutRemake.Runtime;
using Godot;

namespace FalloutRemake.Preview;

public partial class TemplePreview
{
    private ConfirmationDialog? _barrierEffectReview;
    private void CloseBarrierEffectReview()
    {
        if (_barrierEffectReview == null) return;
        _barrierEffectReview.Hide(); _barrierEffectReview.QueueFree(); _barrierEffectReview = null;
    }
    private void ReviewPendingEffect(Guid id)
    {
        if (_campaign == null || _useClock?.Visible != true || _walker?.Moving == true) return;
        Prototype prototype;
        try { prototype = new PrototypeCatalog(new ResourceCatalog(new GameData(_directory!).Read)).Get(TempleBarrierEffectProposal.ReplacementPid); }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException)
        { _useClock.Message("Os dados da barreira estão indisponíveis. O evento foi preservado."); return; }
        BarrierEffectProposal proposed = TempleBarrierEffectProposal.Create(_campaign, id, prototype);
        if (proposed.Status != BarrierEffectStatus.Ready)
        {
            _useClock.Message(proposed.Status switch {
                BarrierEffectStatus.NotDue => "O prazo ainda não venceu.",
                BarrierEffectStatus.NoTarget => "A barreira do Templo ficou fora do alcance. O evento continua pendente; outro efeito ainda não está disponível.",
                BarrierEffectStatus.AlreadyReplaced => "A barreira já está aberta. Este evento continua pendente.",
                _ => "Este evento não tem efeito de cenário disponível neste ensaio. Ele continua preservado."
            }); return;
        }
        TempleBarrierEffectPlan plan = proposed.Plan!;
        if (_campaign.CurrentMap != plan.Center.Map || _elevation != plan.Center.Elevation || _walker == null)
        { _useClock.Message("Volte à área e elevação da barreira para consultar e aplicar este efeito."); return; }
        Guid replacementId = Guid.NewGuid();
        var projection = TempleBarrierProjection.Create(_campaign, plan, prototype, replacementId);
        if (projection.Status != BarrierProjectionStatus.Ready) { _useClock.Message("A situação mudou. Consulte novamente o evento."); return; }
        int before = _navigation!.ReachableFrom(_walker.CurrentHex).Count, after = projection.Projection!.Navigation.ReachableFrom(_walker.CurrentHex).Count;
        CloseUseClock();
        var dialog = new ConfirmationDialog { Name = "BarrierEffectReview", Title = "Abrir passagem · ensaio", Exclusive = true, Transient = true,
            DialogHideOnOk = false, Size = new(650, 330), MinSize = new(520, 280), Theme = _hud.GetChild<Control>(0).Theme };
        _barrierEffectReview = dialog;
        var description = new RichTextLabel { Text = $"A barreira do Templo está no alcance do explosivo.\n\nAplicar substitui a barreira por destroços e resolve esta unidade posicionada. Área alcançável: {before} → {after} hexes.\n\nEste ensaio altera somente o cenário; não causa dano a personagens. Cancelar mantém o evento pendente.",
            BbcodeEnabled = false };
        description.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect); description.OffsetLeft = 20; description.OffsetRight = -20; description.OffsetTop = 16; description.OffsetBottom = -65;
        dialog.GetOkButton().Text = "Aplicar no cenário"; dialog.GetCancelButton().Text = "Cancelar";
        dialog.Confirmed += () => {
            if (_barrierEffectReview != dialog) return;
            if (_campaign == null || _walker?.Moving != false || _campaign.CurrentMap != plan.Center.Map || _elevation != plan.Center.Elevation)
            { description.Text = "A posição ou a situação mudou. Feche e consulte novamente. O evento foi preservado."; dialog.GetOkButton().Disabled = true; return; }
            BarrierCommitResult result = ApplyBarrierEffect(plan, replacementId);
            if (result.Status != BarrierCommitStatus.Ready)
            { description.Text = "O plano já não é válido. Feche e consulte novamente. O evento foi preservado."; dialog.GetOkButton().Disabled = true; }
        };
        void Cancel() { if (_barrierEffectReview != dialog) return; CloseBarrierEffectReview(); OpenUseClock(); }
        dialog.Canceled += Cancel; dialog.CloseRequested += Cancel;
        _hud.AddChild(dialog); dialog.AddChild(description); dialog.PopupCenteredClamped(new Vector2I(650, 330), .92f);
        var target = _layout!.Objects;
        foreach (var obj in target) if (obj.Source.Key == plan.TargetKey) { SelectObject(obj); _focus = Point(obj.Position, 0); _camera.Size = 22; UpdateCamera(); break; }
    }
}
