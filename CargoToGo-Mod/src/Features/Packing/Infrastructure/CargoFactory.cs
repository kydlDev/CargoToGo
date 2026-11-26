using System.Collections.Generic;
using System.Text;
using CargoToGo.Configuration;
using CargoToGo.Features.Packing.Domain;
using HarmonyLib;
using KydlCore.Common;
using UnityEngine;
using CargoToGo.Core;

namespace CargoToGo.Features.Packing.Infrastructure;

/// <summary>Creates one cargo item prefab per buildable piece (CC_Cargo_[piece prefab]).
/// The contents of a cargo are implicit: exactly the piece's m_resources.</summary>
internal static class CargoFactory
{
    public const string PrefabPrefix = CargoNames.PrefabPrefix;
    public const string NamePrefix = CargoNames.NamePrefix;
    private static GameObject _holder;
    private static int RestrictedCount;   // cargos created in the current Ensure() pass that can't go through portals

    private static GameObject Holder()
    {
        if (RuleLog.IsOn()) ModLog.Every("CargoFactory.Holder", 2d, $"CargoFactory.Holder: holder exists {_holder != null}");
        if (_holder == null)
        {
            _holder = new GameObject("CargoToGoPrefabs");
            _holder.SetActive(false);
            Object.DontDestroyOnLoad(_holder);
        }
        { var returnedAt30 = _holder; if (RuleLog.IsOn()) ModLog.Every("CargoFactory.Holder:30", 2d, $"CargoFactory.Holder: returns {returnedAt30} (line 30)"); return returnedAt30; }
    }

    public static void Ensure(ObjectDB db)
    {
        if (db == null || db.m_items == null || db.m_items.Count == 0) { if (RuleLog.IsOn()) ModLog.Verbose($"CargoFactory.Ensure: returns (line 35) - inputs (db={db})"); return; }

        var existing = new HashSet<string>();
        var existingCargo = new Dictionary<string, GameObject>();
        GameObject basePrefab = null, fallback = null;
        foreach (var go in db.m_items)
        {
            if (go == null) continue;
            existing.Add(go.name);
            if (go.name == "CargoCrate") basePrefab = go;
            else if (go.name == "Stone") fallback = go;
            else if (CargoNames.IsCargo(go.name)) existingCargo[go.name] = go;
        }
        if (basePrefab == null) basePrefab = fallback;
        if (basePrefab == null) { ModLog.Error("Neither 'CargoCrate' nor 'Stone' found in ObjectDB (" + existingCargo.Count + " cargo prefabs seen); no cargo items were registered."); return; }
        if (basePrefab.name != "CargoCrate") ModLog.Warn("Prefab 'CargoCrate' not found, using '" + basePrefab.name + "' as the cargo base.");

        var added = 0;
        RestrictedCount = 0;
        foreach (var go in db.m_items.ToArray())
        {
            var drop = go != null ? go.GetComponent<ItemDrop>() : null;
            var table = drop != null ? drop.m_itemData.m_shared.m_buildPieces : null;
            if (table == null || table.m_pieces == null) continue;
            foreach (var pg in table.m_pieces)
            {
                var piece = pg != null ? pg.GetComponent<Piece>() : null;
                if (piece == null || piece.m_icon == null || piece.m_resources == null || piece.m_resources.Length == 0) continue;
                var pname = Utils.GetPrefabName(pg);
                var cname = PrefabPrefix + pname;
                if (!existing.Add(cname))
                {
                    // Already registered by an earlier pass: just make sure its portal flag matches the materials as they are now.
                    GameObject known;
                    if (existingCargo.TryGetValue(cname, out known))
                    {
                        var kd = known.GetComponent<ItemDrop>();
                        string kb;
                        if (kd != null) kd.m_itemData.m_shared.m_teleportable = ContentsTeleportable(piece, out kb);
                    }
                    continue;
                }
                try { if (Create(db, basePrefab, cname, piece)) added++; }
                catch (System.Exception e) { ModLog.Error("Failed to create cargo '" + cname + "'" + " | " + e); }
            }
        }
        if (added > 0)
        {
            var upd = AccessTools.Method(typeof(ObjectDB), "UpdateRegisters");
            if (upd == null) ModLog.Error("ObjectDB.UpdateRegisters not found; " + added + " new cargo items may not be resolvable.");
            else upd.Invoke(db, null);
            ModLog.Info("Registered " + added + " new cargo items in ObjectDB (cloned from '" + basePrefab.name + "'); " + RestrictedCount + " of them contain materials that can't pass through portals.");
        }
    }

