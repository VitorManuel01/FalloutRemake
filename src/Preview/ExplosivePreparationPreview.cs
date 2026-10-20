using FalloutRemake.Runtime;
using Godot;

namespace FalloutRemake.Preview;

public partial class TemplePreview
{
    private bool _explosivePreparationTest;
    private ExplosivePreparationWindow? _explosivePreparation;
    private void CloseExplosivePreparation()
    {
        if (_explosivePreparation == null) return;
        _explosivePreparation.Hide(); _explosivePreparation.QueueFree(); _explosivePreparation = null;
    }
    private ExplosiveProposal ProposeExplosivePreparation(CarriedItem item, int seconds)
    {
        if (_campaign == null || _elevation != _campaign.Location.Elevation) return new(ExplosiveProposalStatus.Unavailable);
        // Explicit rehearsal profile; never invent a live Traps roll or advance a real-time clock.
        return ExplosiveUseProposal.Create(_campaign, item, seconds, SkillRollOutcome.Success, _walker?.Moving == true);
    }
    private void OpenExplosivePreparation(CarriedItem item)
    {
        ExplosiveProposal proposal = ProposeExplosivePreparation(item, ExplosiveTimer.DefaultSeconds);
        if (proposal.Status != ExplosiveProposalStatus.Ready) { NavigationStatus(ExplosivePreparationWindow.Hint(proposal.Status)); RefreshBag(); return; }
        CloseBarrierEffectReview(); CloseUseClock(); CloseExplosivePreparation(); CloseBag(); CloseLoot(); CloseInventory(); ClearRouteHover();
        GetTree().Root.GuiEmbedSubwindows = true;
        var dialog = new ExplosivePreparationWindow { Size = new(580, 320), Theme = _hud.GetChild<Control>(0).Theme,
            Propose = seconds => ProposeExplosivePreparation(item, seconds) };
        _explosivePreparation = dialog;
        dialog.SubmitRequested = plan =>
        {
            if (_explosivePreparation != dialog) return; // Late callbacks after close/load cannot affect the current session.
            if (_campaign == null || _elevation != _campaign.Location.Elevation)
            { dialog.Reject(ExplosivePreparationWindow.Hint(ExplosiveProposalStatus.Unavailable)); return; }
            ExplosiveCommitResult result = _campaign.CommitExplosiveUse(plan, _walker?.Moving == true);
            if (result.Status != ExplosiveCommitStatus.Ready)
            { dialog.Reject("A situação mudou. Feche e prepare novamente; o item foi preservado."); return; }
            CloseExplosivePreparation(); RefreshPlacedUses(); RefreshContainerVisuals(); UpdateCamera();
            NavigationStatus($"Explosivo posicionado · {result.Use!.Allocation.Schedule.SelectedSeconds} s pausados · K para salvar");
        };
        void Cancel() { if (_explosivePreparation != dialog) return; CloseExplosivePreparation(); OpenBag(); }
        dialog.Canceled += Cancel; dialog.CloseRequested += Cancel;
        _hud.AddChild(dialog); dialog.PopupCenteredClamped(new Vector2I(580, 320), .92f);
    }
}
