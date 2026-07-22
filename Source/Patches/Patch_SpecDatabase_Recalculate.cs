using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CWF;
using HarmonyLib;
using RimWorld;
using Verse;

namespace CwfStockpileModules.Patches;

/// <summary>
/// GetStatValue на превью-оружии не включает CompDynamicTraits.GetStatOffset
/// (burst при этом живой — Verb читает Traits напрямую). После штатного Recalculate
/// пересчитываем статы сайдбара: bare (без наших offsets/factors) + трейты вручную.
/// </summary>
[HarmonyPatch(typeof(SpecDatabase), nameof(SpecDatabase.Recalculate))]
internal static class Patch_SpecDatabase_Recalculate
{
	[ThreadStatic]
	private static bool _suppressDynamicTraitStats;

	[ThreadStatic]
	private static bool _inPostfix;

	private static readonly FieldInfo? PreviewWeaponField =
		AccessTools.Field(typeof(SpecDatabase), "_previewWeapon");

	private static readonly FieldInfo? PreviewTraitsField =
		AccessTools.Field(typeof(SpecDatabase), "_previewDynamicTraits");

	private static readonly MethodInfo? ClearAllCachesMethod =
		AccessTools.Method(typeof(CompDynamicTraits), "ClearAllCaches");

	private static readonly MethodInfo? CalculateDpsMethod =
		AccessTools.Method(typeof(SpecDatabase), "CalculateDps");

	private static readonly Type? ModeType =
		typeof(SpecDatabase).GetNestedType("Mode", BindingFlags.Public | BindingFlags.NonPublic);

	private static void Prefix(SpecDatabase __instance)
	{
		ClearPreviewCaches(__instance);
	}

	private static void Postfix(SpecDatabase __instance)
	{
		if (_inPostfix)
			return;

		if (PreviewWeaponField == null || PreviewTraitsField == null)
			return;

		var weapon = PreviewWeaponField.GetValue(__instance) as Thing;
		var comp = PreviewTraitsField.GetValue(__instance) as CompDynamicTraits;
		if (weapon == null || comp == null || comp.Traits.Count == 0)
			return;

		_inPostfix = true;
		try
		{
			ApplyStat(__instance, weapon, comp, "Mass", StatDefOf.Mass);
			ApplyStat(__instance, weapon, comp, "MarketValue", StatDefOf.MarketValue);
			ApplyStat(__instance, weapon, comp, "Cooldown", StatDefOf.RangedWeapon_Cooldown);
			ApplyStat(__instance, weapon, comp, "AccuracyTouch", StatDefOf.AccuracyTouch);
			ApplyStat(__instance, weapon, comp, "AccuracyShort", StatDefOf.AccuracyShort);
			ApplyStat(__instance, weapon, comp, "AccuracyMedium", StatDefOf.AccuracyMedium);
			ApplyStat(__instance, weapon, comp, "AccuracyLong", StatDefOf.AccuracyLong);
			ApplyScaledFromRaw(__instance, weapon, comp, "Range", StatDefOf.RangedWeapon_RangeMultiplier);
			ApplyScaledFromRaw(__instance, weapon, comp, "WarmupTime", StatDefOf.RangedWeapon_WarmupMultiplier);
			RefreshDps(__instance);
		}
		finally
		{
			_inPostfix = false;
		}
	}

	internal static bool SuppressDynamicTraitStats => _suppressDynamicTraitStats;

	private static void ApplyStat(
		SpecDatabase db, Thing weapon, CompDynamicTraits comp, string fieldName, StatDef stat)
	{
		var bare = BareStat(weapon, comp, stat);
		SetDynamic(db, fieldName, (bare + SumOffsets(comp.Traits, stat)) * ProductFactors(comp.Traits, stat));
	}

