using System;
using System.Reflection;
using UnityEngine;

namespace PlayroomsCoop
{
    public partial class CoopBehaviour
    {
        private const string MainMenuCanvasT = "TwoPixels.UserInterfaces.MainMenuCanvas";
        private const string ButtonLabel = "Co-op";

        private GameObject _mmButton;
        private object _mmLabel;
        private float _nextMmCheck;
        private bool _mmFailed;

        // Tick
        private void MainMenuButtonTick(float now)
        {
            if (_mmFailed || !Plugin.MainMenuButton.Value || now < _nextMmCheck) return;
            _nextMmCheck = now + 1.37f;
            try
            {
                if (_mmButton != null)
                {
                    if (_mmLabel != null && R.Get<string>(_mmLabel, "text") != ButtonLabel) R.Set(_mmLabel, "text", ButtonLabel);
                    return;
                }
                _mmLabel = null;
                object canvas = null;
                foreach (var c in R.FindAll(R.T(MainMenuCanvasT))) { canvas = c; break; }
                if (canvas == null) return;
                var view = R.Get(canvas, "_view");
                if (view == null) return;
                var social = R.Get(view, "steamButton") as Component;
                var text = R.Get(view, "settingsButton") as Component;
                if (social == null || text == null) return;
                BuildMainMenuButton(social, text);
            }
            catch (Exception e)
            {
                _mmFailed = true;
                Plugin.Log.LogWarning("Couldn't add the Co-op button to the main menu (F8 still works): " + e.Message);
            }
        }

        // Build
        private void BuildMainMenuButton(Component social, Component textSource)
        {
            var row = R.Get(social.transform, "parent") as Transform;
            var go = Instantiate(social.gameObject, row);
            try { FillMainMenuButton(go, social, textSource); }
            catch { UnityEngine.Object.Destroy(go); throw; }
        }

        private void FillMainMenuButton(GameObject go, Component social, Component textSource)
        {
            go.name = "PlayroomsCoop_MainMenuButton";
            var rt = go.transform;
            var ignore = Component(go, "UnityEngine.UI.LayoutElement") ?? GenericCall(go, "AddComponent", R.T("UnityEngine.UI.LayoutElement"));
            if (ignore != null) R.Set(ignore, "ignoreLayout", true);

            foreach (var img in Components(go, "UnityEngine.UI.Image"))
                if (!R.Same(((Component)img).gameObject, go)) ((Behaviour)img).enabled = false;
            var bg = Component(go, "UnityEngine.UI.Image");
            var srcBg = Component(textSource.gameObject, "UnityEngine.UI.Image");
            if (bg != null && srcBg != null)
                foreach (var prop in new[] { "sprite", "type", "pixelsPerUnitMultiplier", "color" })
                    try { R.Set(bg, prop, R.Get(srcBg, prop)); } catch { }

            float iconW = Width(social.transform), iconH = Height(social.transform);
            R.Set(Rect(rt), "sizeDelta", Vec2(iconW * 1.72f, iconH));
            R.Set(Rect(rt), "pivot", Vec2(1f, 0.5f));
            float scale = LossyX(social.transform);
            var frame = WideParent(R.Get(go.transform, "parent") as Transform, iconW * scale * 6f);
            float right = frame != null ? RightEdge(frame) - iconW * scale * 0.18f : social.transform.position.x + iconW * scale * 3f;
            var sp = social.transform.position;
            rt.position = new Vector3(right, sp.y, sp.z);

            var srcLabel = FirstText(textSource.gameObject);
            if (srcLabel != null)
            {
                var labelGo = Instantiate(((Component)srcLabel).gameObject, rt);
                var lt = labelGo.transform;
                R.Set(Rect(lt), "anchorMin", Vec2(0f, 0f));
                R.Set(Rect(lt), "anchorMax", Vec2(1f, 1f));
                R.Set(Rect(lt), "offsetMin", Vec2(2f, 2f));
                R.Set(Rect(lt), "offsetMax", Vec2(-2f, -2f));
                foreach (var c in Components(labelGo, "UnityEngine.Behaviour"))
                    if (c.ToString().IndexOf("Locali", StringComparison.OrdinalIgnoreCase) >= 0) ((Behaviour)c).enabled = false;
                _mmLabel = FirstText(labelGo);
                if (_mmLabel != null)
                {
                    try { R.Set(_mmLabel, "enableAutoSizing", true); R.Set(_mmLabel, "fontSizeMin", 8f); R.Set(_mmLabel, "fontSizeMax", iconH * 0.42f); } catch { }
                    try
                    {
                        var style = _mmLabel.GetType().GetProperty("fontStyle");
                        style.SetValue(_mmLabel, Enum.ToObject(style.PropertyType, 1));
                    }
                    catch { }
                    R.Set(_mmLabel, "text", ButtonLabel);
                }
            }

            var button = Component(go, "UnityEngine.UI.Button");
            if (button == null) throw new Exception("no button on the copy");
            var eventType = R.T("UnityEngine.UI.Button+ButtonClickedEvent") ?? R.T("UnityEngine.UI.Button.ButtonClickedEvent");
            var onClick = Activator.CreateInstance(eventType);
            R.Set(button, "onClick", onClick);
            try
            {
                var nav = R.Get(button, "navigation");
                nav.GetType().GetProperty("mode")?.SetValue(nav, Enum.ToObject(nav.GetType().GetProperty("mode").PropertyType, 0));
                R.Set(button, "navigation", nav);
            }
            catch { }
            _mmButton = go;
            Plugin.Log.LogInfo("Added the Co-op button to the main menu");
        }

