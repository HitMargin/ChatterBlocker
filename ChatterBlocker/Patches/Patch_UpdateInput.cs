using HarmonyLib;
using September;
using SkyHook;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;

namespace ChatterBlocker.Patches
{
    [HarmonyPatch(typeof(scrController), "UpdateInput")]
    internal static class Patch_UpdateInput
    {
        private static MethodInfo _shouldBlockMethod;
        private static FieldInfo _skyHookEventKeyField;

        static Patch_UpdateInput()
        {
            _shouldBlockMethod = PatchManager.GetMethodInfo(
                typeof(ChatterBlocker),
                nameof(ChatterBlocker.ShouldBlock),
                [typeof(ushort), typeof(long)]
            );
            _skyHookEventKeyField = PatchManager.GetFieldInfo(typeof(SkyHookEvent), "Key");
        }

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
            var keyMaskField = typeof(AsyncInputManager).GetField("keyMask");

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
            if (loopHead < 0)
            {
                Debug.LogWarning("[CB] Loop head not found.");
                return codes;
            }

            int insert = -1;
            for (int i = 0; i < codes.Count - 2; i++)
            {
                if (codes[i].opcode == OpCodes.Ldsfld &&
                    codes[i].operand is FieldInfo fi &&
                    fi == keyMaskField &&
                    codes[i + 2].opcode == OpCodes.Callvirt &&
                    (codes[i + 2].operand as MethodInfo)?.Name == "Contains")
                {
                    insert = i;
                    break;
                }
            }
            if (insert < 0)
            {
                Debug.LogWarning("[CB] Insert point not found.");
                return codes;
            }

            int elementLocal = -1;
            int timeLocal = -1;

            for (int i = loopHead; i < insert; i++)
            {
                if (codes[i].opcode == OpCodes.Callvirt &&
                    (codes[i].operand as MethodInfo)?.Name == "TryDequeue")
                {
                    for (int j = i + 1; j < Math.Min(i + 5, codes.Count); j++)
                    {
                        if (codes[j].opcode == OpCodes.Stloc || codes[j].opcode == OpCodes.Stloc_S)
                        {
                            elementLocal = (codes[j].operand is LocalBuilder lb) ? lb.LocalIndex : (int)codes[j].operand;
                            break;
                        }
                    }
                }

                if (codes[i].opcode == OpCodes.Callvirt &&
                    (codes[i].operand as MethodInfo)?.Name == "GetTimeInTicks")
                {
                    // 该调用返回 long/ulong，之后会有 stloc
                    for (int j = i + 1; j < Math.Min(i + 5, codes.Count); j++)
                    {
                        if (codes[j].opcode == OpCodes.Stloc || codes[j].opcode == OpCodes.Stloc_S)
                        {
                            timeLocal = (codes[j].operand is LocalBuilder lb) ? lb.LocalIndex : (int)codes[j].operand;
                            break;
                        }
                    }
                }

                if (elementLocal >= 0 && timeLocal >= 0) break;
            }

            if (elementLocal < 0 || timeLocal < 0)
            {
                Debug.LogWarning($"[CB] Locals not found: element={elementLocal}, time={timeLocal}");
                return codes;
            }

            var injected = new List<CodeInstruction>
            {
                new CodeInstruction(OpCodes.Ldloc, elementLocal),
                new CodeInstruction(OpCodes.Ldfld, _skyHookEventKeyField),
                new CodeInstruction(OpCodes.Ldloc, timeLocal),
                new CodeInstruction(OpCodes.Conv_I8),
                new CodeInstruction(OpCodes.Ldc_I8, 100L),
                new CodeInstruction(OpCodes.Mul),
                new CodeInstruction(OpCodes.Call, _shouldBlockMethod),
                new CodeInstruction(OpCodes.Brtrue, codes[loopHead].labels[0])
            };

            var result = new List<CodeInstruction>();
            for (int i = 0; i < codes.Count; i++)
            {
                if (i == insert)
                    result.AddRange(injected);
                result.Add(codes[i]);
            }

            Debug.Log("[ChatterBlocker] Transpiler applied successfully.");
            return result;
        }
    }
}