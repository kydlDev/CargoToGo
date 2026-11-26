using System;
using System.Collections.Generic;
using CargoToGo.Configuration;
using CargoToGo.Features.Packing.Infrastructure;
using HarmonyLib;
using KydlCore.Common;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using CargoToGo.Core;

namespace CargoToGo.Features.CargoTab.Infrastructure;

// ====================================================================================================
// The 5th tab ("Cargos") of the hammer / build menu.
//
// How vanilla builds that menu (verified by reading assembly_valheim.dll):
//   BuildUi keeps a List<IPieceList> m_pieceLists - one entry per tab (Categories, Materials, Recent, Favorites).
//   Every tab button is a child of BuildUi.m_tabContainer and is also listed in BuildUi.m_tabButtons and
//   in TabHandler.m_tabs (same index everywhere). Clicking button i calls BuildUi.SelectPieceList(i), which
//   later calls IPieceList.GetAvailablePiecesWithTag(...) to fill the piece grid.
//   The grid is only rebuilt by vanilla when the number of *available pieces* changes, so a list whose
//   content depends on the inventory (like ours) has to trigger its own refresh.
//
// What we do:
//   1. CargoPieceList : IPieceList  -> pieces (of the current build tool) for which the player holds a cargo.
//   2. Postfix on BuildUi.Awake     -> clone the last tab button, add it as tab index 4, register the list.
//   3. Postfix on BuildUi.Update    -> while the Cargos tab is open, rebuild the grid whenever the set of
//                                      cargos in the player's inventory changes (cargo used / destroyed / packed).
// ====================================================================================================

/// <summary>Tab content: one button per piece the player currently holds a cargo for. No tag sidebar (like "Recent").</summary>
internal sealed class CargoPieceList : IPieceList
{
    public string DisplayName { get { return CargoTab.TabLabel; } }
    public bool ShowTags { get { return false; } }
    public bool CanCustomizeTags { get { return false; } }
    public int TagCount { get { return 0; } }
    public string GetTagDisplayName(int index) { return ""; }
    public int GetTagIdByIndex(int index) { return -1; }
    public int TagSeparatorIndex { get { return -1; } }
    public void UpdateAvailableTags(PieceTable pieceTable) { }

    public void GetAvailablePiecesWithTag(int tagId, PieceTable pieceTable, IList<Piece> resultOut)
    {
        try
        {
            var player = Player.m_localPlayer;
            var names = CargoTab.CargoPieceNames(player != null ? player.GetInventory() : null);
            CargoTab.LastSignature = CargoTab.Signature(names);   // so the poller only refreshes on real changes
            if (names.Count == 0 || pieceTable == null || pieceTable.m_pieces == null || pieceTable.m_availablePieces == null) { if (RuleLog.IsOn()) ModLog.Verbose($"CargoPieceList.GetAvailablePiecesWithTag: returns (line 51) - inputs (tagId={tagId}, pieceTable={pieceTable}, resultOut={resultOut})"); return; }

            // Walk the table in its own order so the tab looks like the "Show All" list of the hammer.
            foreach (var go in pieceTable.m_pieces)
            {
                if (go == null) continue;
                var piece = go.GetComponent<Piece>();
                if (piece == null || !pieceTable.m_availablePieces.Contains(piece)) continue;
                if (names.Contains(Utils.GetPrefabName(go))) resultOut.Add(piece);
            }
            ModLog.Verbose("Cargos tab: " + resultOut.Count + " piece(s) listed for " + names.Count + " distinct cargo type(s) in the inventory.");
        }
        catch (Exception e) { ModLog.Error("CargoPieceList.GetAvailablePiecesWithTag failed" + " | " + e); }
    }
}

internal static class CargoTab
{
    public const string TabLabel = "Cargos";

    /// <summary>Signature of the cargos seen the last time the list was built.</summary>
    public static string LastSignature = "";

