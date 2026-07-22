using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CWF;
using CWF.Controllers;
using HarmonyLib;
using RimWorld;
using Verse;

namespace CwfStockpileModules.Patches;

/// <summary>
/// Занятый слот: «Снять» + доступные модули на замену (inventory+склад через BuildInstallOptions).
/// Пустой слот не трогаем — штатный CWF.
/// </summary>
[HarmonyPatch(typeof(InteractionController), nameof(InteractionController.HandleSlotClick))]
internal static class Patch_HandleSlotClick
{
	private static readonly MethodInfo BuildUninstallOptionMethod =
		AccessTools.Method(typeof(InteractionController), "BuildUninstallOption",
			new[] { typeof(PartDef), typeof(List<FloatMenuOption>), typeof(WeaponTraitDef) });

	private static readonly MethodInfo BuildInstallOptionsMethod =
		AccessTools.Method(typeof(InteractionController), "BuildInstallOptions",
			new[] { typeof(PartDef), typeof(List<FloatMenuOption>) });

	private static bool Prefix(InteractionController __instance, PartDef part, WeaponTraitDef? installedTrait)
	{
		if (installedTrait == null)
			return true;

		if (BuildUninstallOptionMethod == null || BuildInstallOptionsMethod == null)
		{
			Log.Error("[CWF Stockpile Modules] HandleSlotClick: BuildUninstall/BuildInstall hooks missing.");
			return true;
		}

		var options = new List<FloatMenuOption>();
		BuildUninstallOptionMethod.Invoke(__instance, new object[] { part, options, installedTrait });
		BuildInstallOptionsMethod.Invoke(__instance, new object[] { part, options });

		var installedLabel = installedTrait.LabelCap.ToString();
		options.RemoveAll(o => o.action != null && o.Label == installedLabel);

		var emptyInventory = "CWF_NoCompatibleModulesInInventory".Translate().ToString();
		var emptyMap = "CWF_NoCompatibleModulesOnMap".Translate().ToString();
		if (options.Any(o => o.action != null))
			options.RemoveAll(o => o.action == null && (o.Label == emptyInventory || o.Label == emptyMap));

		if (options.Count > 0)
			Find.WindowStack.Add(new FloatMenu(options));

		return false;
	}
}
