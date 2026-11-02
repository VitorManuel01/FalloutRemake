using System.Collections.Generic;
using System.Linq;
using FalloutRemake.Geometry;
using FalloutRemake.Runtime;
using FalloutRemake.Runtime.Combat;
using Godot;

namespace FalloutRemake.Preview;

public partial class TemplePreview
{
    private Node3D? _groundLayer;
    private readonly List<Node3D> _groundNodes = new();
    private BoxMesh? _groundMesh;
    private StandardMaterial3D? _groundMaterial;

    // Thrown items lying on this map and elevation: a long bar with its name, in their own layer (never MAP roots).
    private void RefreshGroundItems()
    {
        if (_groundLayer != null) { _world.RemoveChild(_groundLayer); _groundLayer.QueueFree(); }
        _groundLayer = new Node3D { Name = "GroundItems" }; _world.AddChild(_groundLayer);
        _groundNodes.Clear();
        if (_campaign == null || _layout == null) return;
        _groundMesh ??= _resources.Own(new BoxMesh { Size = new Vector3(1.1f, .07f, .09f) });
        _groundMaterial ??= _resources.Own(new StandardMaterial3D { AlbedoColor = new Color("c9a46b"), Roughness = .8f });
        foreach (var (sourceMap, item) in _campaign.GroundItems.Where(g => g.Item.Place.Map == _campaign.CurrentMap && g.Item.Place.Elevation == _elevation))
        {
            var node = new Node3D { Name = $"Ground_{item.ItemKey.Replace(':', '_')}_{item.Place.Hex}", Position = Point(FalloutGrid.HexCenter(item.Place.Hex) - _layout.Origin, .06f) };
            node.SetMeta("source_map", sourceMap); node.SetMeta("item_key", item.ItemKey); node.SetMeta("hex", item.Place.Hex);
            node.AddChild(new MeshInstance3D { Mesh = _groundMesh, MaterialOverride = _groundMaterial, RotationDegrees = new Vector3(0, 35, 0) });
            var caption = new Label3D { Name = "GroundLabel", Text = $"{GroundName(sourceMap, item)}{(item.Quantity > 1 ? $" ×{item.Quantity}" : "")}\nJ recolher ao lado",
                Position = new Vector3(0, 1.2f, 0), FontSize = 30, PixelSize = .012f, Modulate = new Color("f2d9a8"),
                Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, NoDepthTest = true };
            node.AddChild(caption); RegisterWorldLabel(caption);
            _groundLayer.AddChild(node); _groundNodes.Add(node);
        }
    }

    private string GroundName(int sourceMap, GroundItem item) =>
        _campaign!.GroundSource(sourceMap, item) is { } source && OriginalText(source)?.Name is { } name ? ShortName(name) : item.ItemKey;

    /// <summary>J beside a thrown item picks it up (CE actionPickUp), in a fight too on the player's turn; false when none lies within reach.</summary>
    private bool PickUpGroundItem()
    {
        if (_campaign == null) return false;
        var near = _campaign.GroundItems.Where(g => g.Item.Place.Map == _campaign.CurrentMap && g.Item.Place.Elevation == _campaign.Location.Elevation
            && FalloutGrid.Distance(_campaign.Location.Hex, g.Item.Place.Hex) <= 1).ToArray();
        if (near.Length == 0) return false;
        var (sourceMap, item) = near[0];
        string name = GroundName(sourceMap, item);
        ItemTakeStatus status = _campaign.PickUp(sourceMap, item, _walker?.Moving == true);
        NavigationStatus(status == ItemTakeStatus.Ready ? $"{name} recolhido para a bolsa · B para empunhar"
            : status == ItemTakeStatus.Busy ? "Aguarde: termine de andar ou espere o seu turno."
            : status == ItemTakeStatus.OverCapacity ? $"{name} pesa demais para a carga atual." : $"{name}: não dá para recolher agora.");
        RefreshGroundItems(); RefreshBag(); RefreshDoorAction();
        return true;
    }

    // Z switches the striking hand between its weapon's attacks (the spear's thrust and throw), in a fight on the player's turn.
    private void ToggleAttackMode()
    {
        if (_campaign == null) return;
        int logged = _combat?.Log.Count ?? 0;
        HandStatus status = _campaign.ToggleAttackMode(_walker?.Moving == true);
        if (status == HandStatus.Ready && _combat != null) { _combatWeapon = NamedWeapon(_combat.Get("player").Weapon); ApplyCombatEvents(_combat.Log.Skip(logged).ToArray()); }
        Hand hand = _combat?.Get("player") is { } player ? player.RightHandActive ? Hand.Right : Hand.Left : _campaign.ActiveHand;
        CombatWeapon weapon = _campaign.HandWeapon(hand);
        NavigationStatus(status switch
        {
            HandStatus.Ready => $"{HandBlowName(hand)}: {(weapon.Throws ? $"arremesso · {weapon.ActionPoints} AP, até {weapon.Range} hexes (3 × FOR no máximo)" : $"golpe · {weapon.ActionPoints} AP")} · {HandsLine()}",
            HandStatus.NoItem => "A mão ativa está vazia: só soco ou chute.",
            HandStatus.Unsupported => "Esta arma tem um só modo de ataque.",
            HandStatus.InCombat => "Aguarde: é o turno da criatura.",
            _ => "Pare de andar para trocar o modo de ataque."
        });
        RefreshDoorAction();
    }
}
