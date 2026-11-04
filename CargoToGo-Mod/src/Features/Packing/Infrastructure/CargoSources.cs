using UnityEngine;

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
        _inv = inv;
        _cont = cont;
    }

    public override SourceKind Kind { get { return _cont != null ? SourceKind.Chest : SourceKind.Inventory; } }

    public override int Count(string sharedName, string itemPrefab)
    {
        return _inv.CountItems(sharedName, -1, true);
    }

    public override int Take(string sharedName, string itemPrefab, int amount)
    {
        var take = Mathf.Min(amount, _inv.CountItems(sharedName, -1, true));
        if (take <= 0) return 0;
        if (_cont != null) _cont.GetComponent<ZNetView>().ClaimOwnership();
        _inv.RemoveItem(sharedName, take, -1, true);
        return take;
    }
}
