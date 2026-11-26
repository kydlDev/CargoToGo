using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using CargoToGo.Configuration;
using CargoToGo.Features.Packing.Domain;
using HarmonyLib;
using KydlCore.Common;
using UnityEngine;
using CargoToGo.Core;

namespace CargoToGo.Features.Packing.Infrastructure;

internal static class Packer
{
    public const string BuiltFromCargoKey = "CC_FromCargo";

    public static bool Alt { get { return ZInput.GetKey(KeyCode.LeftAlt, false) || ZInput.GetKey(KeyCode.RightAlt, false); } }
    public static bool Ctrl { get { return ZInput.GetKey(KeyCode.LeftControl, false) || ZInput.GetKey(KeyCode.RightControl, false); } }
    /// <summary>Ctrl + Alt: the "pack a cargo" modifier (used together with left click).</summary>
    public static bool PackChord { get { return Alt && Ctrl; } }

    private static readonly MethodInfo CheckAccess = AccessTools.Method(typeof(Container), "CheckAccess");
    private static int _cacheFrame = -1;
    private static List<Container> _cache = new();
    private static float _lastPack;

    public static void Say(Player p, string text)
    {
        if (RuleLog.IsOn()) ModLog.Every("Packer.Say", 2d, $"Packer.Say(p={p}, text={text})");
        p.Message(MessageHud.MessageType.Center, text);
    }

    public static string PName(Player p) { return p != null ? p.GetPlayerName() : "?"; }
    public static string PrefabOf(ItemDrop r) { return Utils.GetPrefabName(r.gameObject); }

    /// <summary>Chests in range that the player may open and nobody is currently using.</summary>
    public static List<Container> NearbyContainers(Player p)
    {
        if (RuleLog.IsOn()) ModLog.Every("Packer.NearbyContainers", 2d, $"Packer.NearbyContainers(p={p})");
        if (_cacheFrame == Time.frameCount) { var returnedAt41 = _cache; if (RuleLog.IsOn()) ModLog.Every("Packer.NearbyContainers:41", 2d, $"Packer.NearbyContainers: returns {returnedAt41} (line 41)"); return returnedAt41; }
        _cacheFrame = Time.frameCount;
        _cache = new List<Container>();
        if (!ModSettings.PullFromContainers.Value) { var returnedAt44 = _cache; if (RuleLog.IsOn()) ModLog.Every("Packer.NearbyContainers:44", 2d, $"Packer.NearbyContainers: returns {returnedAt44} (line 44)"); return returnedAt44; }
        var range = ModSettings.ContainerRange.Value;
        if (range <= 0f) { var returnedAt46 = _cache; if (RuleLog.IsOn()) ModLog.Every("Packer.NearbyContainers:46", 2d, $"Packer.NearbyContainers: returns {returnedAt46} (line 46)"); return returnedAt46; } // 0 = CargoToGo's own radius search is off (AzuCraftyBoxes may still supply chests)
        var seen = new HashSet<Container>();
        foreach (var col in Physics.OverlapSphere(p.transform.position, range))
        {
            var c = col.GetComponentInParent<Container>();
            if (c == null || !seen.Add(c)) continue;
            if (c.GetComponent<TombStone>() != null) continue;
            var nv = c.GetComponent<ZNetView>();
            if (nv == null || !nv.IsValid() || c.GetInventory() == null) continue;
            if (c.IsInUse()) continue;
            if (CheckAccess != null && !(bool)CheckAccess.Invoke(c, new object[] { p.GetPlayerID() })) continue;
            _cache.Add(c);
        }
        { var returnedAt59 = _cache; if (RuleLog.IsOn()) ModLog.Every("Packer.NearbyContainers:59", 2d, $"Packer.NearbyContainers: returns {returnedAt59} (line 59)"); return returnedAt59; }
    }

    private static int _sourcesFrame = -1;
    private static List<Source> _sourcesCache = new();