    private static readonly System.Reflection.FieldInfo F_Lists = AccessTools.Field(typeof(BuildUi), "m_pieceLists");
    private static readonly System.Reflection.FieldInfo F_TabButtons = AccessTools.Field(typeof(BuildUi), "m_tabButtons");
    private static readonly System.Reflection.FieldInfo F_TabContainer = AccessTools.Field(typeof(BuildUi), "m_tabContainer");
    private static readonly System.Reflection.FieldInfo F_TabHandler = AccessTools.Field(typeof(BuildUi), "m_tabHandler");
    private static readonly System.Reflection.FieldInfo F_CurrentList = AccessTools.Field(typeof(BuildUi), "m_currentPieceList");
    private static readonly System.Reflection.MethodInfo M_SelectPieceList = AccessTools.Method(typeof(BuildUi), "SelectPieceList");
    private static readonly System.Reflection.MethodInfo M_UpdatePieceButtons = AccessTools.Method(typeof(BuildUi), "UpdatePieceButtons");

    /// <summary>Index of our tab in BuildUi.m_pieceLists, or -1 when it was not installed.</summary>
    private static int _index = -1;
    private static BuildUi _installedOn;
    private static float _nextPoll;

    // ------------------------------------------------------------------ what the player holds
    /// <summary>Distinct piece prefab names of every cargo in the inventory (first-seen order).</summary>
    public static List<string> CargoPieceNames(Inventory inv)
    {
        if (RuleLog.IsOn()) ModLog.Verbose($"CargoTab.CargoPieceNames(inv={inv})");
        var list = new List<string>();
        if (inv == null) { var returnedAt93 = list; if (RuleLog.IsOn()) ModLog.Verbose($"CargoTab.CargoPieceNames: returns {returnedAt93} (line 93)"); return returnedAt93; }
        foreach (var it in inv.GetAllItems())
        {
            if (!CargoFactory.IsCargo(it)) continue;
            var n = CargoFactory.PieceNameOf(it);
            if (!list.Contains(n)) list.Add(n);
        }
        { var returnedAt100 = list; if (RuleLog.IsOn()) ModLog.Verbose($"CargoTab.CargoPieceNames: returns {returnedAt100} (line 100)"); return returnedAt100; }
    }

    public static string Signature(List<string> names)
    {
        if (RuleLog.IsOn()) ModLog.Verbose($"CargoTab.Signature(names={names})");
        var copy = new List<string>(names);
        copy.Sort(StringComparer.Ordinal);
        { var returnedAt108 = string.Join("|", copy.ToArray()); if (RuleLog.IsOn()) ModLog.Verbose($"CargoTab.Signature: returns {returnedAt108} (line 108)"); return returnedAt108; }
    }

