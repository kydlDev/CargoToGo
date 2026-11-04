using System.Collections.Generic;
using CargoToGo.Features.Packing.Infrastructure;
using HarmonyLib;
using KydlCore.Common;
using UnityEngine;

namespace CargoToGo.Testing.TestHooks;

/// <summary>
/// KydlCore D-084: hooks for DrakesTestLab suites (CargoToGo-Mod/tests/TestLab/suites/CargoToGo). A hook is a static method with [TestHook("name")]; DrakesTestLab finds it by the
/// attribute's NAME (the mod does not reference DrakesTestLab). Hook names carry the mod id so two mods never clash.
/// </summary>
internal static class TestHooks
{
    /// <summary>Every test starts here (MIGRATION_DECISIONS C26): the player moves to the shared open arena spot.</summary>
    [TestHook("CargoToGo.arena")]
    static string Arena() { return TestArena.MovePlayer(); }

    /// <summary>Is the feature (packing, repack, key-hints, cargo-tab, gravestone) running in the FeatureRegistry?</summary>
    [TestHook("CargoToGo.featureRunning")]
    static bool FeatureRunning(string id) { return Bootstrap.ModInstaller.Current != null && Bootstrap.ModInstaller.Current.Features.IsRunning(id); }

    /// <summary>All Startup_* / World_* checks of InGameTests: "PASS: ..." or "FAIL: ...".</summary>
    [TestHook("CargoToGo.selftest")]
    static string SelfTest() { return TestLabSupport.RunInGameTests(typeof(InGameTests)); }

    /// <summary>How many cargo items the ObjectDB holds (one per build piece).</summary>
    [TestHook("CargoToGo.cargoItems")]
    static int CargoItems()
    {
        var db = ObjectDB.instance;
        if (db == null) return -1;
        int n = 0;
        foreach (var go in db.m_items) if (go != null && go.name.StartsWith(CargoFactory.PrefabPrefix)) n++;
        return n;
    }

    // ------------------------------------------------------------------ lab helpers: pack / unpack / rules / gravestone (tests/TestLab/suites/CargoToGo)

    static Piece PieceByName(string name)
    {
        var go = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(name) : null;
        var piece = go != null ? go.GetComponent<Piece>() : null;
        return piece != null ? piece : CargoFactory.PieceFromTables(name);
    }

    static ItemDrop.ItemData FirstCargo(Inventory inv, string pieceName)
    {
        if (inv == null) return null;
        foreach (var it in inv.GetAllItems())
            if (CargoFactory.IsCargo(it) && CargoFactory.PieceNameOf(it) == pieceName) return it;
        return null;
    }

    static int CargosIn(Inventory inv, string pieceName)
    {
        int n = 0;
        if (inv == null) return 0;
        foreach (var it in inv.GetAllItems()) if (CargoFactory.IsCargo(it) && (string.IsNullOrEmpty(pieceName) || CargoFactory.PieceNameOf(it) == pieceName)) n++;
        return n;
    }

    static Container NearestGrave()
    {
        var p = Player.m_localPlayer;
        Container best = null; float bestD = 1e9f;
        foreach (var stone in Object.FindObjectsByType<TombStone>(FindObjectsSortMode.InstanceID))
        {
            var c = stone.GetComponent<Container>();
            if (c == null) continue;
            float d = (stone.transform.position - p.transform.position).sqrMagnitude;
            if (d < bestD) { bestD = d; best = c; }
        }
        return best;
    }

    /// <summary>Puts exactly the materials of the piece into the player's inventory (the way a player would carry them). Expected outcome: "gave N stack(s)".</summary>
    [TestHook("CargoToGo.giveMaterials")]
    static string GiveMaterials(string piece)
    {
        var p = Player.m_localPlayer; var pc = PieceByName(piece);
        if (p == null || pc == null || pc.m_resources == null) return "no player / piece " + piece;
        int n = 0;
        foreach (var r in pc.m_resources)
        {
            if (r.m_resItem == null || r.m_amount <= 0) continue;
            var d = r.m_resItem.m_itemData.Clone();
            d.m_dropPrefab = r.m_resItem.gameObject; d.m_stack = r.m_amount; d.m_quality = 1;
            if (p.GetInventory().AddItem(d)) n++;
        }
        return "gave " + n + " stack(s)";
    }

    /// <summary>How many of an item (prefab name) the player's inventory holds.</summary>
    [TestHook("CargoToGo.itemCount")]
    static int ItemCount(string prefab)
    {
        var p = Player.m_localPlayer; var go = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(prefab) : null;
        if (p == null || go == null) return -1;
        return p.GetInventory().CountItems(go.GetComponent<ItemDrop>().m_itemData.m_shared.m_name);
    }

    /// <summary>Packs the piece into a cargo exactly like Ctrl+Alt + click does (Packer.TryPack). Expected outcome: true when a cargo was made.</summary>
    [TestHook("CargoToGo.pack")]
    static bool Pack(string piece) { var p = Player.m_localPlayer; var pc = PieceByName(piece); return p != null && pc != null && Packer.TryPack(p, pc); }

