using Kydl.Common;
using System;
using HarmonyLib;
using UnityEngine;

namespace CargoToGo
{
    // MIGRATION_DECISIONS A5: every Harmony patch lives in src/Integrations/Harmony. Registration: the cargo prefabs exist in every ObjectDB / ZNetScene (Priority.Last, after the other mods add their pieces).
    // ---------------------------------------------------------------- registration
    [HarmonyPatch(typeof(ObjectDB), "Awake"), HarmonyPriority(Priority.Last)]
    internal static class ObjectDB_Awake { private static void Postfix(ObjectDB __instance) { try { CargoFactory.Ensure(__instance); } catch (Exception e) { ModLog.Error("ObjectDB.Awake hook failed" + " | " + e); } } }

    [HarmonyPatch(typeof(ObjectDB), nameof(ObjectDB.CopyOtherDB)), HarmonyPriority(Priority.Last)]
    internal static class ObjectDB_Copy { private static void Postfix(ObjectDB __instance) { try { CargoFactory.Ensure(__instance); } catch (Exception e) { ModLog.Error("ObjectDB.CopyOtherDB hook failed" + " | " + e); } } }

    // Catches pieces that other mods add to piece tables after ObjectDB init (not saved inventories though).
    [HarmonyPatch(typeof(ZNetScene), "Awake"), HarmonyPriority(Priority.Last)]
    internal static class ZNetScene_Awake { private static void Postfix() { try { CargoFactory.Ensure(ObjectDB.instance); } catch (Exception e) { ModLog.Error("ZNetScene.Awake hook failed" + " | " + e); } } }
}