    // ------------------------------------------------------------------ install
    public static void Install(BuildUi ui)
    {
        _index = -1; _installedOn = null;
        if (!ModSettings.On || !ModSettings.ShowCargoTab.Value) { ModLog.Info("Cargos tab not added: EnableMod " + ModSettings.On + ", ShowCargoTab " + ModSettings.ShowCargoTab.Value); return; }
        if (F_Lists == null || F_TabButtons == null || F_TabContainer == null || F_TabHandler == null || M_SelectPieceList == null || M_UpdatePieceButtons == null)
        {
            ModLog.Error("Cargos tab: BuildUi has changed (found: lists " + (F_Lists != null) + ", buttons " + (F_TabButtons != null) + ", container " + (F_TabContainer != null) + ", handler " + (F_TabHandler != null) + ", SelectPieceList " + (M_SelectPieceList != null) + ", UpdatePieceButtons " + (M_UpdatePieceButtons != null) + "); the tab was not added. Game version incompatible?");
            return;
        }

        var lists = F_Lists.GetValue(ui) as List<IPieceList>;
        var buttons = F_TabButtons.GetValue(ui) as List<Button>;
        var container = F_TabContainer.GetValue(ui) as RectTransform;
        var handler = F_TabHandler.GetValue(ui) as TabHandler;
        if (lists == null || buttons == null || container == null || buttons.Count == 0)
        {
            ModLog.Error("Cargos tab: build menu structure not as expected (lists=" + (lists != null) + ", buttons=" + (buttons != null ? buttons.Count : -1) + ", container=" + (container != null) + "); the tab was not added.");
            return;
        }
        foreach (var l in lists) if (l is CargoPieceList) { ModLog.Verbose("Cargos tab already installed (" + lists.Count + " piece lists)."); return; }
        if (buttons.Count != lists.Count)
        {
            // Tab index == list index everywhere in vanilla. If another mod broke that, adding ours would select the wrong content.
            ModLog.Warn("Cargos tab: " + buttons.Count + " tab buttons but " + lists.Count + " piece lists; the tab was not added to keep indices aligned.");
            return;
        }

        var template = buttons[buttons.Count - 1];
        var go = UnityEngine.Object.Instantiate(template.gameObject, container);
        go.name = "CargoToGoTab";
        var btn = go.GetComponent<Button>();
        if (btn == null) { ModLog.Error("Cargos tab: cloned tab '" + go.name + "' has no Button; aborting."); UnityEngine.Object.Destroy(go); return; }

        // The clone must not re-localize itself back to "Favorites", so drop its Localize components first.
        foreach (var loc in go.GetComponentsInChildren<Localize>(true)) UnityEngine.Object.DestroyImmediate(loc);
        foreach (var t in go.GetComponentsInChildren<TMP_Text>(true)) t.text = TabLabel;
        foreach (var t in go.GetComponentsInChildren<Text>(true)) t.text = TabLabel;
        var selected = go.transform.Find("Selected");
        if (selected != null) selected.gameObject.SetActive(false);
        btn.interactable = true;
        btn.onClick.RemoveAllListeners();

        var index = lists.Count;
        lists.Add(new CargoPieceList());
        buttons.Add(btn);
        var idx = index;
        btn.onClick.AddListener(delegate { M_SelectPieceList.Invoke(ui, new object[] { idx, false }); });

        if (handler != null && handler.m_tabs != null)
        {
            var tab = new TabHandler.Tab();
            tab.m_button = btn;
            tab.m_default = false;
            tab.m_page = null;
            tab.m_onClick = null;
            handler.m_tabs.Add(tab);   // makes Q / E cycling and the "selected" look include the new tab
        }
        else ModLog.Warn("Cargos tab: no TabHandler (handler " + (handler != null) + ", tab " + index + "); the tab works with the mouse but Q/E cycling and highlight may not.");

        _index = index; _installedOn = ui;
        ReLayout(container);
        ModLog.Info("Cargos tab added to the build menu (tab index " + index + ", " + container.childCount + " tabs).");
    }

