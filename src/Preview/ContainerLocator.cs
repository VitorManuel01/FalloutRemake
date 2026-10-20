using System;
using System.Linq;
using FalloutRemake.Geometry;
using FalloutRemake.Runtime;
using Godot;

namespace FalloutRemake.Preview;

public partial class PreviewHud
{
    private readonly OptionButton _containerLocator = new() { Name = "ContainerLocator", FitToLongestItem = false,
        CustomMinimumSize = new(0, 32), TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
        TooltipText = "Enquadra um recipiente desta elevação sem mover o personagem" };
    public Action<string>? ContainerLocateRequested;
    private void AddContainerLocator(VBoxContainer column)
    {
        column.AddChild(_containerLocator);
        _containerLocator.ItemSelected += index => {
            if (index > 0 && index < _containerLocator.ItemCount) ContainerLocateRequested?.Invoke(_containerLocator.GetItemMetadata((int)index).AsString());
        };
    }
    public void Containers((string Key, string Label)[] choices, string? selectedKey)
    {
        _containerLocator.Clear(); _containerLocator.AddItem("Localizar recipiente…");
        int selected = 0;
        foreach (var choice in choices)
        {
            int index = _containerLocator.ItemCount; _containerLocator.AddItem(choice.Label);
            _containerLocator.SetItemMetadata(index, choice.Key); _containerLocator.GetPopup().SetItemTooltip(index, choice.Label);
            if (choice.Key == selectedKey) selected = index;
        }
        _containerLocator.Disabled = choices.Length == 0; _containerLocator.Select(selected);
    }
}

public partial class TemplePreview
{
    private void RefreshContainerLocator()
    {
        if (_layout == null || _campaign == null) return;
        var choices = _layout.Objects.Where(o => IsContainer(o) && VisibleObject(o)).OrderBy(o => o.Source.Offset).Select(o => {
            var contents = _campaign.Current.Items.Contents(o.Source.Key).Where(i => i.Quantity > 0).ToArray();
            bool special = _campaign.Current.Items.ContainerStatus(o.Source.Key) != ItemTakeStatus.Ready;
            string detail = special ? "interação especial" : contents.Length == 0 ? "vazio"
                : string.Join(", ", contents.Take(2).Select(i => OriginalText(i.Source)?.Name ?? "item")) + $" · {contents.Sum(i => (long)i.Quantity)} un.";
            return (o.Source.Key, $"{OriginalText(o.Source)?.Name ?? "Recipiente"} · {detail}");
        }).ToArray();
        _hud.Containers(choices, _selected?.Source.Key);
    }
    private void LocateContainer(string key)
    {
        if (InspectorOpen || _layout == null) return;
        PreviewObject? obj = _layout.Objects.FirstOrDefault(o => o.Source.Key == key && IsContainer(o) && VisibleObject(o));
        if (obj == null) return;
        ClearRouteHover(); SetCameraFollow(false); SelectObject(obj); _focus = Point(obj.Position, 0); _camera.Size = 18; UpdateCamera();
        RefreshContainerLocator();
    }
}