	private static void ApplyScaledFromRaw(
		SpecDatabase db, Thing weapon, CompDynamicTraits comp, string fieldName, StatDef multiplierStat)
	{
		var raw = GetRaw(db, fieldName);
		var bareMult = BareStat(weapon, comp, multiplierStat);
		var mult = (bareMult + SumOffsets(comp.Traits, multiplierStat))
		           * ProductFactors(comp.Traits, multiplierStat);
		SetDynamic(db, fieldName, raw * mult);
	}

	private static float BareStat(Thing weapon, CompDynamicTraits comp, StatDef stat)
	{
		_suppressDynamicTraitStats = true;
		try
		{
			ClearAllCachesMethod?.Invoke(comp, null);
			return weapon.GetStatValue(stat, true, -1);
		}
		finally
		{
			_suppressDynamicTraitStats = false;
		}
	}

	private static void ClearPreviewCaches(SpecDatabase db)
	{
		if (ClearAllCachesMethod == null || PreviewTraitsField == null)
			return;

		var comp = PreviewTraitsField.GetValue(db);
		if (comp != null)
			ClearAllCachesMethod.Invoke(comp, null);
	}

	private static void RefreshDps(SpecDatabase db)
	{
		if (CalculateDpsMethod == null || ModeType == null)
			return;

		var dynamicMode = Enum.Parse(ModeType, "Dynamic");
		var dps = CalculateDpsMethod.Invoke(db, new[] { dynamicMode });
		if (dps is float value)
			SetDynamic(db, "Dps", value);
	}

	private static float SumOffsets(IReadOnlyCollection<WeaponTraitDef> traits, StatDef stat)
	{
		var sum = 0f;
		foreach (var trait in traits)
		{
			var offsets = trait.statOffsets;
			if (offsets == null)
				continue;

			foreach (var mod in offsets)
			{
				if (mod?.stat == stat)
					sum += mod.value;
			}
		}

		return sum;
	}

	private static float ProductFactors(IReadOnlyCollection<WeaponTraitDef> traits, StatDef stat)
	{
		var product = 1f;
		foreach (var trait in traits)
		{
			var factors = trait.statFactors;
			if (factors == null)
				continue;

			foreach (var mod in factors)
			{
				if (mod?.stat == stat)
					product *= mod.value;
			}
		}

		return product;
	}

	private static readonly Type? SpecType = AccessTools.Inner(typeof(SpecDatabase), "Spec")
	                                             ?? AccessTools.TypeByName("CWF.Spec");

	private static float GetRaw(SpecDatabase db, string fieldName)
	{
		var field = AccessTools.Field(typeof(SpecDatabase), fieldName);
		var spec = field?.GetValue(db);
		if (spec == null || SpecType == null)
			return 0f;

		var rawField = AccessTools.Field(SpecType, "Raw");
		return rawField != null ? (float)rawField.GetValue(spec) : 0f;
	}

	private static void SetDynamic(SpecDatabase db, string fieldName, float value)
	{
		var field = AccessTools.Field(typeof(SpecDatabase), fieldName);
		if (field == null || SpecType == null)
			return;

		var boxed = field.GetValue(db);
		if (boxed == null)
			return;

		var dynamicField = AccessTools.Field(SpecType, "Dynamic");
		if (dynamicField == null)
			return;

		dynamicField.SetValue(boxed, value);
		field.SetValue(db, boxed);
	}
}

[HarmonyPatch(typeof(CompDynamicTraits), nameof(CompDynamicTraits.GetStatOffset))]
internal static class Patch_CompDynamicTraits_GetStatOffset
{
	private static bool Prefix(ref float __result)
	{
		if (!Patch_SpecDatabase_Recalculate.SuppressDynamicTraitStats)
			return true;

		__result = 0f;
		return false;
	}
}

[HarmonyPatch(typeof(CompDynamicTraits), nameof(CompDynamicTraits.GetStatFactor))]
internal static class Patch_CompDynamicTraits_GetStatFactor
{
	private static bool Prefix(ref float __result)
	{
		if (!Patch_SpecDatabase_Recalculate.SuppressDynamicTraitStats)
			return true;

		__result = 1f;
		return false;
	}
}