    // ------------------------------------------------------------------ layout (5 tabs now share the space of 4)
    private static void ReLayout(RectTransform container)
    {
        try
        {
            var tabs = new List<RectTransform>();
            foreach (Transform c in container) { var rt = c as RectTransform; if (rt != null && rt.gameObject.activeSelf) tabs.Add(rt); }
            var n = tabs.Count;
            if (n < 2) { if (RuleLog.IsOn()) ModLog.Every("CargoTab.ReLayout:184", 2d, $"CargoTab.ReLayout: returns (line 184) - inputs (container={container})"); return; }

            var glg = container.GetComponent<GridLayoutGroup>();
            var hlg = container.GetComponent<HorizontalLayoutGroup>();
            var anyLayout = container.GetComponent<LayoutGroup>();
            ModLog.Verbose("Tab container '" + container.name + "': rect=" + container.rect + " HLG=" + (hlg != null) + " Grid=" + (glg != null) + " otherLayout=" + (anyLayout != null && hlg == null && glg == null));

            if (glg != null)
            {
                var avail = container.rect.width - glg.padding.left - glg.padding.right - glg.spacing.x * (n - 1);
                if (avail > 0f) glg.cellSize = new Vector2(avail / n, glg.cellSize.y);
                if (glg.constraint == GridLayoutGroup.Constraint.FixedColumnCount) glg.constraintCount = n;
            }
            else if (hlg != null)
            {
                if (!hlg.childControlWidth)
                {
                    var avail = container.rect.width - hlg.padding.left - hlg.padding.right - hlg.spacing * (n - 1);
                    if (avail > 0f) foreach (var rt in tabs) rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, avail / n);
                }
                // with childControlWidth the layout group shares the width itself
            }
            else if (anyLayout == null)
            {
                // Hand-placed tabs: spread all of them evenly over the span the original ones covered.
                tabs.Sort(delegate (RectTransform a, RectTransform b) { { var returnedAt209 = LocalLeft(container, a).CompareTo(LocalLeft(container, b)); if (RuleLog.IsOn()) ModLog.Every("CargoTab.ReLayout:209", 2d, $"CargoTab.ReLayout: returns {returnedAt209} (line 209)"); return returnedAt209; } });
                // the clone was appended last; keep it last even if it sits on top of another tab
                var clone = tabs.Find(delegate (RectTransform r) { { var returnedAt211 = r.gameObject.name == "CargoToGoTab"; if (RuleLog.IsOn()) ModLog.Every("CargoTab.ReLayout:211", 2d, $"CargoTab.ReLayout: returns {returnedAt211} (line 211)"); return returnedAt211; } });
                if (clone != null) { tabs.Remove(clone); tabs.Add(clone); }

                var first = tabs[0];
                var last = tabs[n - 2];                       // last of the original tabs
                var left = LocalLeft(container, first);
                var right = LocalLeft(container, last) + last.rect.width * last.lossyScale.x / container.lossyScale.x;
                var gap = n > 2 ? Mathf.Max(0f, LocalLeft(container, tabs[1]) - (left + first.rect.width * first.lossyScale.x / container.lossyScale.x)) : 0f;
                var w = ((right - left) - gap * (n - 1)) / n;
                if (w <= 1f) { ModLog.Warn("Cargos tab: computed tab width " + w + " is too small; layout left unchanged."); return; }
                for (var i = 0; i < n; i++)
                    tabs[i].SetInsetAndSizeFromParentEdge(RectTransform.Edge.Left, left + i * (w + gap), w);
            }
            LayoutRebuilder.ForceRebuildLayoutImmediate(container);
        }
        catch (Exception e) { ModLog.Warn("Cargos tab: re-layout failed (the tab still exists): " + e.GetType().Name + ": " + e.Message); }
    }

    /// <summary>Left edge of a rect, measured in the container's local space from the container's left edge.</summary>
    private static float LocalLeft(RectTransform container, RectTransform rt)
    {
        if (RuleLog.IsOn()) ModLog.Every("CargoTab.LocalLeft", 2d, $"CargoTab.LocalLeft(container={container}, rt={rt})");
        var corners = new Vector3[4];
        rt.GetWorldCorners(corners);
        { var returnedAt235 = container.InverseTransformPoint(corners[0]).x - container.rect.xMin; if (RuleLog.IsOn()) ModLog.Every("CargoTab.LocalLeft:235", 2d, $"CargoTab.LocalLeft: returns {returnedAt235} (line 235)"); return returnedAt235; }
    }

    // ------------------------------------------------------------------ live refresh
    /// <summary>Called every frame from BuildUi.Update. Cheap: only polls the inventory ~5x per second while the Cargos tab is shown.</summary>
    public static void Tick(BuildUi ui)
    {
        if (_index < 0 || ui == null || _installedOn != ui) { if (RuleLog.IsOn()) ModLog.Verbose($"CargoTab.Tick: returns (line 242) - inputs (ui={ui})"); return; }
        if (Time.unscaledTime < _nextPoll) { if (RuleLog.IsOn()) ModLog.Verbose($"CargoTab.Tick: returns (line 243) - inputs (ui={ui})"); return; }
        _nextPoll = Time.unscaledTime + 0.2f;
        try
        {
            if (F_CurrentList == null || Convert.ToInt32(F_CurrentList.GetValue(ui)) != _index) { if (RuleLog.IsOn()) ModLog.Verbose($"CargoTab.Tick: returns (line 247) - inputs (ui={ui})"); return; }
            var player = Player.m_localPlayer;
            if (player == null) { if (RuleLog.IsOn()) ModLog.Verbose($"CargoTab.Tick: returns (line 249) - inputs (ui={ui})"); return; }
            var sig = Signature(CargoPieceNames(player.GetInventory()));
            if (sig == LastSignature) { if (RuleLog.IsOn()) ModLog.Verbose($"CargoTab.Tick: returns (line 251) - inputs (ui={ui})"); return; }
            ModLog.Verbose("Cargos tab: inventory cargos changed ('" + LastSignature + "' -> '" + sig + "'), rebuilding the grid.");
            LastSignature = sig;
            M_UpdatePieceButtons.Invoke(ui, null);   // rebuilds the grid via CargoPieceList (which also stores the new signature)
        }
        catch (Exception e) { ModLog.Error("Cargos tab refresh failed" + " | " + e); }
    }
}
