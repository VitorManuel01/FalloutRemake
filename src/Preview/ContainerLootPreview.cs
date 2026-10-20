using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FalloutRemake.Geometry;
using FalloutRemake.Import;
using FalloutRemake.Runtime;
using Godot;

namespace FalloutRemake.Preview;

public partial class TemplePreview
{
    private ContainerLootWindow? _loot;
    private string? _lootOwner;
    private void RefreshLootAction() => _hud.LootAction(_selected?.Source is { Subtype: 1 } source && source.Header.Pid >> 24 == 0);
    private void CloseLoot()
    { if (_loot != null) { _loot.Hide(); _loot.QueueFree(); _loot = null; } _lootOwner = null; }
    private int? ContainerApproach(MapObject owner) => _walker?.Moving == false && owner.Header.Elevation == _elevation && _reachable != null
        ? ClosestContainerApproach(owner, _reachable, _navigation!) : null;
    private static int? ClosestContainerApproach(MapObject owner, HexReachability reach, HexNavigation navigation) =>
        Enumerable.Range(0, 6).Select(d => FalloutGrid.Neighbor(owner.Header.Tile, d)).Append(owner.Header.Tile)
            .Where(t => t.HasValue && reach.Distance(t.Value) >= 0 && !navigation.IsExit(t.Value))
            .OrderBy(t => reach.Distance(t!.Value)).ThenBy(t => t).FirstOrDefault();
    private void OpenLoot()
    {
        if (_loot?.Visible == true) { CloseLoot(); return; }
        if (_selected?.Source is not { Subtype: 1 } owner || owner.Header.Pid >> 24 != 0 || _campaign == null) return;
        CloseBarrierEffectReview(); CloseUseClock(); CloseExplosivePreparation(); CloseInventory(); ClearRouteHover(); _lootOwner = owner.Key;
        GetTree().Root.GuiEmbedSubwindows = true;
        var dialog = new ContainerLootWindow { Size = new Vector2I(900, 560), Theme = _hud.GetChild<Control>(0).Theme };
        _loot = dialog; _hud.AddChild(dialog);
        dialog.Confirmed += CloseLoot; dialog.Canceled += CloseLoot; dialog.CloseRequested += CloseLoot;
        dialog.TakeRequested = TakeFromContainer; dialog.ApproachRequested = ApproachLootContainer;
        Guid sessionToken = _campaign.SessionToken;
        dialog.TakeAllRequested = () =>
        {
            if (_loot != dialog || _campaign?.SessionToken != sessionToken || _lootOwner != owner.Key) return;
            TakeAllFromContainer();
        };
        RefreshLootWindow(); dialog.PopupCenteredClamped(new Vector2I(900, 560), .92f);
    }
    private void RefreshLootWindow()
    {
        if (_loot == null || _lootOwner == null || _campaign == null) return;
        MapObject owner = _campaign.Current.Items.Containers.Single(o => o.Key == _lootOwner);
        _loot.Refresh(OriginalText(owner)?.Name ?? "Recipiente", _campaign.Current.Items.Contents(_lootOwner), OriginalText,
            (key, qty) => _campaign.CanTakeItem(_lootOwner, key, qty, _walker?.Moving == true), _campaign.CarriedCount, ContainerApproach(owner).HasValue,
            _campaign.CarriedWeight, _campaign.CarryCapacity, item => _campaign.UnitWeight(_campaign.CurrentMap, item),
            _campaign.Current.Items.ContainerStatus(_lootOwner), item => InventoryTexture(_campaign.CurrentMap, item),
            ScriptedContainerExplanation(_campaign.Current.Map, owner),
            () => _campaign.CanTakeAllItems(_lootOwner, _walker?.Moving == true));
    }
    private static string? ScriptedContainerExplanation(MapDocument map, MapObject owner)
    {
        if (map.Source.Sha256 != TempleBarrierEffectProposal.MapHash || owner.Key != TempleBarrierEffectProposal.MapHash + ":465604"
            || owner.Header is not { Id: 100, Pid: 245, Tile: 20132, Elevation: 2, Sid: 50331654, ScriptIndex: 758 }
            || !map.ScriptBindings.Any(b => b.Sid == owner.Header.Sid && b.Index == 758
                && b.Program.Sha256 == "D80BC3F24D0936D43CA030ED4A24332B4C34CB655982BEFC64402813E451963C")) return null;
        return "Baú do desafio: no jogo original, recebe os itens do personagem por script. "
            + "O desafio e a recuperação desses itens ainda não estão disponíveis nesta prévia.";
    }
    private void TakeFromContainer(string key, int quantity)
    {
        if (_lootOwner == null || _campaign == null) return;
        ItemTakeStatus status = _campaign.TakeItem(_lootOwner, key, quantity, _walker?.Moving == true);
        NavigationStatus(status == ItemTakeStatus.Ready ? $"Item recolhido · bolsa com {_campaign.CarriedCount} {(_campaign.CarriedCount == 1 ? "unidade" : "unidades")} · K para salvar" : ContainerLootWindow.Hint(status));
        RefreshLootWindow();
        RefreshContainerVisuals();
    }
    private void TakeAllFromContainer()
    {
        if (_loot?.Visible != true || _lootOwner == null || _campaign == null) return;
        long before = _campaign.CarriedCount;
        ItemTakeStatus status = _campaign.TakeAllItems(_lootOwner, _walker?.Moving == true);
        NavigationStatus(status == ItemTakeStatus.Ready ? $"Recolhidas {_campaign.CarriedCount - before} unidade(s) · K para salvar" : ContainerLootWindow.Hint(status));
        RefreshLootWindow(); RefreshContainerVisuals();
    }
    private void ApproachLootContainer()
    {
        if (_lootOwner == null || _campaign == null) return;
        MapObject owner = _campaign.Current.Items.Containers.Single(o => o.Key == _lootOwner);
        int? target = ContainerApproach(owner); if (!target.HasValue) return;
        CloseLoot(); if (!_walkMode) ToggleWalkMode(); RequestWalk(target.Value);
    }
    private async Task CheckContainerLoot()
    {
        _checkingNavigation = true;
        try
        {
            ResetWalker(); ToggleWalkMode();
            PreviewObject pot = _layout!.Objects.Single(o => o.Source.Offset == 206348); SelectObject(pot);
            using var key = new InputEventKey { Pressed = true, PhysicalKeycode = Key.J };
            _UnhandledInput(key);
            if (_loot == null || !_loot.StatusText.Contains("Sem caminho")) throw new InvalidDataException("Unreachable container offered an approach.");
            CloseLoot(); RequestWalk(24508); _walker!.Advance(1000); UpdateWalkerPosition(); UseDoor(_layout.Objects.Single(o => o.Source.Offset == 244040).Source.Key);
            SelectObject(pot); _UnhandledInput(key);
            var one = (Button)_loot!.FindChild("TakeOneAction", true, false);
            var approach = (Button)_loot.FindChild("ApproachContainerAction", true, false);
            if (!one.Disabled || approach.Disabled || _campaign!.CarriedCount != 0) throw new InvalidDataException("Distant container allowed immediate transfer.");
            approach.EmitSignal(Button.SignalName.Pressed);
            if (_loot != null || !_walker.Moving || !_walkMode) throw new InvalidDataException("Container approach failed to start walking.");
            _walker.Advance(.05); UpdateWalkerPosition(); var position = _walker.Position;
            _UnhandledInput(key); await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (_loot == null || !_loot.StatusText.Contains("aguarde") || _walker.Position != position) throw new InvalidDataException("Loot window failed to pause movement or gate transfer.");
            _loot.EmitSignal(Window.SignalName.WindowInput, key); _walker.Advance(1000); UpdateWalkerPosition();
            if (_loot != null) throw new InvalidDataException("J did not close the loot window.");
            var location = _campaign.Location; var doors = _doors!.Capture(); int roots = _lookup.Count;
            var hudButton = (Button)_hud.FindChild("LootAction", true, false); hudButton.EmitSignal(Button.SignalName.Pressed);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            one = (Button)_loot!.FindChild("TakeOneAction", true, false);
            Vector2 viewport = GetViewport().GetVisibleRect().Size;
            if (one.Disabled || !_loot.ReadableLayout || _loot.Remaining != 1 || _loot.SelectedWeightText != "1 lb" || _loot.SelectedArtFid != 0x070000AA || !_loot.LoadText.Contains("0 / 150 lb")
                || _loot.Size.X > viewport.X - 32 || _loot.Size.Y > viewport.Y - 32)
                throw new InvalidDataException($"Nearby original item was not readable or collectable: disabled={one.Disabled}, list={_loot.ListSize}, window={_loot.Size}, remaining={_loot.Remaining}, actor={_campaign.Location.Hex}, moving={_walker.Moving}, status={_loot.StatusText}.");
            one.EmitSignal(Button.SignalName.Pressed);
            if (_campaign.CarriedCount != 1 || _campaign.Carried.Single().Source.Header.Id != 106 || _loot.Remaining != 0
                || !one.Disabled || !_loot.StatusText.Contains("já foi recolhida") || _loot.SelectedWeightText != "0 lb" || !_loot.LoadText.Contains("1 / 150 lb"))
                throw new InvalidDataException("Loot action lost quantity/identity/weight or allowed duplication.");
            one.EmitSignal(Button.SignalName.Pressed);
            if (_campaign.CarriedCount != 1 || _campaign.Location != location || _lookup.Count != roots || !_doors.Capture().Doors.SequenceEqual(doors.Doors))
                throw new InvalidDataException("Repeated or modal collection changed unrelated world state.");
            if (_capture != null) await Capture(Path.ChangeExtension(_capture, "loot.png"));
            _loot.EmitSignal(AcceptDialog.SignalName.Confirmed); OpenInventory();
            if (_inventory?.ItemRows != 1 || _inventory.SelectedArtFid != 0x070000AA || _campaign.CarriedCount != 1)
                throw new InvalidDataException("Original inventory artwork changed after collecting its runtime contents.");
            CloseInventory(); SaveCheckpoint(); ResetWalker(); LoadCheckpoint();
            SelectObject(_layout!.Objects.Single(o => o.Source.Offset == 206348)); _UnhandledInput(key);
            if (_loot!.Remaining != 0 || _campaign.CarriedCount != 1) throw new InvalidDataException("Reopened container duplicated checkpoint contents.");
            CloseLoot(); MapLayoutWriter.Verify(_map!); ResetWalker();
            GD.Print("CONTAINER_LOOT_OK: J/button, blocked/distant approach, paused modal movement, original powder ID 106, exact remaining quantity, repeat refusal, checkpoint/reopen, unchanged location/doors/root objects/source bytes verified.");
        }
        finally { CloseLoot(); _checkingNavigation = false; }
    }
    private async Task CheckScriptedContainer()
    {
        _checkingNavigation = true;
        try
        {
            ResetWalker(); var location = _campaign!.Location; var doors = _doors!.Capture();
            ChangeElevation(2); SelectObject(_layout!.Objects.Single(o => o.Source.Offset == 465604));
            OpenLoot(); await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (_loot == null || _loot.Remaining != 0 || !_loot.StatusText.Contains("Baú do desafio")
                || !_loot.StatusText.Contains("recuperação desses itens ainda não")
                || _campaign.Current.Items.ContainerStatus(_lootOwner!) != ItemTakeStatus.ScriptRequired
                || !((Button)_loot.FindChild("TakeOneAction", true, false)).Disabled
                || !((Button)_loot.FindChild("TakeStackAction", true, false)).Disabled
                || !((Button)_loot.FindChild("TakeAllAction", true, false)).Disabled
                || !((Button)_loot.FindChild("ApproachContainerAction", true, false)).Disabled)
                throw new InvalidDataException("Empty scripted chest concealed its unsupported interaction or enabled an action.");
            var source = _selected!.Source;
            if (ScriptedContainerExplanation(_map!, source with { Header = source.Header with { Pid = 246 } }) != null
                || ScriptedContainerExplanation(_map! with { Source = _map.Source with { Sha256 = new string('0', 64) } }, source) != null)
                throw new InvalidDataException("Known chest explanation leaked to another source profile.");
            // Refreshing the same dialog must clear a previous source-specific explanation.
            _loot.Refresh("Recipiente especial", Array.Empty<ContainerItem>(), _ => null, (_, _) => ItemTakeStatus.ScriptRequired,
                0, false, 0, _campaign.CarryCapacity, _ => null, ItemTakeStatus.ScriptRequired);
            if (!_loot.StatusText.Contains("interação ainda não disponível") || _loot.StatusText.Contains("Baú do desafio"))
                throw new InvalidDataException("A generic scripted container retained a known chest explanation.");
            RefreshLootWindow();
            if (_capture != null) await Capture(Path.ChangeExtension(_capture, "scripted-chest.png"));
            if (_campaign.Location != location || _campaign.CarriedCount != 0 || !_doors.Capture().Doors.SequenceEqual(doors.Doors))
                throw new InvalidDataException("Inspecting scripted storage changed world state.");
            MapLayoutWriter.Verify(_map!); CloseLoot(); ResetWalker();
            GD.Print("SCRIPTED_CONTAINER_OK: known chest role and recovery limitation, generic fallback/reset, actions disabled, unchanged actor/items/doors/source bytes verified.");
        }
        finally { CloseLoot(); _checkingNavigation = false; }
    }
}
