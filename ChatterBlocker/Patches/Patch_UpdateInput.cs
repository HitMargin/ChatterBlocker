using HarmonyLib;
using SkyHook;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;
using September;

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

            // 1. Locate loop head (TryDequeue call)
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

            // 2. Locate insertion point (before Contains(keyMask))
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
            if (insert < 0) { Debug.LogWarning("[CB] Insert point not found."); return codes; }

            // 3. Locate element and priority locals (scan backwards from TryDequeue)
            object elementLocalOperand = null;
            object priorityLocalOperand = null;

            int tryDequeueIdx = -1;
            for (int i = loopHead; i < insert; i++)
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
                // Walk backward from the call: first ldloca encountered = priority (2nd arg), second = element (1st arg)
                for (int j = tryDequeueIdx - 1; j >= loopHead && ldlocaCount < 2; j--)
                {
                    if (codes[j].opcode == OpCodes.Ldloca || codes[j].opcode == OpCodes.Ldloca_S)
                    {
                        if (ldlocaCount == 0)
                            priorityLocalOperand = codes[j].operand;   // closest to call = priority
                        else if (ldlocaCount == 1)
                            elementLocalOperand = codes[j].operand;    // further back = element
                        ldlocaCount++;
                    }
                }
            }

            if (elementLocalOperand == null || priorityLocalOperand == null)
            {
                Debug.LogWarning($"[CB] Locals not found: element={elementLocalOperand}, priority={priorityLocalOperand}");
                return codes;
            }

            // (Optional) verify types
            // Debug.Log($"[CB] element type: {(elementLocalOperand as LocalBuilder)?.LocalType}, priority type: {(priorityLocalOperand as LocalBuilder)?.LocalType}");

            // 4. Create a valid label for the loop head using ILGenerator
            Label loopLabel;
            if (codes[loopHead].labels.Count > 0)
                loopLabel = codes[loopHead].labels[0];
            else
            {
                loopLabel = generator.DefineLabel();
                codes[loopHead].labels.Add(loopLabel);
            }

            // 5. Inject IL: use Ldloca for struct (element), load priority as value, convert, call ShouldBlock
            var injected = new List<CodeInstruction>
            {
                new CodeInstruction(OpCodes.Ldloca, elementLocalOperand),   // address of SkyHookEvent
                new CodeInstruction(OpCodes.Ldfld, _skyHookEventKeyField),
                new CodeInstruction(OpCodes.Ldloc, priorityLocalOperand),   // ulong priority
                new CodeInstruction(OpCodes.Conv_I8),                       // ulong -> long
                new CodeInstruction(OpCodes.Ldc_I8, 100L),                  // ticks to ns (1 tick = 100 ns)
                new CodeInstruction(OpCodes.Mul),
                new CodeInstruction(OpCodes.Call, _shouldBlockMethod),
                new CodeInstruction(OpCodes.Brtrue, loopLabel)              // if blocked, discard key
            };

            // 6. Insert at the found position
            var result = new List<CodeInstruction>();
            for (int i = 0; i < codes.Count; i++)
            {
                if (i == insert)
                    result.AddRange(injected);
                result.Add(codes[i]);
            }

            Debug.Log("[CB] Transpiler applied successfully.");
            return result;
        }
    }
}