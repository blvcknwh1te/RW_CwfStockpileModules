using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CWF;
using CWF.Controllers;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace CwfStockpileModules.Patches;

[HarmonyPatch(typeof(JobDispatcher), "DispatchFieldModificationJobs")]
internal static class Patch_DispatchFieldModificationJobs
{
	private static readonly FieldInfo WeaponField =
		AccessTools.Field(typeof(JobDispatcher), "<weapon>P");

	private static bool Prefix(JobDispatcher __instance, Pawn ownerPawn, List<ModificationData> netChanges)
	{
		var weapon = (Thing)WeaponField.GetValue(__instance);
		var map = weapon.MapHeld;
		var root = weapon.PositionHeld;
		if (map == null)
			return true;

		var toHaul = new List<Thing>();
		foreach (var change in netChanges.Where(c => c.Type == ModificationType.Install))
		{
			if (ModuleAccess.InventoryHasModule(ownerPawn, change.ModuleDef))
				continue;

			var thing = ModuleAccess.FindReachableModule(change.ModuleDef, ownerPawn, root, map);
			if (thing == null)
			{
				Messages.Message(
					"CWF_CannotFindModuleForModification".Translate(change.ModuleDef.Named("MODULE")),
					MessageTypeDefOf.RejectInput,
					false);
				return false;
			}

			toHaul.Add(thing);
		}

		// Всё уже в инвентаре — оригинальный ModifyWeaponSelf
		if (toHaul.Count == 0)
			return true;

		var job = JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("CWF_Stockpile_ModifyEquipped"), weapon);
		job.targetQueueB = toHaul.Select(t => new LocalTargetInfo(t)).ToList();
		job.source = new ModificationJobSource { ModDataList = netChanges };
		job.playerForced = true;

		ownerPawn.jobs.ClearQueuedJobs(true);
		ownerPawn.jobs.StartJob(
			job,
			JobCondition.InterruptForced,
			null,
			false,
			true,
			null,
			JobTag.Misc,
			false,
			false,
			null,
			false,
			true,
			false);

		Messages.Message(
			"CWF_ModificationJobDispatched".Translate(ownerPawn.Named("PAWN"), weapon.Named("WEAPON")),
			new LookTargets(ownerPawn, weapon),
			MessageTypeDefOf.PositiveEvent,
			true);

		return false;
	}
}
