using HarmonyLib;
using SkyHook;

namespace ChatterBlocker.Patches;

// ── SkyHook 事件回调拦截 ──
// 在 SkyHookManager.HookCallback 将事件派发到 KeyUpdated 前拦截。
[HarmonyPatch(typeof(SkyHook.SkyHookManager), "HookCallback")]
internal static class Patch_HookCallback
{
    [HarmonyPrefix]
    internal static bool Prefix(SkyHookEvent ev)
    {
        int interval = Main.ChatterBlockInterval;
        if (interval <= 0) return true;

        // 只拦 key-down，key-up 放行
        if ((int)ev.Type != 0) return true;

        long eventTimeNs = (long)ev.GetTimeInTicks() * 100L;
        if (ChatterBlocker.ShouldBlock(ev.Key, eventTimeNs))
            return false; // 拦截此事件

        return true;
    }
}
