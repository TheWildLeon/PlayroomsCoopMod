using System;
using System.Collections;
using System.Reflection;
using UnityEngine;

namespace PlayroomsNewGamePlus;

internal static class MenuButton
{
    private const string MainMenuCanvasT = "TwoPixels.UserInterfaces.MainMenuCanvas";
    private const string CoopButtonName = "PlayroomsCoop_MainMenuButton";
    private const string ButtonLabel = "NG+";

    private static GameObject _button;
    private static object _label;
    private static Transform _frame;
    private static float _iconW, _iconY, _iconZ, _scale;
    private static float _nextCheck, _waitingSince = -1f;
    private static bool _failed;

    public static bool Showing => _button != null && _button.activeInHierarchy;

    public static bool UseFallback => _failed || !Plugin.MainMenuButton.Value;

    // Tick
    public static void Tick()
    {
        if (_failed || !Plugin.MainMenuButton.Value) return;
        float now = Time.realtimeSinceStartup;
        if (now < _nextCheck) return;
        _nextCheck = now + (_button == null ? 0.2f : 1.43f);
        try
        {
            if (_button == null)
            {
                if (!Ui.HasSlots || !Game.MainMenuShowing()) { _waitingSince = -1f; return; }
                if (_waitingSince < 0f) _waitingSince = now;
                if (now - _waitingSince > 8f) throw new Exception("the main menu buttons weren't found");
                var canvas = R.FindFirst(MainMenuCanvasT);
                var view = canvas != null ? R.Get(canvas, "_view") : null;
                if (view == null) return;
                var social = R.Get(view, "steamButton") as Component;
                var textSource = R.Get(view, "settingsButton") as Component;
                if (social == null || textSource == null) return;
                Build(social, textSource);
                return;
            }
            if (_button.activeSelf != Ui.HasSlots) _button.SetActive(Ui.HasSlots);
            if (_label != null && R.Get<string>(_label, "text") != ButtonLabel) R.Set(_label, "text", ButtonLabel);
            Place();
        }
        catch (Exception e)
        {
            _failed = true;
            Plugin.Log.LogWarning((object)("Couldn't add the NG+ button to the main menu (the top right button is used instead): " + e.Message));
        }
    }

    // Click
    public static void CheckClick()
    {
        if (!Showing) return;
        var e = R.GetStatic(R.T("UnityEngine.Event"), "current");
        if (e == null || Convert.ToInt32(R.Get(e, "type")) != 0 || Convert.ToInt32(R.Get(e, "button")) != 0) return;
        if (!UnderMouse(_button, R.Get(e, "mousePosition"))) return;
        R.Call(e, "Use");
        Ui.ToggleMenu();
    }

    // Build
    private static void Build(Component social, Component textSource)
    {
        var row = R.Get(social.transform, "parent") as Transform;
        var go = Instantiate(social.gameObject, row);
        try { Fill(go, social, textSource); }
        catch { UnityEngine.Object.Destroy(go); throw; }
        _button = go;
        Place();
        Plugin.Log.LogInfo((object)"Added the NG+ button to the main menu");
    }

