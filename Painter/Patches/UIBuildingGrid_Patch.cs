﻿using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

namespace ExcelDsp.Painter.Patches;

/// <summary>Patch for <see cref="UIBuildingGrid"/></summary>
[HarmonyPatch(typeof(UIBuildingGrid))]
internal class UIBuildingGrid_Patch
{
    /// <summary>Before each update action when foundation cursor is active</summary>
    [HarmonyTranspiler, HarmonyPatch(nameof(UIBuildingGrid.Update))]
    public static IEnumerable<CodeInstruction> Update_Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        FieldInfo brushSize = AccessTools.Field(typeof(BuildTool_Reform), nameof(BuildTool_Reform.brushSize));
        FieldInfo cursorIndices = AccessTools.Field(typeof(BuildTool_Reform), nameof(BuildTool_Reform.cursorIndices));

        CodeMatcher matcher = new CodeMatcher(instructions)
            .MatchForward(false,
                new CodeMatch(i => i.opcode == OpCodes.Ldfld && (FieldInfo)i.operand == brushSize),
                new CodeMatch(OpCodes.Ldloc_S),
                new CodeMatch(i => i.opcode == OpCodes.Ldfld && (FieldInfo)i.operand == brushSize),
                new CodeMatch(OpCodes.Mul)
            );

        if(!matcher.IsValid)
            throw new InvalidOperationException("Failed to find patch target");

        return matcher
            .RemoveInstructions(4)
            .InsertAndAdvance(
                new CodeInstruction(OpCodes.Ldfld, cursorIndices),
                new CodeInstruction(OpCodes.Ldlen)
            )
            .InstructionEnumeration();
    }
}