    /// <summary>Everything a cargo may take materials from, in the order they are used:
    /// chests (CargoToGo's own radius first, then whatever AzuCraftyBoxes reaches) and the player's inventory.
    /// Cached per frame because CanPack runs every frame while the pack chord is held.</summary>
    private static List<Source> Sources(Player p)
    {
        if (RuleLog.IsOn()) ModLog.Every("Packer.Sources", 2d, $"Packer.Sources(p={p})");
        if (_sourcesFrame == Time.frameCount) { var returnedAt71 = _sourcesCache; if (RuleLog.IsOn()) ModLog.Every("Packer.Sources:71", 2d, $"Packer.Sources: returns {returnedAt71} (line 71)"); return returnedAt71; }
        _sourcesFrame = Time.frameCount;

        var chests = new List<Source>();
        var covered = new HashSet<Inventory>();
        foreach (var c in NearbyContainers(p))
        {
            var inv = c.GetInventory();
            covered.Add(inv);
            chests.Add(new InventorySource(inv, c));
        }
        if (ModSettings.PullFromContainers.Value && AzuCompat.Active)
            chests.AddRange(AzuBridge.Collect(p, covered));

        var list = new List<Source>();
        var mine = new InventorySource(p.GetInventory(), null);
        if (!ModSettings.ContainersFirst.Value) list.Add(mine);
        list.AddRange(chests);
        if (ModSettings.ContainersFirst.Value) list.Add(mine);
        _sourcesCache = list;
        { var returnedAt91 = list; if (RuleLog.IsOn()) ModLog.Every("Packer.Sources:91", 2d, $"Packer.Sources: returns {returnedAt91} (line 91)"); return returnedAt91; }
    }

    // ---------- cost rules (mirrors vanilla Player.UpdatePlacement / Piece.DropResources) ----------
    /// <summary>The piece's free-build WORLD key is set (NoBuildCost, or NoCraftCost for crafting-station type pieces).
    /// Vanilla then needs no materials, consumes none, and Piece.DropResources refunds none.</summary>
    public static bool FreeKeyActive(Piece piece)
    {
        if (RuleLog.IsOn()) ModLog.Every("Packer.FreeKeyActive", 2d, $"Packer.FreeKeyActive(piece={piece})");
        var zs = ZoneSystem.instance;
        { var returnedAt101 = zs != null && piece != null && zs.GetGlobalKey(piece.FreeBuildKey()); if (RuleLog.IsOn()) ModLog.Every("Packer.FreeKeyActive:101", 2d, $"Packer.FreeKeyActive: returns {returnedAt101} (line 101)"); return returnedAt101; }
    }

    /// <summary>Vanilla lets you place a piece when the nocost dev flag is on OR the free-build world key is set.</summary>
    public static bool BuildIsFree(Player p, Piece piece)
    {
        if (RuleLog.IsOn()) ModLog.Every("Packer.BuildIsFree", 2d, $"Packer.BuildIsFree(p={p}, piece={piece})");
        { var returnedAt108 = (p != null && p.NoCostCheat()) || FreeKeyActive(piece); if (RuleLog.IsOn()) ModLog.Every("Packer.BuildIsFree:108", 2d, $"Packer.BuildIsFree: returns {returnedAt108} (line 108)"); return returnedAt108; }
    }

    public static bool CanPack(Player p, Piece piece)
    {
        if (RuleLog.IsOn()) ModLog.Every("Packer.CanPack", 2d, $"Packer.CanPack(p={p}, piece={piece})");
        if (piece != null && piece.m_resources != null && piece.m_resources.Length > 0 && BuildIsFree(p, piece)) { if (RuleLog.IsOn()) ModLog.Every("Packer.CanPack:114", 2d, $"Packer.CanPack: returns true (line 114) - inputs (p={p}, piece={piece})"); return true; }
        string ignored;
        { var returnedAt116 = CheckResources(p, piece, Sources(p), out ignored); if (RuleLog.IsOn()) ModLog.Every("Packer.CanPack:116", 2d, $"Packer.CanPack: returns {returnedAt116} (line 116)"); return returnedAt116; }
    }

    private static int[] CountsOf(List<Source> sources, string name, string prefab)
    {
        if (RuleLog.IsOn()) ModLog.Every("Packer.CountsOf", 2d, $"Packer.CountsOf(sources={sources}, name={name}, prefab={prefab})");
        var counts = new int[sources.Count];
        for (var i = 0; i < counts.Length; i++) counts[i] = sources[i].Count(name, prefab);
        { var returnedAt124 = counts; if (RuleLog.IsOn()) ModLog.Every("Packer.CountsOf:124", 2d, $"Packer.CountsOf: returns {returnedAt124} (line 124)"); return returnedAt124; }
    }

