using HarmonyLib;

namespace ChatterBlocker.Patches;

// Refresh
[HarmonyPatch(typeof(scrController), "Start")]
internal static class Patch_Start
{
    [HarmonyPrefix]
    internal static void Prefix()
    {
        ChatterBlocker.Reset();
    }
}

[HarmonyPatch(typeof(scrController), "Restart")]
internal static class Patch_Restart
{
    [HarmonyPrefix]
    internal static void Prefix()
    {
        ChatterBlocker.Reset();
    }
}
