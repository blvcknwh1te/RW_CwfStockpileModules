using System.Collections.Generic;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace CwfStockpileModules;

/// <summary>Тултип модуля в стиле CWF MainDrawer (описание + эффекты трейта).</summary>
internal static class ModuleTooltip
{
	public static TipSignal ForTrait(WeaponTraitDef trait)
	{
		var sb = new StringBuilder();
		sb.AppendLine($"<b>{trait.LabelCap}</b>");
		if (!trait.description.NullOrEmpty())
			sb.AppendLine(trait.description);

		AppendEffects(sb, trait);
		return new TipSignal(sb.ToString().TrimEnd());
	}

	private static void AppendEffects(StringBuilder sb, WeaponTraitDef trait)
	{
		if (!trait.statOffsets.NullOrEmpty())
		{
			foreach (var mod in trait.statOffsets)
			{
				if (mod?.stat == null || mod.stat == StatDefOf.MarketValue || mod.stat == StatDefOf.Mass)
					continue;
				sb.AppendLine($" - {mod.stat.LabelCap}: {mod.stat.Worker.ValueToString(mod.value, false, ToStringNumberSense.Offset)}");
			}
		}

		if (!trait.statFactors.NullOrEmpty())
		{
			foreach (var mod in trait.statFactors)
			{
				if (mod?.stat == null)
					continue;
				sb.AppendLine($" - {mod.stat.LabelCap}: {mod.stat.Worker.ValueToString(mod.value, false, ToStringNumberSense.Factor)}");
			}
		}

		if (!Mathf.Approximately(trait.burstShotCountMultiplier, 1f))
			sb.AppendLine($" - {"CWF_BurstShotCountMultiplier".Translate()}: {trait.burstShotCountMultiplier.ToStringByStyle(ToStringStyle.PercentZero, ToStringNumberSense.Factor)}");

		if (!Mathf.Approximately(trait.burstShotSpeedMultiplier, 1f))
			sb.AppendLine($" - {"CWF_BurstShotSpeedMultiplier".Translate()}: {trait.burstShotSpeedMultiplier.ToStringByStyle(ToStringStyle.PercentZero, ToStringNumberSense.Factor)}");

		if (!Mathf.Approximately(trait.additionalStoppingPower, 0f))
			sb.AppendLine($" - {"CWF_AdditionalStoppingPower".Translate()}: {trait.additionalStoppingPower.ToStringByStyle(ToStringStyle.FloatOne, ToStringNumberSense.Offset)}");

		if (!trait.equippedStatOffsets.NullOrEmpty())
		{
			foreach (var mod in trait.equippedStatOffsets)
			{
				if (mod?.stat == null)
					continue;
				sb.AppendLine($" - {mod.stat.LabelCap}: {mod.stat.ValueToString(mod.value, ToStringNumberSense.Offset, true)}");
			}
		}
	}
}
