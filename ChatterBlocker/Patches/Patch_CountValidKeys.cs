using HarmonyLib;
using UnityEngine;

namespace ChatterBlocker.Patches;

// ── Debouncing the synchronous input path──
//CountValidKeysPressed Postfix: loop through KeyCode in GetMainPressKeys(),
//Adjust ShouldBlockSync to filter key presses and fix __result.
[HarmonyPatch(typeof(scrPlayer), "CountValidKeysPressed")]
internal static class Patch_CountValidKeys
{
    [HarmonyPostfix]
    internal static void Postfix(ref int __result)
    {
        if (Main.ChatterBlockInterval <= 0) return;

        foreach (var anyKey in RDInput.GetMainPressKeys())
        {
            if (anyKey.value is KeyCode keyCode
                && ChatterBlocker.ShouldBlockSync(keyCode))
            {
                __result = Mathf.Max(0, __result - 1);
            }
        }
    }
}