    private static bool Create(ObjectDB db, GameObject basePrefab, string cname, Piece piece)
    {
        var clone = Object.Instantiate(basePrefab, Holder().transform);
        clone.name = cname;
        var drop = clone.GetComponent<ItemDrop>();
        if (drop == null) { ModLog.Warn("Base prefab has no ItemDrop; skipped '" + cname + "'"); Object.Destroy(clone); return false; }

        var sd = drop.m_itemData.m_shared;
        var desc = new StringBuilder("Sealed materials for building this piece. They can only be used for that.\nCtrl+Alt+Right-click destroys the cargo and returns the materials.\n");
        float weight = 0f;
        foreach (var r in piece.m_resources)
        {
            if (r.m_resItem == null || r.m_amount <= 0) continue;
            weight += r.m_amount * r.m_resItem.m_itemData.m_shared.m_weight;
            desc.Append("\n").Append(r.m_amount).Append("x ").Append(r.m_resItem.m_itemData.m_shared.m_name);
        }

        // Portal rules: a cargo is exactly as teleportable as the materials inside it (ores, ingots, ... are not).
        // The world modifiers (TeleportAll / NoPortals) are applied by vanilla Inventory.IsTeleportable on top of this flag.
        string blocker;
        var teleportable = ContentsTeleportable(piece, out blocker);
        if (!teleportable)
        {
            desc.Append("\n\nContains materials that can't pass through portals (unless the world's portal rules allow everything).");
            RestrictedCount++;
            ModLog.Verbose("Cargo '" + cname + "' is portal-restricted: contains '" + blocker + "'.");
        }

        sd.m_name = NamePrefix + piece.m_name;
        sd.m_description = desc.ToString();
        sd.m_itemType = ItemDrop.ItemData.ItemType.Material;
        sd.m_icons = new[] { piece.m_icon };
        sd.m_variants = 0;
        sd.m_maxStackSize = 1;
        sd.m_autoStack = false;
        sd.m_maxQuality = 1;
        sd.m_weight = weight * ModSettings.WeightMultiplier.Value;
        sd.m_teleportable = teleportable;
        sd.m_questItem = false;
        sd.m_dlc = "";
        sd.m_value = 0;
        sd.m_equipStatusEffect = null;
        sd.m_movementModifier = 0f;
        sd.m_useDurability = false;
        sd.m_canBeReparied = false;
        drop.m_itemData.m_dropPrefab = clone;
        db.m_items.Add(clone);
        { if (RuleLog.IsOn()) ModLog.Every("CargoFactory.Create:137", 2d, $"CargoFactory.Create: returns true (line 137) - inputs (db={db}, basePrefab={basePrefab}, cname={cname}, piece={piece})"); return true; }
    }

    // ---------- portal rules ----------
    /// <summary>True only if every material of the piece is itself teleportable (vanilla m_teleportable flag).
    /// Metal ores / ingots / scrap etc. are not, so a cargo holding them isn't either. Follows modded items too,
    /// because it reads the live flag instead of a hard-coded list. Unknown piece -> false (fail closed).</summary>
    public static bool ContentsTeleportable(Piece piece, out string blocker)
    {
        if (RuleLog.IsOn()) ModLog.Every("CargoFactory.ContentsTeleportable", 2d, $"CargoFactory.ContentsTeleportable(piece={piece})");
        blocker = null;
        if (piece == null || piece.m_resources == null) { blocker = "(unknown contents)"; { if (RuleLog.IsOn()) ModLog.Every("CargoFactory.ContentsTeleportable:148", 2d, $"CargoFactory.ContentsTeleportable: returns false (line 148) - inputs (piece={piece})"); return false; } }
        foreach (var r in piece.m_resources)
        {
            if (r.m_resItem == null || r.m_amount <= 0) continue;
            var sd = r.m_resItem.m_itemData.m_shared;
            if (!sd.m_teleportable) { blocker = sd.m_name; { if (RuleLog.IsOn()) ModLog.Every("CargoFactory.ContentsTeleportable:153", 2d, $"CargoFactory.ContentsTeleportable: returns false (line 153) - inputs (piece={piece})"); return false; } }
        }
        { if (RuleLog.IsOn()) ModLog.Every("CargoFactory.ContentsTeleportable:155", 2d, $"CargoFactory.ContentsTeleportable: returns true (line 155) - inputs (piece={piece})"); return true; }
    }

