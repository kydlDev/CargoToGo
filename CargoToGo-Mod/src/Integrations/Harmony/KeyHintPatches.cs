using Kydl.Common;
using System;
using HarmonyLib;
using UnityEngine;

namespace CargoToGo
{
    // MIGRATION_DECISIONS A5: every Harmony patch lives in src/Integrations/Harmony. Build-mode key hint bar (feature KeyHints: [KeyHints] Enable).
    // ---------------------------------------------------------------- build-mode key hint bar
    [HarmonyPatch(typeof(KeyHints), "UpdateHints")]
    internal static class KeyHints_UpdateHints
    {
        private static void Postfix(KeyHints __instance) { KeyHintUi.Tick(__instance); }
    }
}
