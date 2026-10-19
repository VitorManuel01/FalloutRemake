using System;
using FalloutRemake.Runtime;
using Godot;

namespace FalloutRemake.Preview;

/// <summary>Shared integer quantity input. Both displayed status and click-time validation use the same callback.</summary>
public partial class QuantityTransfer : HBoxContainer
{
    private readonly SpinBox _quantity = new() { Name = "QuantityValue", MinValue = 1, MaxValue = 1, Step = 1, Rounded = true,
        UpdateOnTextChanged = true, CustomMinimumSize = new Vector2(100, 0) };
    private readonly Button _action = new() { Name = "QuantityTransferAction", Text = "Transferir quantidade", Disabled = true };
    private Func<int, ItemTakeStatus>? _check;
    private int _available;
    public Action<int>? Requested;
    private int Quantity => double.IsFinite(_quantity.Value) && _quantity.Value >= 1 && _quantity.Value <= int.MaxValue ? (int)_quantity.Value : 0;
    public override void _Ready()
    {
        AddChild(new Label { Text = "Qtd.:" }); AddChild(_quantity); AddChild(_action);
        _quantity.ValueChanged += _ => RefreshStatus();
        _action.Pressed += () =>
        {
            _quantity.Apply(); RefreshStatus();
            if (!_action.Disabled) Requested?.Invoke(Quantity);
        };
    }
    public void Configure(int available, string action, Func<int, ItemTakeStatus>? check)
    {
        _available = Math.Max(0, available); _check = check; _action.Text = action;
        Visible = _available > 1; _quantity.Editable = _available > 0;
        _quantity.MaxValue = Math.Max(1, _available); _quantity.SetValueNoSignal(1); RefreshStatus();
    }
    private void RefreshStatus()
    {
        ItemTakeStatus status = Quantity < 1 || Quantity > _available ? ItemTakeStatus.InvalidQuantity : _check?.Invoke(Quantity) ?? ItemTakeStatus.Unsupported;
        _action.Disabled = status != ItemTakeStatus.Ready;
        _action.TooltipText = _available == 0 ? "Sem quantidade disponível" : status == ItemTakeStatus.Ready
            ? $"Transferir {Quantity} {(Quantity == 1 ? "unidade" : "unidades")}" : ContainerLootWindow.Hint(status);
    }
}