    private static bool CheckResources(Player p, Piece piece, List<Source> sources, out string missing)
    {
        if (RuleLog.IsOn()) ModLog.Every("Packer.CheckResources", 2d, $"Packer.CheckResources(p={p}, piece={piece}, sources={sources})");
        missing = null;
        if (piece == null || piece.m_resources == null || piece.m_resources.Length == 0) { missing = "nothing to pack"; { if (RuleLog.IsOn()) ModLog.Every("Packer.CheckResources:131", 2d, $"Packer.CheckResources: returns false (line 131) - inputs (p={p}, piece={piece}, sources={sources})"); return false; } }
        foreach (var r in piece.m_resources)
        {
            if (r.m_resItem == null || r.m_amount <= 0) continue;
            var name = r.m_resItem.m_itemData.m_shared.m_name;
            var prefab = PrefabOf(r.m_resItem);
            if (!PackPlan.Covers(CountsOf(sources, name, prefab), r.m_amount)) { missing = name; { if (RuleLog.IsOn()) ModLog.Every("Packer.CheckResources:137", 2d, $"Packer.CheckResources: returns false (line 137) - inputs (p={p}, piece={piece}, sources={sources})"); return false; } }
        }
        { if (RuleLog.IsOn()) ModLog.Every("Packer.CheckResources:139", 2d, $"Packer.CheckResources: returns true (line 139) - inputs (p={p}, piece={piece}, sources={sources})"); return true; }
    }

    /// <summary>Pulls the piece's materials from chests/inventory into a new cargo in the player's inventory.</summary>
    public static bool TryPack(Player p, Piece piece)
    {
        try
        {
            if (p == null || piece == null) { if (RuleLog.IsOn()) ModLog.Every("Packer.TryPack:147", 2d, $"Packer.TryPack: returns false (line 147) - inputs (p={p}, piece={piece})"); return false; }
            if (Time.time - _lastPack < 0.35f) { if (RuleLog.IsOn()) ModLog.Every("Packer.TryPack:148", 2d, $"Packer.TryPack: returns false (line 148) - inputs (p={p}, piece={piece})"); return false; } // held-button repeat guard
            _lastPack = Time.time;
            var pieceName = Utils.GetPrefabName(piece.gameObject);

            var prefab = CargoFactory.GetCargoPrefab(piece);
            if (prefab == null)
            {
                Say(p, Loc.T("cg_cant_pack"));
                ModLog.Warn("Pack refused for '" + pieceName + "' by " + PName(p) + ": no cargo prefab registered for this piece.");
                return false;
            }

            var inv = p.GetInventory();
            if (!inv.HaveEmptySlot())
            {
                Say(p, Loc.T("cg_no_slot"));
                ModLog.Info("Pack refused for '" + pieceName + "' by " + PName(p) + ": inventory has no free slot.");
                return false;
            }

            var sources = Sources(p);
            // Same rules vanilla uses when placing: nocost flag or free-build world key = no materials needed.
            // The world key also means nothing is consumed; with the nocost flag vanilla still takes whatever you do have.
            var keyFree = FreeKeyActive(piece);
            var free = keyFree || p.NoCostCheat();
            string missing = null;
            if (!free && !CheckResources(p, piece, sources, out missing))
            {
                Say(p, Loc.T("cg_missing") + " " + Localization.instance.Localize(missing));
                ModLog.Info("Pack refused for '" + pieceName + "' by " + PName(p) + ": missing material " + missing + ".");
                return false;
            }

            var cargo = prefab.GetComponent<ItemDrop>().m_itemData.Clone();
            cargo.m_dropPrefab = prefab;
            cargo.m_stack = 1;
            cargo.m_quality = 1;
            if (!inv.AddItem(cargo))
            {
                Say(p, Loc.T("cg_no_slot"));
                ModLog.Warn("Pack failed for '" + pieceName + "' by " + PName(p) + ": AddItem(cargo) returned false although a free slot was reported.");
                return false;
            }

            var report = new StringBuilder();
            var taken = new List<KeyValuePair<ItemDrop, int>>();   // what really left the sources, for a rollback
            foreach (var r in piece.m_resources)
            {
                if (r.m_resItem == null || r.m_amount <= 0) continue;
                var name = r.m_resItem.m_itemData.m_shared.m_name;
                if (keyFree)
                {
                    report.Append(PrefabOf(r.m_resItem)).Append(" x").Append(r.m_amount).Append(" (free-build world key, nothing consumed); ");
                    continue;
                }
                var itemPrefab = PrefabOf(r.m_resItem);
                var plan = PackPlan.Allocate(CountsOf(sources, name, itemPrefab), r.m_amount);
                var tookEach = new int[sources.Count];
                int fromChests = 0, fromAzu = 0, fromInv = 0;
                for (var i = 0; i < sources.Count; i++)
                {
                    if (plan[i] <= 0) continue;
                    var took = sources[i].Take(name, itemPrefab, plan[i]);
                    tookEach[i] = took;
                    if (took <= 0) continue;
                    if (sources[i].Kind == SourceKind.Chest) fromChests += took;
                    else if (sources[i].Kind == SourceKind.Azu) fromAzu += took;
                    else fromInv += took;
                }
                taken.Add(new KeyValuePair<ItemDrop, int>(r.m_resItem, fromChests + fromAzu + fromInv));
                var left = PackPlan.Shortfall(r.m_amount, tookEach);
                if (left > 0 && !free)
                {
                    // Someone emptied a chest between the check and the withdrawal (multiplayer): no cargo from thin air.
                    ModLog.Error("Pack of '" + pieceName + "': " + left + " of " + itemPrefab + " could not be removed after the availability check passed; pack rolled back.");
                    inv.RemoveItem(cargo);
                    GiveBack(p, taken);
                    Say(p, Loc.T("cg_missing") + " " + Localization.instance.Localize(name));
                    { if (RuleLog.IsOn()) ModLog.Every("Packer.TryPack:226", 2d, $"Packer.TryPack: returns false (line 226) - inputs (p={p}, piece={piece})"); return false; }
                }
                report.Append(PrefabOf(r.m_resItem)).Append(" x").Append(r.m_amount)
                      .Append(" (chests ").Append(fromChests).Append(", azu ").Append(fromAzu).Append(", inventory ").Append(fromInv)
                      .Append(left > 0 ? ", free (nocost) " + left : "").Append("); ");
            }

            if (ModSettings.PackEffects.Value) PlayPackEffects(p, piece);
            Say(p, Loc.T("cg_packed") + " " + Localization.instance.Localize(piece.m_name));
            ModLog.Info("Cargo created for '" + pieceName + "' by " + PName(p) + " -> " + report.ToString().TrimEnd());
            return true;
        }
        catch (Exception e)
        {
            ModLog.Error("TryPack threw for player " + PName(p) + " | " + e);
            return false;
        }
    }

