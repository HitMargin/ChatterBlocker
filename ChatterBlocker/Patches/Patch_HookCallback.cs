using HarmonyLib;
using SkyHook;

namespace ChatterBlocker.Patches;

[HarmonyPatch(typeof(SkyHookManager), "HookCallback")]
internal static class Patch_HookCallback
{
    [HarmonyPrefix]
    internal static bool Prefix(SkyHookEvent ev)
    {
        int interval = Main.ChatterBlockInterval;
        if (interval <= 0) return true;

        if (ev.Type == EventType.KeyPressed)
        {
            long eventTimeNs = ev.GetTimeInTicks() * 100L;
            if (ChatterBlocker.ShouldBlock(ev.Key, eventTimeNs))
                return false; // Intercept KeyDown
            return true;
        }
        else if (ev.Type == EventType.KeyReleased)
        {
            // Clear the status, but always let it through
            ChatterBlocker.ShouldBlockKeyUp(ev.Key);
            ChatterBlocker.OnKeyUp(ev.Key);
            return true;
        }
        return true;
    }
}