    private static void Fill(GameObject go, Component social, Component textSource)
    {
        go.name = "PlayroomsNewGamePlus_MainMenuButton";
        var layout = Component(go, "UnityEngine.UI.LayoutElement") ?? Generic(go, "AddComponent", R.T("UnityEngine.UI.LayoutElement"));
        if (layout != null) R.Set(layout, "ignoreLayout", true);

        foreach (var img in Components(go, "UnityEngine.UI.Image"))
            if (R.Ptr(((Component)img).gameObject) != R.Ptr(go)) ((Behaviour)img).enabled = false;
        var bg = Component(go, "UnityEngine.UI.Image");
        var srcBg = Component(textSource.gameObject, "UnityEngine.UI.Image");
        if (bg != null && srcBg != null)
            foreach (var prop in new[] { "sprite", "type", "pixelsPerUnitMultiplier", "color" })
                try { R.Set(bg, prop, R.Get(srcBg, prop)); } catch { }

        _iconW = Width(social.transform);
        float iconH = Height(social.transform);
        _scale = LossyX(social.transform);
        var sp = social.transform.position;
        _iconY = sp.y; _iconZ = sp.z;
        _frame = WideParent(R.Get(go.transform, "parent") as Transform, _iconW * _scale * 6f);
        R.Set(Rect(go.transform), "sizeDelta", Vec2(_iconW * 1.72f, iconH));
        R.Set(Rect(go.transform), "pivot", Vec2(1f, 0.5f));

        var srcLabel = FirstText(textSource.gameObject);
        if (srcLabel != null)
        {
            var labelGo = Instantiate(((Component)srcLabel).gameObject, go.transform);
            var lt = Rect(labelGo.transform);
            R.Set(lt, "anchorMin", Vec2(0f, 0f));
            R.Set(lt, "anchorMax", Vec2(1f, 1f));
            R.Set(lt, "offsetMin", Vec2(2f, 2f));
            R.Set(lt, "offsetMax", Vec2(-2f, -2f));
            foreach (var c in Components(labelGo, "UnityEngine.Behaviour"))
                if (c.ToString().IndexOf("Locali", StringComparison.OrdinalIgnoreCase) >= 0) ((Behaviour)c).enabled = false;
            _label = FirstText(labelGo);
            if (_label != null)
            {
                try { R.Set(_label, "enableAutoSizing", true); R.Set(_label, "fontSizeMin", 8f); R.Set(_label, "fontSizeMax", iconH * 0.42f); } catch { }
                try
                {
                    var style = _label.GetType().GetProperty("fontStyle");
                    style.SetValue(_label, Enum.ToObject(style.PropertyType, 1));
                }
                catch { }
                R.Set(_label, "text", ButtonLabel);
            }
        }

        var button = Component(go, "UnityEngine.UI.Button") ?? throw new Exception("no button on the copy");
        var eventType = R.T("UnityEngine.UI.Button+ButtonClickedEvent") ?? R.T("UnityEngine.UI.Button.ButtonClickedEvent");
        R.Set(button, "onClick", Activator.CreateInstance(eventType));
        try
        {
            var nav = R.Get(button, "navigation");
            var mode = nav.GetType().GetProperty("mode");
            mode?.SetValue(nav, Enum.ToObject(mode.PropertyType, 0));
            R.Set(button, "navigation", nav);
        }
        catch { }
    }

    // Place
    private static void Place()
    {
        if (_button == null) return;
        float right;
        var coop = CoopButton();
        if (coop != null) right = coop.position.x - Width(coop) * LossyX(coop) - _iconW * _scale * 0.3f;
        else if (_frame != null) right = RightEdge(_frame) - _iconW * _scale * 0.18f;
        else return;
        var pos = _button.transform.position;
        if (Math.Abs(pos.x - right) > 0.0001f || Math.Abs(pos.y - _iconY) > 0.0001f)
            _button.transform.position = new Vector3(right, _iconY, _iconZ);
    }

    private static Transform CoopButton()
    {
        var row = R.Get(_button.transform, "parent") as Transform;
        if (row == null) return null;
        int n = Convert.ToInt32(R.Get(row, "childCount"));
        for (int i = 0; i < n; i++)
        {
            var child = R.Call(row, "GetChild", i) as Transform;
            if (child != null && child.name == CoopButtonName && child.gameObject.activeSelf) return child;
        }
        return null;
    }

    // Helpers
    private static bool UnderMouse(GameObject target, object guiPoint)
    {
        object cam = null;
        var canvas = Generic(target, "GetComponentInParent", R.T("UnityEngine.Canvas"));
        if (canvas != null)
        {
            var root = R.Get(canvas, "rootCanvas") ?? canvas;
            if (Convert.ToInt32(R.Get(root, "renderMode")) != 0) cam = R.Get(root, "worldCamera");
        }
        var t = guiPoint.GetType();
        float x = Convert.ToSingle(t.GetField("x").GetValue(guiPoint)), y = Convert.ToSingle(t.GetField("y").GetValue(guiPoint));
        var screen = Vec2(x, Screen.height - y);
        foreach (var m in R.T("UnityEngine.RectTransformUtility").GetMethods(BindingFlags.Public | BindingFlags.Static))
        {
            if (m.Name != "RectangleContainsScreenPoint" || m.GetParameters().Length != 3) continue;
            return Convert.ToBoolean(m.Invoke(null, new[] { Rect(target.transform), screen, cam }));
        }
        return false;
    }

