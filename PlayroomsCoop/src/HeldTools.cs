using System;
using UnityEngine;

namespace PlayroomsCoop
{
    internal static partial class World
    {
        private static object _localHandVac, _localHandVacWorld;
        private static float _nextHandVacFind, _nextHandVacCapture;
        private static object _handVacVisual;
        private static Vector3 _handVacHoldPos;
        private static Quaternion _handVacHoldRot;
        private static bool _handVacHoldKnown;

        // Local tool
        internal static byte LocalTool(float now)
        {
            if (!_connected) return 0;
            try
            {
                var shovel = FindShovel();
                if (shovel != null && Convert.ToBoolean(R.Get(shovel, "IsBehaviourActive"))) return Protocol.HeldBroom;
                var vac = FindHandVac(now);
                if (vac != null && Convert.ToBoolean(R.Get(vac, "IsBehaviourActive"))) return Protocol.HeldHandVac;
            }
            catch { }
            return 0;
        }

        private static object FindHandVac(float now)
        {
            object world = Alive(_ballService) ? _ballService : null;
            if (_localHandVac != null && Alive(_localHandVac) && R.Same(world, _localHandVacWorld)) return _localHandVac;
            if (now < _nextHandVacFind) return null;
            _nextHandVacFind = now + 2.3f;
            _localHandVac = MenuInput.FindPlayerBehaviour("HandVacuumBehaviour");
            _localHandVacWorld = _localHandVac != null ? world : null;
            return _localHandVac;
        }

        // Hold capture
        private static void CaptureHandVacHold(float now)
        {
            if (now < _nextHandVacCapture) return;
            _nextHandVacCapture = now + 1.13f;
            var vac = FindHandVac(now);
            if (vac == null || !Convert.ToBoolean(R.Get(vac, "IsBehaviourActive"))) return;
            var data = R.Get(vac, "HandVacuumBehaviourData");
            var vis = data != null ? R.Get(data, "ToolVisualTransform") as Transform : null;
            if (vis == null || !Convert.ToBoolean(R.Get(vis.gameObject, "activeInHierarchy"))) return;
            var cam = R.GetStatic(R.T("UnityEngine.Camera"), "main") as Component;
            if (cam == null) return;
            var ct = cam.transform; var fwd = ct.forward;
            var pivot = new GameObject("PlayroomsCoop_HandVacPivot").transform;
            pivot.position = ct.position;
            pivot.rotation = Quaternion.Euler(0f, Mathf.Atan2(fwd.x, fwd.z) * Mathf.Rad2Deg, 0f);
            var probe = new GameObject("PlayroomsCoop_HandVacProbe").transform;
            try
            {
                probe.position = vis.position; probe.rotation = vis.rotation;
                probe.SetParent(pivot, true);
                _handVacHoldPos = R.Get<Vector3>(probe, "localPosition");
                _handVacHoldRot = R.Get<Quaternion>(probe, "localRotation");
                bool first = !_handVacHoldKnown;
                _handVacVisual = vis.gameObject; _handVacHoldKnown = true;
                if (first) Plugin.Log.LogInfo($"[Tools] hand vacuum model found: held at {_handVacHoldPos} from the camera");
            }
            finally { UnityEngine.Object.Destroy(probe.gameObject); UnityEngine.Object.Destroy(pivot.gameObject); }
        }

        internal static bool BuildGameHandVac(Transform parent, out Transform root)
        {
            root = null;
            if (!_handVacHoldKnown || !Alive(_handVacVisual)) return false;
            if (!CopyModel(_handVacVisual, parent, _handVacHoldPos, _handVacHoldRot, "PlayroomsCoop_GameHandVac", out root, out int parts)) return false;
            Plugin.Log.LogInfo($"[Tools] showing the hand vacuum on the other player ({parts} parts)");
            return true;
        }
    }
}