    /// <summary>Re-evaluates one cargo's flag from what is inside it right now. Called right before vanilla checks
    /// Inventory.IsTeleportable, so changes made to material flags by other mods after our registration still count.
    /// The ItemData.m_shared object is shared with the cargo prefab, so this also fixes the tooltip.</summary>
    public static void RefreshTeleportable(ItemDrop.ItemData cargo)
    {
        string blocker;
        var piece = PrefabPieceOf(cargo);
        var ok = ContentsTeleportable(piece, out blocker);
        if (cargo.m_shared.m_teleportable != ok)
            ModLog.Verbose("Cargo '" + cargo.m_dropPrefab.name + "' teleportable " + cargo.m_shared.m_teleportable + " -> " + ok + (ok ? "" : " (contains '" + blocker + "')"));
        cargo.m_shared.m_teleportable = ok;
    }

    // ---------- helpers ----------
    public static bool IsCargo(ItemDrop.ItemData item)
    {
        if (RuleLog.IsOn()) ModLog.Every("CargoFactory.IsCargo", 2d, $"CargoFactory.IsCargo(item={item})");
        { var returnedAt175 = item != null && item.m_dropPrefab != null && CargoNames.IsCargo(item.m_dropPrefab.name); if (RuleLog.IsOn()) ModLog.Every("CargoFactory.IsCargo:175", 2d, $"CargoFactory.IsCargo: returns {returnedAt175} (line 175)"); return returnedAt175; }
    }

    /// <summary>Piece prefab name of a cargo item; null when the item is not a cargo (callers check IsCargo first).</summary>
    public static string PieceNameOf(ItemDrop.ItemData item)
    {
        if (RuleLog.IsOn()) ModLog.Every("CargoFactory.PieceNameOf", 2d, $"CargoFactory.PieceNameOf(item={item})");
        { var returnedAt182 = item != null && item.m_dropPrefab != null ? CargoNames.PieceOf(item.m_dropPrefab.name) : null; if (RuleLog.IsOn()) ModLog.Every("CargoFactory.PieceNameOf:182", 2d, $"CargoFactory.PieceNameOf: returns {returnedAt182} (line 182)"); return returnedAt182; }
    }

    public static GameObject GetCargoPrefab(Piece piece)
    {
        if (RuleLog.IsOn()) ModLog.Every("CargoFactory.GetCargoPrefab", 2d, $"CargoFactory.GetCargoPrefab(piece={piece})");
        if (piece == null || ObjectDB.instance == null) { if (RuleLog.IsOn()) ModLog.Every("CargoFactory.GetCargoPrefab:188", 2d, $"CargoFactory.GetCargoPrefab: returns null (line 188) - inputs (piece={piece})"); return null; }
        { var returnedAt189 = ObjectDB.instance.GetItemPrefab(PrefabPrefix + Utils.GetPrefabName(piece.gameObject)); if (RuleLog.IsOn()) ModLog.Every("CargoFactory.GetCargoPrefab:189", 2d, $"CargoFactory.GetCargoPrefab: returns {returnedAt189} (line 189)"); return returnedAt189; }
    }