    private static object FirstText(GameObject go)
    {
        foreach (var t in Components(go, "TMPro.TMP_Text")) return t;
        foreach (var t in Components(go, "UnityEngine.UI.Text")) return t;
        return null;
    }

    private static object Rect(Transform t) => R.Cast(t, "UnityEngine.RectTransform") ?? throw new Exception(t.name + " has no RectTransform");

    private static float Width(Transform t) => RectValue(t, "width");
    private static float Height(Transform t) => RectValue(t, "height");

    private static float RectValue(Transform t, string name)
    {
        var rect = R.Get(Rect(t), "rect");
        return Convert.ToSingle(rect.GetType().GetProperty(name).GetValue(rect));
    }

    private static float LossyX(Transform t)
    {
        var v = R.Get(t, "lossyScale");
        return Convert.ToSingle(v.GetType().GetField("x").GetValue(v));
    }

    private static Transform WideParent(Transform from, float minWorldWidth)
    {
        for (var p = from; p != null; p = R.Get(p, "parent") as Transform)
        {
            try { if (Width(p) * LossyX(p) >= minWorldWidth) return p; } catch { }
        }
        return null;
    }

    private static float RightEdge(Transform t)
    {
        var pivot = R.Get(Rect(t), "pivot");
        float px = Convert.ToSingle(pivot.GetType().GetField("x").GetValue(pivot));
        return t.position.x + (1f - px) * Width(t) * LossyX(t);
    }

    private static object Vec2(float x, float y)
    {
        var t = R.T("UnityEngine.Vector2");
        var v = Activator.CreateInstance(t);
        t.GetField("x").SetValue(v, x);
        t.GetField("y").SetValue(v, y);
        return v;
    }

    private static GameObject Instantiate(GameObject original, Transform parent)
    {
        foreach (var m in typeof(UnityEngine.Object).GetMethods(BindingFlags.Public | BindingFlags.Static))
        {
            if (m.Name != "Instantiate" || m.IsGenericMethodDefinition) continue;
            var ps = m.GetParameters();
            if (ps.Length == 3 && ps[1].ParameterType == typeof(Transform) && ps[2].ParameterType == typeof(bool))
                return AsGameObject(m.Invoke(null, new object[] { original, parent, false }));
        }
        var go = AsGameObject(R.CallStatic(typeof(UnityEngine.Object), "Instantiate", original));
        R.Call(go.transform, "SetParent", parent, false);
        return go;
    }

    private static GameObject AsGameObject(object o)
    {
        if (o == null) return null;
        return o as GameObject ?? (GameObject)Activator.CreateInstance(typeof(GameObject), R.Ptr(o));
    }

    private static object Component(GameObject go, string typeName)
    {
        var t = R.T(typeName);
        return t == null ? null : Generic(go, "GetComponent", t);
    }

    private static IEnumerable Components(GameObject go, string typeName)
    {
        var t = R.T(typeName);
        if (t == null) return new object[0];
        return Generic(go, "GetComponentsInChildren", t, true) as IEnumerable ?? new object[0];
    }

    private static object Generic(object target, string name, Type t, params object[] args)
    {
        foreach (var m in target.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance))
        {
            if (m.Name != name || !m.IsGenericMethodDefinition || m.GetParameters().Length != args.Length) continue;
            if (args.Length == 1 && m.GetParameters()[0].ParameterType != typeof(bool)) continue;
            return m.MakeGenericMethod(t).Invoke(target, args);
        }
        return null;
    }
}
