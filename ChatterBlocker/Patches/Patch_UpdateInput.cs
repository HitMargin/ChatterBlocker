using HarmonyLib;
using SkyHook;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;
using September;

namespace ChatterBlocker.Patches;

[HarmonyPatch(typeof(scrController), "UpdateInput")]
internal static class Patch_UpdateInput
{
    private static MethodInfo _shouldBlockMethod;
    private static MethodInfo _shouldBlockKeyUpMethod;
    private static MethodInfo _onKeyUpMethod;
    private static FieldInfo _skyHookEventKeyField;

    static Patch_UpdateInput()
    {
        _shouldBlockMethod = PatchManager.GetMethodInfo(
            typeof(ChatterBlocker),
            nameof(ChatterBlocker.ShouldBlock),
            [typeof(ushort), typeof(long)]
        );
        _shouldBlockKeyUpMethod = PatchManager.GetMethodInfo(
            typeof(ChatterBlocker),
            nameof(ChatterBlocker.ShouldBlockKeyUp),
            [typeof(ushort)]
        );
        _onKeyUpMethod = PatchManager.GetMethodInfo(
            typeof(ChatterBlocker),
            nameof(ChatterBlocker.OnKeyUp),
            [typeof(ushort)]
        );
        _skyHookEventKeyField = PatchManager.GetFieldInfo(typeof(SkyHookEvent), "Key");
    }

    [HarmonyTranspiler]
    internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        try { return Transpile(instructions, generator); }
        catch (Exception ex)
        {
            Debug.LogError($"[CB] Transpiler exception: {ex}");
            return instructions;
        }
    }

    private static IEnumerable<CodeInstruction> Transpile(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        var codes = instructions.ToList();
        var keyMaskField = typeof(AsyncInputManager).GetField("keyMask");

        // Locate loop head (TryDequeue)
        int loopHead = -1;
        for (int i = 0; i < codes.Count - 4; i++)
        {
            if (codes[i].opcode == OpCodes.Ldarg_0 &&
                codes[i + 1].opcode == OpCodes.Ldfld &&
                (codes[i + 1].operand as FieldInfo)?.Name == "sortedKeyQueue" &&
                codes[i + 2].opcode == OpCodes.Ldloca_S)
            {
                for (int j = i + 2; j < Math.Min(i + 10, codes.Count); j++)
                {
                    if (codes[j].opcode == OpCodes.Callvirt &&
                        (codes[j].operand as MethodInfo)?.Name == "TryDequeue")
                    {
                        loopHead = i;
                        break;
                    }
                }
                if (loopHead >= 0) break;
            }
        }
        if (loopHead < 0) { Debug.LogWarning("[CB] Loop head not found."); return codes; }

        // Locate KeyDown insertion point (before Contains)
        int insertKeyDown = -1;
        for (int i = 0; i < codes.Count - 2; i++)
        {
            if (codes[i].opcode == OpCodes.Ldsfld &&
                codes[i].operand is FieldInfo fi &&
                fi == keyMaskField &&
                codes[i + 2].opcode == OpCodes.Callvirt &&
                (codes[i + 2].operand as MethodInfo)?.Name == "Contains")
            {
                insertKeyDown = i;
                break;
            }
        }
        if (insertKeyDown < 0) { Debug.LogWarning("[CB] KeyDown insertion point not found."); return codes; }

        // Locate KeyUp insertion point (before Remove)
        int insertKeyUp = -1;
        for (int i = 0; i < codes.Count - 2; i++)
        {
            if (codes[i].opcode == OpCodes.Ldsfld &&
                codes[i].operand is FieldInfo fi &&
                fi == keyMaskField &&
                codes[i + 2].opcode == OpCodes.Callvirt &&
                (codes[i + 2].operand as MethodInfo)?.Name == "Remove")
            {
                insertKeyUp = i;
                break;
            }
        }
        if (insertKeyUp < 0) { Debug.LogWarning("[CB] KeyUp insertion point not found."); return codes; }

        // Locate element and priority locals (using the same robust method as before)
        object elementLocalOperand = null;
        object priorityLocalOperand = null;

        // Find TryDequeue call between loopHead and insertKeyDown (same scope as original working version)
        int tryDequeueIdx = -1;
        for (int i = loopHead; i < insertKeyDown; i++)
        {
            if (codes[i].opcode == OpCodes.Callvirt &&
                (codes[i].operand as MethodInfo)?.Name == "TryDequeue")
            {
                tryDequeueIdx = i;
                break;
            }
        }

        if (tryDequeueIdx >= 0)
        {
            int ldlocaCount = 0;
            // Walk backward from the call: first ldloca = priority (2nd arg), second = element (1st arg)
            for (int j = tryDequeueIdx - 1; j >= loopHead && ldlocaCount < 2; j--)
            {
                if (codes[j].opcode == OpCodes.Ldloca || codes[j].opcode == OpCodes.Ldloca_S)
                {
                    if (ldlocaCount == 0)
                        priorityLocalOperand = codes[j].operand;
                    else if (ldlocaCount == 1)
                        elementLocalOperand = codes[j].operand;
                    ldlocaCount++;
                }
            }
        }

        if (elementLocalOperand == null || priorityLocalOperand == null)
        {
            Debug.LogWarning("[CB] Locals not found.");
            return codes;
        }

        // Create loop label for KeyDown branch 
        Label loopLabel;
        if (codes[loopHead].labels.Count > 0)
            loopLabel = codes[loopHead].labels[0];
        else
        {
            loopLabel = generator.DefineLabel();
            codes[loopHead].labels.Add(loopLabel);
        }

        // Build KeyDown injection (original working logic)
        var keyDownInjection = new List<CodeInstruction>
            {
                new CodeInstruction(OpCodes.Ldloca, elementLocalOperand),
                new CodeInstruction(OpCodes.Ldfld, _skyHookEventKeyField),
                new CodeInstruction(OpCodes.Ldloc, priorityLocalOperand),
                new CodeInstruction(OpCodes.Conv_I8),
                new CodeInstruction(OpCodes.Ldc_I8, 100L),
                new CodeInstruction(OpCodes.Mul),
                new CodeInstruction(OpCodes.Call, _shouldBlockMethod),
                new CodeInstruction(OpCodes.Brtrue, loopLabel)
            };

        // Build KeyUp injection (simple calls, no jumps)
        var keyUpInjection = new List<CodeInstruction>
            {
                new CodeInstruction(OpCodes.Ldloca, elementLocalOperand),
                new CodeInstruction(OpCodes.Ldfld, _skyHookEventKeyField),
                new CodeInstruction(OpCodes.Call, _shouldBlockKeyUpMethod),
                new CodeInstruction(OpCodes.Pop),
                new CodeInstruction(OpCodes.Ldloca, elementLocalOperand),
                new CodeInstruction(OpCodes.Ldfld, _skyHookEventKeyField),
                new CodeInstruction(OpCodes.Call, _onKeyUpMethod)
            };

        // Insert both injections
        var result = new List<CodeInstruction>();
        for (int i = 0; i < codes.Count; i++)
        {
            if (i == insertKeyDown)
                result.AddRange(keyDownInjection);
            if (i == insertKeyUp)
                result.AddRange(keyUpInjection);
            result.Add(codes[i]);
        }

        Debug.Log("[CB] Dual insertion Transpiler applied.");
        return result;
    }
}