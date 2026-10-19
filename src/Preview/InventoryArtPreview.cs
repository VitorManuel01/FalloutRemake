using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FalloutRemake.Import;
using Godot;

namespace FalloutRemake.Preview;

public partial class TemplePreview
{
    private InventoryArtCatalog? _inventoryArt;
    private readonly Dictionary<string, ImageTexture> _itemTextures = new();
    private readonly HashSet<int> _unavailableItemArt = new();
    private Texture2D? InventoryTexture(int sourceMap, MapObject item)
    {
        var area = _campaign?.Areas.SingleOrDefault(a => a.Map.Prefix.Header.MapIndex == sourceMap);
        int? fid = area?.Map.Prototypes.SingleOrDefault(p => p.Pid == item.Header.Pid)?.InventoryFid;
        if (fid == null || fid == -1 || _inventoryArt == null || _unavailableItemArt.Contains(fid.Value)) return null;
        try
        {
            InventoryArt art = _inventoryArt.Get(fid.Value)!;
            string palette = _inventoryArt.Dependencies.Single(d => d.Path == "color.pal").Sha256;
            string key = $"{fid}:{palette}:{art.Source.Path}:{art.Source.Sha256}";
            if (_itemTextures.TryGetValue(key, out ImageTexture? texture)) return texture;
            if (_itemTextures.Count >= 512) throw new InvalidDataException("Inventory texture cache limit reached.");
            using var image = Image.CreateFromData(art.Width, art.Height, false, Image.Format.Rgba8, art.Rgba.ToArray());
            texture = _resources.Own(ImageTexture.CreateFromImage(image)); texture.SetMeta("inventory_fid", fid.Value);
            texture.SetMeta("source_hash", art.Source.Sha256); texture.SetMeta("palette_hash", palette);
            _itemTextures.Add(key, texture); return texture;
        }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException)
        { _unavailableItemArt.Add(fid.Value); GD.Print($"ITEM_ART_UNAVAILABLE: FID {fid} · {error.Message}"); return null; }
    }
    private async Task CheckInventoryArtUi()
    {
        _checkingNavigation = true; InventoryArtCatalog? original = _inventoryArt;
        try
        {
            ResetWalker(); var location = _campaign!.Location;
            var powder = _map!.AllObjects.Single(o => o.Offset == 206440);
            Texture2D icon = InventoryTexture(3, powder) ?? throw new InvalidDataException("Original item icon missing.");
            if (icon.GetWidth() != 47 || icon.GetHeight() != 63 || !ReferenceEquals(icon, InventoryTexture(3, powder)))
                throw new InvalidDataException("Item icon dimensions or cache identity changed.");
            _inventoryArt = new InventoryArtCatalog(_ => throw new FileNotFoundException("Synthetic missing icon fixture."));
            if (InventoryTexture(3, powder) != null || !_unavailableItemArt.Contains(0x070000AA)) throw new InvalidDataException("Missing item art failed to fall back.");
            _inventoryArt = original; _unavailableItemArt.Clear();
            SelectObject(_layout!.Objects.Single(o => o.Source.Offset == 206348)); OpenLoot();
            if (_loot!.SelectedArtFid != 0x070000AA) throw new InvalidDataException("Loot failed to use restored original artwork.");
            if (_capture != null) await Capture(Path.ChangeExtension(_capture, "item-icon.png"));
            var line = _campaign.Current.Items.Contents(_lootOwner!).Single();
            _loot.Refresh("Arte indisponível (teste)", new[] { line }, OriginalText, (_, _) => Runtime.ItemTakeStatus.Ready,
                0, false, 0, 150, _ => 1, artwork: _ => null);
            if (_loot.SelectedArtFid != null || ((Button)_loot.FindChild("TakeOneAction", true, false)).Disabled)
                throw new InvalidDataException("Missing art disabled an otherwise supported transfer.");
            CloseLoot(); OpenBag(); if (_bag!.SelectedArtFid != null) throw new InvalidDataException("Empty bag retained an old icon.");
            if (_campaign.Location != location || _campaign.CarriedCount != 0) throw new InvalidDataException("Artwork inspection modified the world.");
            CloseBag(); ChangeElevation(2);
            SelectObject(_layout!.Objects.Single(o => o.Source.Inventory.Any(i => i.Object.Header.Pid == 438))); OpenInventory();
            if (_inventory?.SelectedArtFid != 0x0700012F || !_inventory.DetailText.Contains("Temple Key") || _campaign.Location != location || _campaign.CarriedCount != 0)
                throw new InvalidDataException("Read-only key artwork altered the actor or inventory.");
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (!_inventory.ReadableLayout) throw new InvalidDataException("Original item artwork made the source inspector unreadable.");
            if (_capture != null) await Capture(Path.ChangeExtension(_capture, "original-key.png"));
            CloseInventory(); ChangeElevation(0);
            MapLayoutWriter.Verify(_map); CloseBag(); ResetWalker();
            GD.Print("INVENTORY_ART_UI_OK: original powder FID/dimensions, shared texture identity, unavailable-art fallback, supported action preserved, empty-icon cleanup, inactive-source bag icon, read-only Temple Key inspection without transfer/teleport and unchanged actor/items/source verified.");
        }
        finally { _inventoryArt = original; _unavailableItemArt.Clear(); CloseInventory(); CloseLoot(); CloseBag(); _checkingNavigation = false; }
    }
}