    /// <summary>Build dust + build sound (the piece's place effect) and the piece's own break effect,
    /// at the placement ghost if there is one, otherwise just in front of the player.</summary>
    private static void PlayPackEffects(Player p, Piece piece)
    {
        try
        {
            var pos = p.transform.position + p.transform.forward * 2f;
            var rot = p.transform.rotation;
            var ghost = Traverse.Create(p).Field("m_placementGhost").GetValue<GameObject>();
            if (ghost != null && ghost.activeInHierarchy) { pos = ghost.transform.position; rot = ghost.transform.rotation; }

            var n = 0;
            if (piece.m_placeEffect != null) { piece.m_placeEffect.Create(pos, rot, null, 1f, -1, default(ZDOID)); n++; }
            var wnt = piece.GetComponent<WearNTear>();
            if (wnt != null && wnt.m_destroyedEffect != null) { wnt.m_destroyedEffect.Create(pos, rot, null, 1f, -1, default(ZDOID)); n++; }
            ModLog.Verbose("Played " + n + " pack effect lists at " + pos);
        }
        catch (Exception e)
        {
            ModLog.Warn("Pack effects failed (cargo was still created): " + e.GetType().Name + ": " + e.Message);
        }
    }

    /// <summary>Destroying a cargo: the cargo is removed and its materials go back into the inventory
    /// (whatever does not fit is dropped at the player's feet).</summary>
    public static bool Unpack(Player p, ItemDrop.ItemData cargo)
    {
        try
        {
            var piece = CargoFactory.PrefabPieceOf(cargo);
            var pieceName = CargoFactory.PieceNameOf(cargo);
            if (piece == null || piece.m_resources == null)
            {
                Say(p, Loc.T("cg_unread"));
                ModLog.Error("Unpack aborted by " + PName(p) + ": piece prefab '" + pieceName + "' not found, cargo kept.");
                return false;
            }
            var inv = p.GetInventory();
            if (!inv.RemoveItem(cargo))
            {
                ModLog.Warn("Unpack aborted by " + PName(p) + ": cargo for '" + pieceName + "' could not be removed from the inventory.");
                return false;
            }

            if (FreeKeyActive(piece))
            {
                // Vanilla Piece.DropResources returns nothing under the free-build world key, so a cargo can't be turned into real materials here either.
                Say(p, Loc.T("cg_unpacked_free"));
                ModLog.Info("Cargo destroyed for '" + pieceName + "' by " + PName(p) + " -> no materials returned (free-build world key is active).");
                return true;
            }

            var report = new StringBuilder();
            var anyDropped = false;
            foreach (var r in piece.m_resources)
            {
                if (r.m_resItem == null || r.m_amount <= 0) continue;
                int returned, dropped;
                GiveBack(p, r.m_resItem, r.m_amount, out returned, out dropped);
                if (dropped > 0) anyDropped = true;
                report.Append(PrefabOf(r.m_resItem)).Append(" x").Append(r.m_amount)
                      .Append(" (to inventory ").Append(returned).Append(", dropped ").Append(dropped).Append("); ");
            }
            Say(p, Loc.T(anyDropped ? "cg_unpacked_drop" : "cg_unpacked"));
            ModLog.Info("Cargo destroyed for '" + pieceName + "' by " + PName(p) + " -> materials returned: " + report.ToString().TrimEnd());
            return true;
        }
        catch (Exception e)
        {
            ModLog.Error("Unpack threw for player " + PName(p) + " | " + e);
            return false;
        }
    }

