using System.Collections.Generic;
using System.Linq;
using CWF.Controllers;
using HarmonyLib;
using RimWorld;
using Verse;

namespace CwfStockpileModules.Patches;

/// <summary>Тултипы на пунктах установки модуля (наведение в FloatMenu).</summary>
[HarmonyPatch(typeof(InteractionController), "BuildInstallOptions")]
internal static class Patch_BuildInstallOptions_Tooltips
{
	private static void Postfix(List<FloatMenuOption> options)
	{
		if (options == null || options.Count == 0)
			return;

		foreach (var option in options)
		{
			if (option.action == null || option.tooltip.HasValue)
				continue;

			var trait = FindTraitByLabel(option.Label);
			if (trait != null)
				option.tooltip = ModuleTooltip.ForTrait(trait);
		}
	}

	private static WeaponTraitDef? FindTraitByLabel(string label)
	{
		return DefDatabase<WeaponTraitDef>.AllDefsListForReading
			.FirstOrDefault(t => t.LabelCap.ToString() == label);
	}
}

/// <summary>Тултип на пункте «Снять».</summary>
[HarmonyPatch(typeof(InteractionController), "BuildUninstallOption")]
internal static class Patch_BuildUninstallOption_Tooltip
{
	private static void Postfix(List<FloatMenuOption> options, WeaponTraitDef installedTrait)
	{
		if (options == null || installedTrait == null)
			return;

		for (var i = options.Count - 1; i >= 0; i--)
		{
			var option = options[i];
			if (option.action == null || option.tooltip.HasValue)
				continue;

			option.tooltip = ModuleTooltip.ForTrait(installedTrait);
			break;
		}
	}
}