        // Click
        private void MainMenuButtonClick()
        {
            if (_mmButton == null || !_mmButton.activeInHierarchy) return;
            var e = R.GetStatic(R.T("UnityEngine.Event"), "current");
            if (e == null || Convert.ToInt32(R.Get(e, "type")) != 0 || Convert.ToInt32(R.Get(e, "button")) != 0) return;
            if (!MainMenuClick.Under(_mmButton, R.Get(e, "mousePosition"))) return;
            R.Call(e, "Use");
            SetMenu(!_menuOpen, Time.realtimeSinceStartup);
        }

        private static object FirstText(GameObject go)
        {
            foreach (var t in Components(go, "TMPro.TMP_Text")) return t;
            foreach (var t in Components(go, "UnityEngine.UI.Text")) return t;
            return null;
        }

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

        // Helpers
        private static Transform RootCanvas(Transform t)
        {
            var best = R.Get(t, "parent") as Transform;
            for (var p = best; p != null; p = R.Get(p, "parent") as Transform)
                if (Component(p.gameObject, "UnityEngine.Canvas") != null) best = p;
            return best;
        }

        internal static object Vec2(float x, float y)
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

        internal static object Rect(Transform t)
        {
            var rectType = R.T("UnityEngine.RectTransform");
            if (rectType.IsInstanceOfType(t)) return t;
            var cast = t.GetType().GetMethod("TryCast")?.MakeGenericMethod(rectType).Invoke(t, null);
            return cast ?? throw new Exception(t.name + " has no RectTransform");
        }

        private static GameObject AsGameObject(object o)
        {
            if (o == null) return null;
            return o as GameObject ?? (GameObject)Activator.CreateInstance(typeof(GameObject), R.Ptr(o));
        }

        private static object Component(GameObject go, string typeName)
        {
            var t = R.T(typeName);
            if (t == null) return null;
            return GenericCall(go, "GetComponent", t);
        }

        private static System.Collections.IEnumerable Components(GameObject go, string typeName)
        {
            var t = R.T(typeName);
            if (t == null) return new object[0];
            return GenericCall(go, "GetComponentsInChildren", t, true) as System.Collections.IEnumerable ?? new object[0];
        }

        internal static object GenericCall(object target, string name, Type t, params object[] args)
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

    internal static class MainMenuClick
    {
        public static bool Under(GameObject button, object guiPoint)
        {
            var rect = CoopBehaviour.Rect(button.transform);
            object cam = null;
            var canvas = CoopBehaviour.GenericCall(button, "GetComponentInParent", R.T("UnityEngine.Canvas"));
            if (canvas != null)
            {
                var root = R.Get(canvas, "rootCanvas") ?? canvas;
                if (Convert.ToInt32(R.Get(root, "renderMode")) != 0) cam = R.Get(root, "worldCamera");
            }
            var t = guiPoint.GetType();
            float x = Convert.ToSingle(t.GetField("x").GetValue(guiPoint)), y = Convert.ToSingle(t.GetField("y").GetValue(guiPoint));
            var screen = CoopBehaviour.Vec2(x, Screen.height - y);
            foreach (var m in R.T("UnityEngine.RectTransformUtility").GetMethods(BindingFlags.Public | BindingFlags.Static))
            {
                if (m.Name != "RectangleContainsScreenPoint" || m.GetParameters().Length != 3) continue;
                return Convert.ToBoolean(m.Invoke(null, new[] { rect, screen, cam }));
            }
            return false;
        }
    }
}