    public static ItemDrop.ItemData FindCargoFor(Inventory inv, Piece piece)
    {
        if (RuleLog.IsOn()) ModLog.Every("CargoFactory.FindCargoFor", 2d, $"CargoFactory.FindCargoFor(inv={inv}, piece={piece})");
        if (inv == null || piece == null) { if (RuleLog.IsOn()) ModLog.Every("CargoFactory.FindCargoFor:195", 2d, $"CargoFactory.FindCargoFor: returns null (line 195) - inputs (inv={inv}, piece={piece})"); return null; }
        var want = PrefabPrefix + Utils.GetPrefabName(piece.gameObject);
        foreach (var it in inv.GetAllItems())
            if (it.m_dropPrefab != null && it.m_dropPrefab.name == want) { var returnedAt198 = it; if (RuleLog.IsOn()) ModLog.Every("CargoFactory.FindCargoFor:198", 2d, $"CargoFactory.FindCargoFor: returns {returnedAt198} (line 198)"); return returnedAt198; }
        { if (RuleLog.IsOn()) ModLog.Every("CargoFactory.FindCargoFor:199", 2d, $"CargoFactory.FindCargoFor: returns null (line 199) - inputs (inv={inv}, piece={piece})"); return null; }
    }

    private static readonly Dictionary<string, Piece> TablePieces = new();

    /// <summary>The piece a cargo stands for. Looked up in ZNetScene first; pieces that are not networked prefabs
    /// (the terrain tools raise_v2 / paved_road_v2, ...) are not there, so the hammer's piece tables are searched too.
    /// Without that fallback those cargos could never be unpacked.</summary>
    public static Piece PrefabPieceOf(ItemDrop.ItemData cargo)
    {
        if (RuleLog.IsOn()) ModLog.Every("CargoFactory.PrefabPieceOf", 2d, $"CargoFactory.PrefabPieceOf(cargo={cargo})");
        var name = PieceNameOf(cargo);
        if (name == null) { if (RuleLog.IsOn()) ModLog.Every("CargoFactory.PrefabPieceOf:211", 2d, $"CargoFactory.PrefabPieceOf: returns null (line 211) - inputs (cargo={cargo})"); return null; }
        var go = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(name) : null;
        var piece = go != null ? go.GetComponent<Piece>() : null;
        { var returnedAt214 = piece != null ? piece : PieceFromTables(name); if (RuleLog.IsOn()) ModLog.Every("CargoFactory.PrefabPieceOf:214", 2d, $"CargoFactory.PrefabPieceOf: returns {returnedAt214} (line 214)"); return returnedAt214; }
    }

    public static Piece PieceFromTables(string pieceName)
    {
        if (RuleLog.IsOn()) ModLog.Every("CargoFactory.PieceFromTables", 2d, $"CargoFactory.PieceFromTables(pieceName={pieceName})");
        Piece known;
        if (TablePieces.TryGetValue(pieceName, out known) && known != null) { var returnedAt221 = known; if (RuleLog.IsOn()) ModLog.Every("CargoFactory.PieceFromTables:221", 2d, $"CargoFactory.PieceFromTables: returns {returnedAt221} (line 221)"); return returnedAt221; }
        var db = ObjectDB.instance;
        if (db == null || db.m_items == null) { if (RuleLog.IsOn()) ModLog.Every("CargoFactory.PieceFromTables:223", 2d, $"CargoFactory.PieceFromTables: returns null (line 223) - inputs (pieceName={pieceName})"); return null; }
        foreach (var item in db.m_items)
        {
            var drop = item != null ? item.GetComponent<ItemDrop>() : null;
            var table = drop != null ? drop.m_itemData.m_shared.m_buildPieces : null;
            if (table == null || table.m_pieces == null) continue;
            foreach (var pg in table.m_pieces)
            {
                if (pg == null || Utils.GetPrefabName(pg) != pieceName) continue;
                var piece = pg.GetComponent<Piece>();
                if (piece != null) { TablePieces[pieceName] = piece; { var returnedAt233 = piece; if (RuleLog.IsOn()) ModLog.Every("CargoFactory.PieceFromTables:233", 2d, $"CargoFactory.PieceFromTables: returns {returnedAt233} (line 233)"); return returnedAt233; } }
            }
        }
        { if (RuleLog.IsOn()) ModLog.Every("CargoFactory.PieceFromTables:236", 2d, $"CargoFactory.PieceFromTables: returns null (line 236) - inputs (pieceName={pieceName})"); return null; }
    }
}