    /// <summary>Cargos of this piece (empty = any piece) in the player's inventory.</summary>
    [TestHook("CargoToGo.cargoCount")]
    static int CargoCount(string piece) { var p = Player.m_localPlayer; return p == null ? -1 : CargosIn(p.GetInventory(), piece); }

    /// <summary>Unpacks one cargo of the piece in the player's inventory (Ctrl+Alt + right click). Expected outcome: true.</summary>
    [TestHook("CargoToGo.unpack")]
    static bool Unpack(string piece) { var p = Player.m_localPlayer; var c = p != null ? FirstCargo(p.GetInventory(), piece) : null; return c != null && Packer.Unpack(p, c); }

    /// <summary>The rule "a cargo stays in your inventory": tries to add the cargo to a chest next to the player. Expected outcome: false (refused), the cargo is still in the inventory.</summary>
    [TestHook("CargoToGo.cargoIntoChest")]
    static bool CargoIntoChest(string piece)
    {
        var p = Player.m_localPlayer; var cargo = p != null ? FirstCargo(p.GetInventory(), piece) : null;
        if (cargo == null) return true;
        Container chest = null; float best = 1e9f;
        foreach (var c in Object.FindObjectsByType<Container>(FindObjectsSortMode.InstanceID))
        {
            if (c.GetComponent<TombStone>() != null) continue;
            float d = (c.transform.position - p.transform.position).sqrMagnitude;
            if (d < best) { best = d; chest = c; }
        }
        if (chest == null) return true;
        return chest.GetInventory().AddItem(cargo.Clone());
    }

    /// <summary>The death path of the player: Player.CreateTombStone (what OnDeath calls) with the current inventory. Expected outcome: "cargos in grave N, in inventory M".</summary>
    [TestHook("CargoToGo.makeGrave")]
    static string MakeGrave()
    {
        var p = Player.m_localPlayer;
        if (p == null) return "no player";
        AccessTools.Method(typeof(Player), "CreateTombStone").Invoke(p, null);
        var grave = NearestGrave();
        return "cargos in grave " + (grave != null ? CargosIn(grave.GetInventory(), null) : -1) + ", in inventory " + CargosIn(p.GetInventory(), null);
    }

    /// <summary>The grave container loads its items again (what happens when the zone reloads or the grave changes owner): Container.Load. Expected outcome: cargos in the grave afterwards.</summary>
    [TestHook("CargoToGo.reloadGrave")]
    static int ReloadGrave()
    {
        var grave = NearestGrave();
        if (grave == null) return -1;
        AccessTools.Method(typeof(Container), "Save").Invoke(grave, null);
        AccessTools.Method(typeof(Container), "Load").Invoke(grave, null);
        return CargosIn(grave.GetInventory(), null);
    }

    /// <summary>Cargos in the nearest grave.</summary>
    [TestHook("CargoToGo.graveCargos")]
    static int GraveCargos() { var g = NearestGrave(); return g != null ? CargosIn(g.GetInventory(), null) : -1; }

    /// <summary>The player tries to drop the cargo out of the grave onto the ground. Expected outcome: false (refused), the cargo stays in the grave.</summary>
    [TestHook("CargoToGo.dropFromGrave")]
    static bool DropFromGrave()
    {
        var p = Player.m_localPlayer; var g = NearestGrave();
        if (p == null || g == null) return true;
        var cargo = FirstCargo(g.GetInventory(), null);
        return cargo != null && p.DropItem(g.GetInventory(), cargo, 1);
    }

    /// <summary>"Take all": every cargo of the nearest grave moves into the player's inventory. Expected outcome: the cargos now in the player's inventory.</summary>
    [TestHook("CargoToGo.takeAllFromGrave")]
    static int TakeAllFromGrave()
    {
        var p = Player.m_localPlayer; var g = NearestGrave();
        if (p == null || g == null) return -1;
        var items = new List<ItemDrop.ItemData>(g.GetInventory().GetAllItems());
        foreach (var it in items) p.GetInventory().MoveItemToThis(g.GetInventory(), it);
        return CargosIn(p.GetInventory(), null);
    }

    /// <summary>Test cleanup: removes every grave near the player (and the cargos in them).</summary>
    [TestHook("CargoToGo.clearGraves")]
    static int ClearGraves()
    {
        var p = Player.m_localPlayer; int n = 0;
        foreach (var stone in Object.FindObjectsByType<TombStone>(FindObjectsSortMode.InstanceID))
        {
            if (p != null && (stone.transform.position - p.transform.position).sqrMagnitude > 50f * 50f) continue;
            var nv = stone.GetComponent<ZNetView>();
            if (nv != null && nv.IsValid()) { nv.ClaimOwnership(); nv.Destroy(); n++; }
        }
        return n;
    }
}
