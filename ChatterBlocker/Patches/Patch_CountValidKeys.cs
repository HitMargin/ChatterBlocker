using HarmonyLib;
using UnityEngine;

namespace ChatterBlocker.Patches;

// ── 同步输入路径去抖 ──
// CountValidKeysPressed Postfix: 遍历 GetMainPressKeys() 中 KeyCode，
// 调 ShouldBlockSync 过滤弹键并修正 __result。
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