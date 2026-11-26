using System;
using System.Collections.Generic;
using CargoToGo.Configuration;
using KydlCore.Common;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using CargoToGo.Core;

namespace CargoToGo.Features.KeyHints.Infrastructure;

/// <summary>Adds "Pack cargo" / "Repack" hints to the build-mode key hint bar.
/// The hints are built from scratch (own layout groups) but copy font, colour and key-chip sprite
/// from an existing hint of the vanilla bar, and sit in a row directly above that bar.</summary>
internal static class KeyHintUi
{
    private static global::KeyHints _builtFor;
    private static GameObject _row, _packHint, _repackHint;
    private static TMP_Text _packKey, _repackKey;
    private static bool _disabled;
    private static int _lastRefresh;

    public static void Tick(global::KeyHints kh)
    {
        if (_disabled || kh == null) { if (RuleLog.IsOn()) ModLog.Every("KeyHintUi.Tick:25", 2d, $"KeyHintUi.Tick: no hints (line 25) - disabled {_disabled}, kh {kh != null}"); return; }
        try
        {
            var bh = kh.m_buildHints;
            if (bh == null) { if (RuleLog.IsOn()) ModLog.Every("KeyHintUi.Tick:29", 2d, $"KeyHintUi.Tick: no hints (line 29) - disabled {_disabled}, kh {kh != null}"); return; }

            var want = ModSettings.On && ModSettings.ShowKeyHints.Value && bh.activeInHierarchy && !ZInput.IsGamepadActive();
            if (_row == null || _builtFor != kh)
            {
                if (!want) { if (RuleLog.IsOn()) ModLog.Every("KeyHintUi.Tick:34", 2d, $"KeyHintUi.Tick: no hints (line 34) - disabled {_disabled}, kh {kh != null}"); return; }
                if (_row != null) UnityEngine.Object.Destroy(_row);
                if (!Build(kh)) { _disabled = true; { if (RuleLog.IsOn()) ModLog.Every("KeyHintUi.Tick:36", 2d, $"KeyHintUi.Tick: no hints (line 36) - disabled {_disabled}, kh {kh != null}"); return; } }
            }
            if (_row.activeSelf != want) _row.SetActive(want);
            if (!want) { if (RuleLog.IsOn()) ModLog.Every("KeyHintUi.Tick:39", 2d, $"KeyHintUi.Tick: no hints (line 39) - disabled {_disabled}, kh {kh != null}"); return; }

            _packHint.SetActive(ModSettings.PackInWorld.Value);
            Position(bh);
            if (Time.frameCount - _lastRefresh > 60) { _lastRefresh = Time.frameCount; RefreshKeys(); }
            if (RuleLog.IsOn()) ModLog.Every("KeyHintUi.Tick", 5d, $"KeyHintUi.Tick: showing the key hints (pack hint {ModSettings.PackInWorld.Value}, last key refresh frame {_lastRefresh})");
        }
        catch (Exception e)
        {
            _disabled = true;
            ModLog.Error("Key hint bar extension failed and was disabled for this session" + " | " + e);
        }
    }

