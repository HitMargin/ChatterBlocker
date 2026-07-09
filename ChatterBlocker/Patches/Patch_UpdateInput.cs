using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using September;
using SkyHook;
using UnityEngine;

namespace ChatterBlocker.Patches;

[HarmonyPatch(typeof(scrController), "UpdateInput")]
internal static class Patch_UpdateInput
{
    [HarmonyTranspiler]
    internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        try
        {
            return Transpile(instructions);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ChatterBlocker] Transpiler exception: {ex}");
            return instructions;
        }
    }

    private static IEnumerable<CodeInstruction> Transpile(IEnumerable<CodeInstruction> instructions)
    {
        var codes = instructions.ToList();
        var keyMaskField = PatchManager.GetFieldInfo(typeof(AsyncInputManager), "keyMask");
        var shouldBlock = PatchManager.GetMethodInfo(typeof(ChatterBlocker), nameof(ChatterBlocker.ShouldBlock));
        var keyField = PatchManager.GetFieldInfo(typeof(SkyHookEvent), "Key");

        // ── 找循环头 ──
        int loopHead = -1;
        for (int i = 0; i < codes.Count - 4; i++)
        {
            if (codes[i].opcode != OpCodes.Ldarg_0) continue;
            if (codes[i + 1].opcode != OpCodes.Ldfld) continue;
            if (!(codes[i + 1].operand is FieldInfo f1) || f1.Name != "sortedKeyQueue") continue;
            if (codes[i + 2].opcode != OpCodes.Ldloca_S) continue;

            // 确认跟的是 TryDequeue 不是 Enqueue
            bool hasTryDequeue = false;
            for (int j = i + 2; j < Math.Min(i + 8, codes.Count); j++)
            {
                if (codes[j].opcode == OpCodes.Callvirt
                    && codes[j].operand is MethodInfo mm
                    && mm.Name == "TryDequeue")
                {
                    hasTryDequeue = true;
                    break;
                }
            }
            if (!hasTryDequeue) continue;
            loopHead = i;
            break;
        }

        if (loopHead < 0)
        {
            Debug.LogWarning("[ChatterBlocker] Transpiler: loop head not found");
            return codes;
        }

        // ── 找插入点：Contains(keyMask) ──
        int insert = -1;
        for (int i = 0; i < codes.Count - 2; i++)
        {
            if (codes[i].opcode != OpCodes.Ldsfld) continue;
            if (!(codes[i].operand is FieldInfo ff) || ff != keyMaskField) continue;
            if (codes[i + 2].opcode != OpCodes.Callvirt) continue;
            if (!(codes[i + 2].operand is MethodInfo m) || m.Name != "Contains") continue;
            insert = i;
            break;
        }

        if (insert < 0)
        {
            Debug.LogWarning("[ChatterBlocker] Transpiler: keyMask + Contains not found");
            return codes;
        }

        var loopHeadInstr = codes[loopHead];
        // loopHead is a branch target in original code → has at least one label
        var loopHeadLabel = loopHeadInstr.labels[0];
        var result = new List<CodeInstruction>(codes.Count + 7);

        for (int i = 0; i < codes.Count; i++)
        {
            if (i == insert)
            {
                result.Add(new CodeInstruction(OpCodes.Ldloc_3));
                result.Add(new CodeInstruction(OpCodes.Ldfld, keyField));
                result.Add(new CodeInstruction(OpCodes.Ldloc_S, (byte)6));
                result.Add(new CodeInstruction(OpCodes.Conv_I8));
                result.Add(new CodeInstruction(OpCodes.Ldc_I8, 100L));
                result.Add(new CodeInstruction(OpCodes.Mul));
                result.Add(new CodeInstruction(OpCodes.Call, shouldBlock));
                result.Add(new CodeInstruction(OpCodes.Brtrue, loopHeadLabel));
            }
            result.Add(codes[i]);
        }

        Debug.Log("[ChatterBlocker] Transpiler applied OK");
        return result;
    }
}