using System;
using System.Collections.Generic;
using UnityEngine;

namespace PlayroomsCoop
{
    internal static partial class World
    {
        private static bool _replayingDevicesHome;

        private static void HelpMenuHooks()
        {
            if (R.T("TwoPixels.Popups.HelpPopup.HelpPopup") == null) return;
            R.Patch(R.T(BallServiceT), "ResetRoomBalls", 1, typeof(Hooks), nameof(Hooks.ResetBallsPrefix), nameof(Hooks.ResetBallsPostfix));
            // scanned every ball in the room twice each morning, froze big rooms like the hotel
            // R.Patch(R.T(BallServiceT), "HealWedgedSpots", 2, typeof(Hooks), nameof(Hooks.HealBallsPrefix), nameof(Hooks.HealBallsPostfix));
            R.Patch(R.T(BallServiceT), "HealStuckBalls", 0, typeof(Hooks), nameof(Hooks.GameHealPrefix), nameof(Hooks.GameHealPostfix));
            R.Patch(R.T(BallServiceT), "RelocateSpot", 3, typeof(Hooks), null, nameof(Hooks.SpotMovedPostfix));
            R.Patch(R.T("TwoPixels.Ball.DeviceHomecoming"), "ReturnAllHome", 0, typeof(Hooks), null, nameof(Hooks.DevicesHomePostfix));
            R.Patch(R.T("TwoPixels.Popups.HelpPopup.HelpPopup"), "OnClickRestartDay", 0, typeof(Hooks), nameof(Hooks.RestartDayPrefix), null);
            R.Patch(R.T("TwoPixels.BackroomCleaner.GameManager"), "RestartDay", 0, typeof(Hooks), nameof(Hooks.RestartDayPrefix), null);
        }

        internal static List<(int idx, Vector3 pos)> BallSpotsBefore(object room)
        {
            if (!_connected || _applying || room == null) return null;
            var list = new List<(int, Vector3)>();
            try
            {
                var spots = R.Get(room, "_ballSpots");
                int n = R.Count(spots);
                for (int i = 0; i < n; i++)
                {
                    var spot = R.At(spots, i);
                    if (spot == null || R.Get<bool>(spot, "IsCollected")) continue;
                    list.Add((i, (Vector3)R.Get(spot, "Position")));
                }
            }
            catch (Exception e) { Plugin.Log.LogWarning("[Help] reset balls: " + e.Message); return null; }
            return list;
        }

        // only had the help menu reset, the game unsticking balls on its own needs the same send
        // internal static void SendResetBalls(object room, List<(int idx, Vector3 pos)> before)
        internal static void SendResetBalls(object room, List<(int idx, Vector3 pos)> before, string logStart = "[Help] reset")
        {
            if (before == null || !_connected) return;
            try
            {
                var id = R.Get<string>(room, "RoomId");
                var spots = R.Get(room, "_ballSpots");
                int moved = 0;
                foreach (var (idx, pos) in before)
                {
                    var spot = R.At(spots, idx);
                    if (spot == null || R.Get<bool>(spot, "IsCollected")) continue;
                    var now = (Vector3)R.Get(spot, "Position");
                    if (Dist(now, pos) < 0.05f) continue;
                    _outMoved.Add((id, idx, now));
                    moved++;
                }
                // log always said help reset, wrong when the game did it
                // if (moved > 0) Plugin.Log.LogInfo($"[Help] reset {moved} balls in {id} for everyone");
                if (moved > 0) Plugin.Log.LogInfo($"{logStart} {moved} balls in {id} for everyone");
            }
            catch (Exception e) { Plugin.Log.LogWarning("[Help] reset balls: " + e.Message); }
        }

        // the game unsticking balls when a new day starts
        private static bool _gameHealing;
        private static int _gameHealed;

        internal static void GameHealStart()
        {
            _gameHealing = true;
            _gameHealed = 0;
        }

        internal static void GameHealEnd()
        {
            _gameHealing = false;
            if (_gameHealed > 0) Plugin.Log.LogInfo($"[Balls] game unstuck {_gameHealed} balls; moved them for everyone");
        }

        internal static void GameMovedSpot(object spot)
        {
            if (!_gameHealing || !_connected || _applying || spot == null) return;

            var room = R.Get(spot, "_room");
            if (room == null) return;
            int index = IndexOf(room, spot);
            if (index < 0) return;

            var position = (Vector3)R.Get(spot, "Position");
            _outMoved.Add((R.Get<string>(room, "RoomId"), index, position));
            _gameHealed++;
        }

        internal static void SendDevicesHome(int count)
        {
            if (!_connected || _replayingDevicesHome) return;
            Protocol.W.Begin(Msg.DevicesHome).SendAll(SteamNet.SendReliable);
            Plugin.Log.LogInfo($"[Help] devices sent home ({count}); every player follows");
        }

        private static void OnDevicesHome()
        {
            if (!InGameWorld()) return;
            _replayingDevicesHome = true;
            try
            {
                int n = Convert.ToInt32(R.CallStatic(R.T("TwoPixels.Ball.DeviceHomecoming"), "ReturnAllHome"));
                Plugin.Log.LogInfo($"[Help] {Net.NameOf(Net.Sender)} sent the devices home ({n} here)");
                CoopBehaviour.Notify($"{Net.NameOf(Net.Sender)} sent all devices home");
            }
            catch (Exception e) { Plugin.Log.LogWarning("[Help] devices home: " + e.Message); }
            finally { _replayingDevicesHome = false; }
        }

        internal static bool AllowRestartDay()
        {
            if (!_connected) return true;
            const string text = "Restart day is off in co-op. Everyone would end up in a different day.";
            try { R.CallStatic(R.T("TwoPixels.Popups.HelpPopup.HelpPopup"), "ShowInfo", text); }
            catch { CoopBehaviour.Notify(text); }
            return false;
        }
    }
}