    // ------------------------------------------------------------------ build
    private static bool Build(global::KeyHints kh)
    {
        var bh = kh.m_buildHints;
        TMP_Text label = null, key = null;
        Transform tpl = FindHintRoot(bh.transform);
        if (tpl != null)
        {
            var t = new List<TMP_Text>(tpl.GetComponentsInChildren<TMP_Text>(false));
            if (t.Count != 2) t = new List<TMP_Text>(tpl.GetComponentsInChildren<TMP_Text>(true));
            if (t.Count >= 2)
            {
                label = t[0]; key = t[1];
                // use screen position if it tells us which one is left of the other
                if (t[0].transform.position.x > t[1].transform.position.x + 1f) { label = t[1]; key = t[0]; }
            }
        }
        if (label == null)
        {
            var any = bh.GetComponentsInChildren<TMP_Text>(true);
            if (any.Length == 0) { ModLog.Warn("Key hint bar: no TextMeshPro text found under '" + bh.name + "'; hints not added."); return false; }
            label = key = any[0];
        }

        // Vanilla texts may auto-size, so TMP's fontSize property can be wrong; read the size actually rendered.
        float labelSize = EffectiveSize(label), keySize = EffectiveSize(key);
        if (labelSize < keySize * 0.6f) labelSize = keySize;   // implausibly small label -> use the key text size
        if (keySize < labelSize * 0.6f) keySize = labelSize;
        ModLog.Verbose("Key hint font sizes: label " + labelSize + " (property " + label.fontSize + ", autosize " + label.enableAutoSizing + "), key " + keySize + " (property " + key.fontSize + ", autosize " + key.enableAutoSizing + ")");

        Image chipImg = null;
        for (var t = key.transform; t != null && t != bh.transform; t = t.parent)
        {
            var im = t.GetComponent<Image>();
            if (im != null) { chipImg = im; break; }
        }

        if (ModLog.DebugOn) DumpHierarchy(bh.transform, tpl, label, key, chipImg);

        var canvas = bh.GetComponentInParent<Canvas>();
        var parent = canvas != null ? canvas.transform : bh.transform.parent;

        _row = new GameObject("CargoToGoHints", typeof(RectTransform));
        _row.layer = bh.layer;
        var rt = (RectTransform)_row.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0f);
        var hlg = _row.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 45f;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childControlWidth = hlg.childControlHeight = true;
        hlg.childForceExpandWidth = hlg.childForceExpandHeight = false;
        var fit = _row.AddComponent<ContentSizeFitter>();
        fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var style = new HintStyle(label, key, labelSize, keySize, chipImg);
        _packHint = AddHint("Pack", style, "Pack cargo", PackKeys(), out _packKey);
        _repackHint = AddHint("Repack", style, "Repack", RepackKeys(), out _repackKey);
        _builtFor = kh;
        ModLog.Info("Key hint bar: added Pack/Repack hints (font " + labelSize + "/" + keySize + ")" + (tpl != null ? " (style copied from '" + tpl.name + "')." : " (no template hint found, used first text style)."));
        return true;
    }

    private static Transform FindHintRoot(Transform root)
    {
        if (RuleLog.IsOn()) ModLog.Verbose($"KeyHintUi.FindHintRoot(root={root})");
        // Breadth-first walk (List + index instead of Queue<T>, which fails to resolve against the game reference set).
        var q = new List<Transform>();
        foreach (Transform c in root) q.Add(c);
        for (var i = 0; i < q.Count; i++)
        {
            var t = q[i];
            if (!t.gameObject.activeInHierarchy) continue;
            if (t.GetComponentsInChildren<TMP_Text>(false).Length == 2) { var returnedAt127 = t; if (RuleLog.IsOn()) ModLog.Verbose($"KeyHintUi.FindHintRoot: returns {returnedAt127} (line 127)"); return returnedAt127; }
            foreach (Transform c in t) q.Add(c);
        }
        { if (RuleLog.IsOn()) ModLog.Verbose($"KeyHintUi.FindHintRoot: returns null (line 130) - inputs (root={root})"); return null; }
    }

    /// <summary>How a hint looks: the text styles copied from the game's own hints, their sizes and the chip background.</summary>
    private readonly struct HintStyle
    {
        internal HintStyle(TMP_Text labelStyle, TMP_Text keyStyle, float labelSize, float keySize, Image chipImg)
        {
            if (RuleLog.IsOn()) ModLog.Verbose($"HintStyle.HintStyle(labelStyle={labelStyle}, keyStyle={keyStyle}, labelSize={labelSize}, keySize={keySize}, chipImg={chipImg})");
            LabelStyle = labelStyle; KeyStyle = keyStyle; LabelSize = labelSize; KeySize = keySize; ChipImg = chipImg;
        }
        internal TMP_Text LabelStyle { get; }
        internal TMP_Text KeyStyle { get; }
        internal float LabelSize { get; }
        internal float KeySize { get; }
        internal Image ChipImg { get; }
    }

    private static GameObject AddHint(string name, HintStyle style, string labelText, string keyText, out TMP_Text keyOut)
    {
        if (RuleLog.IsOn()) ModLog.Verbose($"HintStyle.AddHint(name={name}, style={style}, labelText={labelText}, keyText={keyText})");
        var labelStyle = style.LabelStyle; var keyStyle = style.KeyStyle;
        var labelSize = style.LabelSize; var keySize = style.KeySize; var chipImg = style.ChipImg;
        var hint = NewObj("Hint_" + name, _row.transform);
        var h = hint.AddComponent<HorizontalLayoutGroup>();
        h.spacing = 10f; h.childAlignment = TextAnchor.MiddleCenter;
        h.childControlWidth = h.childControlHeight = true;
        h.childForceExpandWidth = h.childForceExpandHeight = false;

        var l = NewText("Label", hint.transform, labelStyle, labelSize);
        l.text = labelText;

        var chip = NewObj("Chip", hint.transform);
        if (chipImg != null)
        {
            var img = chip.AddComponent<Image>();
            img.sprite = chipImg.sprite; img.type = chipImg.type; img.color = chipImg.color; img.raycastTarget = false;
        }
        var c = chip.AddComponent<HorizontalLayoutGroup>();
        c.spacing = 0f; c.childAlignment = TextAnchor.MiddleCenter;
        c.childControlWidth = c.childControlHeight = true;
        c.childForceExpandWidth = c.childForceExpandHeight = false;
        Spacer(chip.transform, 12f);
        var k = NewText("Key", chip.transform, keyStyle, keySize);
        k.text = keyText;
        var le = k.gameObject.AddComponent<LayoutElement>();
        le.minHeight = k.preferredHeight + 12f;
        Spacer(chip.transform, 12f);

        keyOut = k;
        { var returnedAt180 = hint; if (RuleLog.IsOn()) ModLog.Verbose($"HintStyle.AddHint: returns {returnedAt180} (line 180)"); return returnedAt180; }
    }

    /// <summary>The point size a TMP text is really drawn with (handles auto-sizing).</summary>
    private static float EffectiveSize(TMP_Text t)
    {
        try
        {
            t.ForceMeshUpdate(false, false);
            var ti = t.textInfo;
            if (ti != null && ti.characterCount > 0 && ti.characterInfo != null && ti.characterInfo.Length > 0 && ti.characterInfo[0].pointSize > 1f)
                { var returnedAt191 = ti.characterInfo[0].pointSize; if (RuleLog.IsOn()) ModLog.Verbose($"HintStyle.EffectiveSize: returns {returnedAt191} (line 191)"); return returnedAt191; }
        }
        catch (Exception e) { ModLog.Verbose("EffectiveSize failed: " + e.Message); }
        return t.enableAutoSizing ? t.fontSizeMax : t.fontSize;
    }

    private static GameObject NewObj(string name, Transform parent)
    {
        if (RuleLog.IsOn()) ModLog.Verbose($"HintStyle.NewObj(name={name}, parent={parent})");
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = _row.layer;
        go.transform.SetParent(parent, false);
        { var returnedAt203 = go; if (RuleLog.IsOn()) ModLog.Verbose($"HintStyle.NewObj: returns {returnedAt203} (line 203)"); return returnedAt203; }
    }

    private static void Spacer(Transform parent, float width)
    {
        if (RuleLog.IsOn()) ModLog.Verbose($"HintStyle.Spacer(parent={parent}, width={width})");
        var s = NewObj("Spacer", parent);
        var le = s.AddComponent<LayoutElement>();
        le.minWidth = le.preferredWidth = width;
    }

    private static TMP_Text NewText(string name, Transform parent, TMP_Text style, float size)
    {
        if (RuleLog.IsOn()) ModLog.Verbose($"HintStyle.NewText(name={name}, parent={parent}, style={style}, size={size})");
        var go = NewObj(name, parent);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.font = style.font;
        t.fontSharedMaterial = style.fontSharedMaterial;
        t.enableAutoSizing = false;
        t.fontSize = size;
        t.fontStyle = style.fontStyle;
        t.color = style.color;
        t.alignment = TextAlignmentOptions.Center;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Overflow;
        t.richText = false;
        t.raycastTarget = false;
        { var returnedAt230 = t; if (RuleLog.IsOn()) ModLog.Verbose($"HintStyle.NewText: returns {returnedAt230} (line 230)"); return returnedAt230; }
    }

    // ------------------------------------------------------------------ placement / text
    /// <summary>Row sits centred directly above whatever the vanilla build hint bar currently occupies.</summary>
    private static void Position(GameObject buildHints)
    {
        if (RuleLog.IsOn()) ModLog.Verbose($"HintStyle.Position(buildHints={buildHints})");
        var texts = buildHints.GetComponentsInChildren<TMP_Text>(false);
        if (texts.Length == 0) { if (RuleLog.IsOn()) ModLog.Verbose($"HintStyle.Position: returns (line 239) - inputs (buildHints={buildHints})"); return; }
        float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
        var c = new Vector3[4];
        foreach (var t in texts)
        {
            ((RectTransform)t.transform).GetWorldCorners(c);
            for (var i = 0; i < 4; i++)
            {
                minX = Mathf.Min(minX, c[i].x); maxX = Mathf.Max(maxX, c[i].x);
                minY = Mathf.Min(minY, c[i].y); maxY = Mathf.Max(maxY, c[i].y);
            }
        }
        var gap = Mathf.Max(10f, (maxY - minY) * 0.9f);
        _row.transform.position = new Vector3((minX + maxX) * 0.5f, maxY + gap, buildHints.transform.position.z);
    }

    private static string BoundKey(string action, string fallback)
    {
        try
        {
            var z = ZInput.instance;
            if (z != null)
            {
                var s = z.GetBoundKeyString(action, true);
                if (!string.IsNullOrEmpty(s))
                {
                    s = Localization.instance.Localize(s);
                    if (!string.IsNullOrEmpty(s) && !s.StartsWith("$") && !s.StartsWith("[")) { var returnedAt266 = s; if (RuleLog.IsOn()) ModLog.Verbose($"HintStyle.BoundKey: returns {returnedAt266} (line 266)"); return returnedAt266; }
                }
            }
        }
        catch (Exception e) { ModLog.Verbose("BoundKey(" + action + ") failed: " + e.Message); }
        return fallback;
    }

    private static string PackKeys() { return "Ctrl + Alt + " + BoundKey("Attack", "Mouse-1"); }
    private static string RepackKeys() { return "Alt + " + BoundKey("Remove", "Mouse-3"); }

    private static void RefreshKeys()
    {
        if (RuleLog.IsOn()) ModLog.Every("HintStyle.RefreshKeys", 2d, $"HintStyle.RefreshKeys: pack '{PackKeys()}', repack '{RepackKeys()}'");
        var a = PackKeys(); if (_packKey.text != a) _packKey.text = a;
        var b = RepackKeys(); if (_repackKey.text != b) _repackKey.text = b;
    }

    // ------------------------------------------------------------------ diagnostics (VerboseLogging only)
    private static void DumpHierarchy(Transform root, Transform tpl, TMP_Text label, TMP_Text key, Image chip)
    {
        var lines = 0;
        ModLog.Verbose("KeyHints dump: template=" + (tpl != null ? tpl.name : "none") + " label=" + label.name + " key=" + key.name + " chipImage=" + (chip != null ? chip.name : "none"));
        Walk(root, 0, ref lines);
    }

    private static void Walk(Transform t, int depth, ref int lines)
    {
        if (lines++ > 150) { if (RuleLog.IsOn()) ModLog.Verbose($"HintStyle.Walk: returns (line 294) - inputs (t={t}, depth={depth}, lines={lines})"); return; }
        var tmp = t.GetComponent<TMP_Text>();
        var comps = new List<string>();
        foreach (var c in t.GetComponents<Component>()) if (c != null) comps.Add(c.GetType().Name);
        ModLog.Verbose("KeyHints dump: " + new string('.', depth) + t.name + " active=" + t.gameObject.activeSelf + " [" + string.Join(",", comps.ToArray()) + "]" + (tmp != null ? " text='" + tmp.text + "'" : ""));
        foreach (Transform c in t) Walk(c, depth + 1, ref lines);
    }
}
