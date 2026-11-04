using System.Collections.Generic;
using System.Text;
using CargoToGo.Configuration;
using CargoToGo.Features.Packing.Domain;
using CargoToGo.Features.Packing.Infrastructure;
using CargoToGo.Shared;
using KydlCore.Common;

namespace CargoToGo.Testing;

/// <summary>
/// In-game tests run by the user's Kydl.GameProbe (dev tool, never a dependency: it finds this class by its NAME `InGameTests`) and by the TestLab hook CargoToGo.selftest.
///   Startup_*  at the main menu;   World_*  5 s after a world is loaded. Each returns "PASS: ..." / "FAIL: ..." / "SKIP: ..." with the numbers that decided it. Read-only.
/// </summary>
internal static class InGameTests
{
    static string Startup_ConfigBound()
    {
        bool bound = ModSettings.ContainerRange != null && ModSettings.PullFromContainers != null && ModSettings.WeightMultiplier != null;
        return bound ? "PASS: config bound (ContainerRange " + ModSettings.ContainerRange.Value + " m, weight x" + ModSettings.WeightMultiplier.Value + ")" : "FAIL: the config entries are not bound";
    }

    /// The config rule (PROJECT_RULES 11): [General] EnableAll and [DebugLog] Enable exist, the mod is on.
    static string Startup_ConfigRules()
    {
        if (ModSettings.EnableAll == null || ModSettings.DebugLog == null) return "FAIL: ModSettings.Bind did not run (EnableAll / DebugLog null)";
        return ModSettings.On ? "PASS: EnableAll on, debug log " + ModSettings.DebugLog.Value : "SKIP: EnableAll is off in the config";
    }

    /// KydlCore (hard dependency) is loaded: it owns the KY hub (F6) with this mod's Settings tab.
    static string Startup_CoreLoaded()
    {
        bool loaded = BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey(KydlCore.Bootstrap.Plugin.ModGuid);
        ModLog.Verbose("InGameTests: Startup_CoreLoaded " + loaded);
        return loaded ? "PASS: " + KydlCore.Bootstrap.Plugin.ModGuid + " is loaded" : "FAIL: " +
            KydlCore.Bootstrap.Plugin.ModGuid +
            " is not loaded (this mod requires it)";
    }

    /// All player-facing texts are registered (a missing key would show as the raw key in game).
    static string Startup_TextsRegistered()
    {
        var missing = new List<string>();
        foreach (var kv in CgText.En)
            if (string.IsNullOrEmpty(Loc.T(kv.Key)) || Loc.T(kv.Key) == kv.Key) missing.Add(kv.Key);
        return missing.Count == 0 ? "PASS: " + CgText.En.Count + " texts registered" : "FAIL: not translated: " + string.Join(", ", missing.ToArray());
    }

    /// One cargo item exists in the item database for pieces of the hammer (CargoFactory.Ensure ran).
    static string World_CargoItemsExist()
    {
        var db = ObjectDB.instance;
        if (db == null) return "FAIL: no ObjectDB in a loaded world";
        int n = 0;
        foreach (var go in db.m_items) if (go != null && CargoNames.IsCargo(go.name)) n++;
        ModLog.Verbose("InGameTests: World_CargoItemsExist " + n);
        return n > 0 ? "PASS: " + n + " cargo item(s) in the ObjectDB" : "FAIL: no " + CargoFactory.PrefabPrefix + "* item in the ObjectDB";
    }

    /// Every cargo is consistent with the piece it stands for: the piece exists and has materials, the weight is the materials' weight times the multiplier,
    /// the portal flag is exactly "every material may pass a portal", it is a single non-stacking item.
    static string World_CargoContentsConsistent()
    {
        var db = ObjectDB.instance;
        if (db == null || ZNetScene.instance == null) return "SKIP: no world";
        int checkedCount = 0;
        var bad = new StringBuilder();
        foreach (var go in db.m_items)
        {
            if (go == null || !CargoNames.IsCargo(go.name)) continue;
            checkedCount++;
            var drop = go.GetComponent<ItemDrop>();
            var piece = CargoFactory.PieceFromTables(CargoNames.PieceOf(go.name));
            var pieceGo = piece != null ? piece.gameObject : null;
            if (drop == null) { bad.Append(go.name).Append(": no ItemDrop; "); continue; }
            if (pieceGo == null) { bad.Append(go.name).Append(": piece prefab not in ZNetScene; "); continue; }
            if (piece == null) { bad.Append(go.name).Append(": prefab has no Piece; "); continue; }
            if (piece.m_resources == null || piece.m_resources.Length == 0) { bad.Append(go.name).Append(": piece has no materials (").Append(piece.m_resources == null ? "null" : "0").Append("); "); continue; }
            var sd = drop.m_itemData.m_shared;
            float weight = 0f;
            foreach (var r in piece.m_resources)
                if (r.m_resItem != null && r.m_amount > 0) weight += r.m_amount * r.m_resItem.m_itemData.m_shared.m_weight;
            weight *= ModSettings.WeightMultiplier.Value;
            string blocker;
            if (System.Math.Abs(sd.m_weight - weight) > 0.01f) bad.Append(go.name).Append(": weight ").Append(sd.m_weight).Append(" != ").Append(weight).Append("; ");
            if (sd.m_teleportable != CargoFactory.ContentsTeleportable(piece, out blocker)) bad.Append(go.name).Append(": teleportable flag wrong; ");
            if (sd.m_maxStackSize != 1 || sd.m_autoStack) bad.Append(go.name).Append(": stacks; ");
            if (!sd.m_name.StartsWith(CargoNames.NamePrefix)) bad.Append(go.name).Append(": name without prefix; ");
        }
        if (checkedCount == 0) return "FAIL: no cargo to check";
        return bad.Length == 0 ? "PASS: " + checkedCount + " cargo(s) match their pieces" : "FAIL: " + bad;
    }
}
