using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using CWF.Controllers;
using HarmonyLib;
using Verse;

namespace CwfStockpileModules.Patches;

/// <summary>
/// Подменяет только источник вещей в BuildInstallOptions (inventory → inventory+stockpile).
/// CreateInstallAction / DoInstall / onDataChanged(Recalculate) остаются штатными CWF.
/// </summary>
[HarmonyPatch(typeof(InteractionController), "BuildInstallOptions")]
internal static class Patch_BuildInstallOptions
{
	private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
	{
		var codes = instructions.ToList();
		var ownerPawn = FindOwnerPawnField();
		var inventory = AccessTools.Field(typeof(Pawn), nameof(Pawn.inventory));
		var innerContainer = AccessTools.Field(typeof(Pawn_InventoryTracker), nameof(Pawn_InventoryTracker.innerContainer));
		var weapon = AccessTools.Field(typeof(InteractionController), "<weapon>P");
		var enumerate = AccessTools.Method(
			typeof(ModuleAccess),
			nameof(ModuleAccess.EnumerateModuleThingsForEquippedUi));

		if (ownerPawn == null || inventory == null || innerContainer == null || weapon == null || enumerate == null)
		{
			Log.Error("[CWF Stockpile Modules] BuildInstallOptions transpiler: missing fields/methods.");
			return codes;
		}

		for (var i = 0; i < codes.Count - 4; i++)
		{
			if (codes[i].opcode != OpCodes.Ldloc_0)
				continue;
			if (!LoadsField(codes[i + 1], ownerPawn))
				continue;
			if (!LoadsField(codes[i + 2], inventory))
				continue;
			if (!LoadsField(codes[i + 3], innerContainer))
				continue;
			if (codes[i + 4].opcode != OpCodes.Stloc_S && codes[i + 4].opcode != OpCodes.Stloc)
				continue;

			var stloc = codes[i + 4];
			var labels = codes[i].labels.ToList();
			codes[i].labels.Clear();

			var replacement = new List<CodeInstruction>
			{
				new CodeInstruction(OpCodes.Ldarg_0).WithLabels(labels),
				new CodeInstruction(OpCodes.Ldfld, weapon),
				new CodeInstruction(OpCodes.Ldloc_0),
				new CodeInstruction(OpCodes.Ldfld, ownerPawn),
				new CodeInstruction(OpCodes.Call, enumerate),
				stloc
			};

			codes.RemoveRange(i, 5);
			codes.InsertRange(i, replacement);

			Log.Message("[CWF Stockpile Modules] BuildInstallOptions: source → inventory+stockpile.");
			return codes;
		}

		Log.Error("[CWF Stockpile Modules] BuildInstallOptions transpiler: inventory pattern not found.");
		return codes;
	}

	private static bool LoadsField(CodeInstruction instruction, FieldInfo field)
	{
		return instruction.opcode == OpCodes.Ldfld && Equals(instruction.operand, field);
	}

	private static FieldInfo? FindOwnerPawnField()
	{
		foreach (var type in typeof(InteractionController).GetNestedTypes(
			         BindingFlags.Public | BindingFlags.NonPublic))
		{
			var field = AccessTools.Field(type, "ownerPawn");
			if (field != null && field.FieldType == typeof(Pawn))
				return field;
		}

		return null;
	}
}
