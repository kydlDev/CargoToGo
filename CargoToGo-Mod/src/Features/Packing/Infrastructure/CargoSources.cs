using UnityEngine;
using CargoToGo.Core;
using KydlCore.Common;

namespace CargoToGo.Features.Packing.Infrastructure;

internal enum SourceKind { Inventory, Chest, Azu }

/// <summary>One place a cargo can pull materials from: the player's inventory, a chest found by
/// CargoToGo's own radius search, or a container reached through AzuCraftyBoxes.
/// Nothing in here may reference AzuCraftyBoxes types (it is an optional dependency).</summary>
internal abstract class Source
{
    public abstract SourceKind Kind { get; }

    /// <summary>How many of the item this source can hand over right now.</summary>
    /// <param name="sharedName">Item shared name, e.g. "$item_wood".</param>
    /// <param name="itemPrefab">Item prefab name, e.g. "Wood".</param>
    public abstract int Count(string sharedName, string itemPrefab);

    /// <summary>Removes up to <paramref name="amount"/> of the item and returns how many were really removed.</summary>
    public abstract int Take(string sharedName, string itemPrefab, int amount);
}

/// <summary>The player's own inventory (Cont == null) or a vanilla chest found by CargoToGo itself.</summary>
internal sealed class InventorySource : Source
{
    private readonly Inventory _inv;
    private readonly Container _cont;

    public InventorySource(Inventory inv, Container cont)
    {
        if (RuleLog.IsOn()) ModLog.Every("InventorySource.InventorySource", 2d, $"InventorySource.InventorySource(inv={inv}, cont={cont})");
        _inv = inv;
        _cont = cont;
    }

    public override SourceKind Kind { get { return _cont != null ? SourceKind.Chest : SourceKind.Inventory; } }

    public override int Count(string sharedName, string itemPrefab)
    {
        if (RuleLog.IsOn()) ModLog.Every("InventorySource.Count", 2d, $"InventorySource.Count(sharedName={sharedName}, itemPrefab={itemPrefab})");
        { var returnedAt43 = _inv.CountItems(sharedName, -1, true); if (RuleLog.IsOn()) ModLog.Every("InventorySource.Count:43", 2d, $"InventorySource.Count: returns {returnedAt43} (line 43)"); return returnedAt43; }
    }

    public override int Take(string sharedName, string itemPrefab, int amount)
    {
        if (RuleLog.IsOn()) ModLog.Every("InventorySource.Take", 2d, $"InventorySource.Take(sharedName={sharedName}, itemPrefab={itemPrefab}, amount={amount})");
        var take = Mathf.Min(amount, _inv.CountItems(sharedName, -1, true));
        if (take <= 0) { if (RuleLog.IsOn()) ModLog.Every("InventorySource.Take:50", 2d, $"InventorySource.Take: returns 0 (line 50) - inputs (sharedName={sharedName}, itemPrefab={itemPrefab}, amount={amount})"); return 0; }
        if (_cont != null) _cont.GetComponent<ZNetView>().ClaimOwnership();
        _inv.RemoveItem(sharedName, take, -1, true);
        { var returnedAt53 = take; if (RuleLog.IsOn()) ModLog.Every("InventorySource.Take:53", 2d, $"InventorySource.Take: returns {returnedAt53} (line 53)"); return returnedAt53; }
    }
}