    private static void GiveBack(Player p, List<KeyValuePair<ItemDrop, int>> items)
    {
        if (RuleLog.IsOn()) ModLog.Every("Packer.GiveBack", 2d, $"Packer.GiveBack(p={p}, items={items})");
        foreach (var kv in items)
        {
            if (kv.Value <= 0) continue;
            int returned, dropped;
            GiveBack(p, kv.Key, kv.Value, out returned, out dropped);
        }
    }

    /// <summary>Puts <paramref name="amount"/> of a material into the inventory in stacks; what does not fit is dropped at the player's feet.</summary>
    private static void GiveBack(Player p, ItemDrop res, int amount, out int returned, out int dropped)
    {
        if (RuleLog.IsOn()) ModLog.Every("Packer.GiveBack", 2d, $"Packer.GiveBack(p={p}, res={res}, amount={amount})");
        var inv = p.GetInventory();
        var dropPos = p.transform.position + p.transform.forward * 1.5f + Vector3.up;
        var maxStack = Mathf.Max(1, res.m_itemData.m_shared.m_maxStackSize);
        int left = amount;
        returned = 0; dropped = 0;
        while (left > 0)
        {
            var n = Mathf.Min(left, maxStack);
            var d = res.m_itemData.Clone();
            d.m_dropPrefab = res.gameObject;
            d.m_stack = n;
            d.m_quality = 1;
            d.m_worldLevel = Game.m_worldLevel;
            if (inv.CanAddItem(d, n) && inv.AddItem(d)) returned += n;
            else { ItemDrop.DropItem(d, n, dropPos, Quaternion.identity); dropped += n; }
            left -= n;
        }
    }

    public static bool IsBuiltFromCargo(Piece piece)
    {
        if (RuleLog.IsOn()) ModLog.Every("Packer.IsBuiltFromCargo", 2d, $"Packer.IsBuiltFromCargo(piece={piece})");
        var nv = piece != null ? piece.GetComponent<ZNetView>() : null;
        { var returnedAt357 = nv != null && nv.IsValid() && nv.GetZDO().GetBool(BuiltFromCargoKey, false); if (RuleLog.IsOn()) ModLog.Every("Packer.IsBuiltFromCargo:357", 2d, $"Packer.IsBuiltFromCargo: returns {returnedAt357} (line 357)"); return returnedAt357; }
    }
}
