using System;
using CargoToGo.Core;

namespace CargoToGo.Features.Packing.Domain;

/// <summary>The rules of a cargo that need no game: how a requirement is split over the sources in order,
/// and how cargo prefab names relate to piece prefab names. Kept free of Unity / Valheim types so unit tests cover it.</summary>
internal static class PackPlan
{
    /// <summary>Splits <paramref name="wanted"/> over sources in order. available[i] is what source i can hand over.
    /// Result[i] is what to take from source i: never more than it has, earlier sources first, sum = min(wanted, total available).
    /// Negative counts count as 0; wanted &lt;= 0 takes nothing.</summary>
    public static int[] Allocate(int[] available, int wanted)
    {
        if (RuleLog.IsOn()) RuleLog.Every("PackPlan.Allocate", 2d, $"PackPlan.Allocate(available={available}, wanted={wanted})");
        if (available == null) { var returnedAt16 = new int[0]; if (RuleLog.IsOn()) RuleLog.Every("PackPlan.Allocate:16", 2d, $"PackPlan.Allocate: returns {returnedAt16} (line 16)"); return returnedAt16; }
        var take = new int[available.Length];
        var left = wanted;
        // Stryker disable once Equality : with left == 0 the loop body takes 0 from every source, same result (equivalent mutant)
        for (var i = 0; i < available.Length && left > 0; i++)
        {
            var n = Math.Min(left, Math.Max(0, available[i]));
            take[i] = n;
            left -= n;
        }
        { var returnedAt26 = take; if (RuleLog.IsOn()) RuleLog.Every("PackPlan.Allocate:26", 2d, $"PackPlan.Allocate: returns {returnedAt26} (line 26)"); return returnedAt26; }
    }

    /// <summary>True when the sources together hold at least <paramref name="wanted"/>. wanted &lt;= 0 is always covered.</summary>
    public static bool Covers(int[] available, int wanted)
    {
        if (RuleLog.IsOn()) RuleLog.Every("PackPlan.Covers", 2d, $"PackPlan.Covers(available={available}, wanted={wanted})");
        if (wanted <= 0) { if (RuleLog.IsOn()) RuleLog.Every("PackPlan.Covers:33", 2d, $"PackPlan.Covers: returns true (line 33) - inputs (available={available}, wanted={wanted})"); return true; }
        if (available == null) { if (RuleLog.IsOn()) RuleLog.Every("PackPlan.Covers:34", 2d, $"PackPlan.Covers: returns false (line 34) - inputs (available={available}, wanted={wanted})"); return false; }
        long sum = 0;
        foreach (var a in available)
        {
            // Stryker disable once Equality : adding a == 0 changes nothing (equivalent mutant)
            if (a > 0) sum += a;
            if (sum >= wanted) { if (RuleLog.IsOn()) RuleLog.Every("PackPlan.Covers:40", 2d, $"PackPlan.Covers: returns true (line 40) - inputs (available={available}, wanted={wanted})"); return true; }
        }
        { if (RuleLog.IsOn()) RuleLog.Every("PackPlan.Covers:42", 2d, $"PackPlan.Covers: returns false (line 42) - inputs (available={available}, wanted={wanted})"); return false; }
    }

    /// <summary>How much of a requirement could not be taken: wanted minus what was really removed (never negative).</summary>
    public static int Shortfall(int wanted, int[] took)
    {
        if (RuleLog.IsOn()) RuleLog.Every("PackPlan.Shortfall", 2d, $"PackPlan.Shortfall(wanted={wanted}, took={took})");
        long sum = 0;
        // Stryker disable once Equality : adding t == 0 changes nothing (equivalent mutant)
        if (took != null) foreach (var t in took) if (t > 0) sum += t;
        { var returnedAt52 = (int)Math.Max(0, wanted - sum); if (RuleLog.IsOn()) RuleLog.Every("PackPlan.Shortfall:52", 2d, $"PackPlan.Shortfall: returns {returnedAt52} (line 52)"); return returnedAt52; }
    }
}

/// <summary>Cargo item prefabs are named CC_Cargo_[piece prefab]; the item's display name is "Cargo: [piece name]".</summary>
internal static class CargoNames
{
    public const string PrefabPrefix = "CC_Cargo_";
    public const string NamePrefix = "Cargo: ";

    /// <summary>The cargo item prefab name for a piece prefab name.</summary>
    public static string CargoFor(string piecePrefab)
    {
        if (RuleLog.IsOn()) RuleLog.Verbose($"CargoNames.CargoFor(piecePrefab={piecePrefab})");
        { var returnedAt66 = PrefabPrefix + piecePrefab; if (RuleLog.IsOn()) RuleLog.Verbose($"CargoNames.CargoFor: returns {returnedAt66} (line 66)"); return returnedAt66; }
    }

    /// <summary>True for a name that is a cargo prefab of some piece (the prefix alone, with no piece, is not).</summary>
    public static bool IsCargo(string prefabName)
    {
        if (RuleLog.IsOn()) RuleLog.Every("CargoNames.IsCargo", 2d, $"CargoNames.IsCargo(prefabName={prefabName})");
        { var returnedAt73 = prefabName != null && prefabName.Length > PrefabPrefix.Length && prefabName.StartsWith(PrefabPrefix, StringComparison.Ordinal); if (RuleLog.IsOn()) RuleLog.Every("CargoNames.IsCargo:73", 2d, $"CargoNames.IsCargo: returns {returnedAt73} (line 73)"); return returnedAt73; }
    }

    /// <summary>The piece prefab a cargo prefab name stands for, or null when the name is not a cargo.</summary>
    public static string PieceOf(string cargoPrefab)
    {
        if (RuleLog.IsOn()) RuleLog.Every("CargoNames.PieceOf", 2d, $"CargoNames.PieceOf(cargoPrefab={cargoPrefab})");
        { var returnedAt80 = IsCargo(cargoPrefab) ? cargoPrefab.Substring(PrefabPrefix.Length) : null; if (RuleLog.IsOn()) RuleLog.Every("CargoNames.PieceOf:80", 2d, $"CargoNames.PieceOf: returns {returnedAt80} (line 80)"); return returnedAt80; }
    }
}
