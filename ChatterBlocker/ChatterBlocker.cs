using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChatterBlocker
{
    public static class ChatterBlocker
    {
        private static readonly Dictionary<KeyCode, long> _lastSyncKeyDownTimeMs = new();
        private static readonly Dictionary<ushort, long> _lastKeyDownTimeNs = new();
        private static readonly Dictionary<ushort, bool> _keyWasReleased = new();
        private static readonly HashSet<ushort> _blockedDownKeys = new();

        public static bool ShouldBlock(ushort vk, long eventTimeNs)
        {
            int intervalMs = Main.ChatterBlockInterval;
            if (intervalMs <= 0) return false;

            // Idempotence: If this button has already been accepted with the same timestamp, just let it through.
            if (_lastKeyDownTimeNs.TryGetValue(vk, out long lastTime) && eventTimeNs == lastTime)
                return false;

            long intervalNs = intervalMs * 1_000_000L;

            if (_lastKeyDownTimeNs.TryGetValue(vk, out lastTime))
            {
                if (eventTimeNs - lastTime < intervalNs)
                {
                    _blockedDownKeys.Add(vk);
                    return true;
                }
            }

            _lastKeyDownTimeNs[vk] = eventTimeNs;
            _keyWasReleased[vk] = false;
            return false;
        }

        public static bool ShouldBlockKeyUp(ushort vk)
        {
            return _blockedDownKeys.Remove(vk);
        }

        public static void OnKeyUp(ushort vk)
        {
            _keyWasReleased[vk] = true;
        }

        public static bool ShouldBlockSync(KeyCode key)
        {
            int intervalMs = Main.ChatterBlockInterval;
            if (intervalMs <= 0) return false;

            long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            if (_lastSyncKeyDownTimeMs.TryGetValue(key, out long lastTime))
            {
                long diff = nowMs - lastTime;
                if (diff > 0 && diff < intervalMs)
                    return true;
            }

            _lastSyncKeyDownTimeMs[key] = nowMs;
            return false;
        }

        public static void Reset()
        {
            _lastSyncKeyDownTimeMs.Clear();
            _lastKeyDownTimeNs.Clear();
            _keyWasReleased.Clear();
            _blockedDownKeys.Clear();
        }
    }
}
