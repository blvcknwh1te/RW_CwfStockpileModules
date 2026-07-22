using System.Collections.Generic;
using System.Linq;
using CWF;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace CwfStockpileModules;

/// <summary>
/// Haul модулей в инвентарь, затем установка как ModifyWeaponSelf
/// (без Goto/FailOnCannotTouch по экипированному оружию — у CWF_Haul из‑за этого срывается FinishAction).
/// </summary>
public class JobDriver_ModifyEquippedFromStockpile : JobDriver
{
	private const TargetIndex WeaponInd = TargetIndex.A;
	private const TargetIndex ModuleInd = TargetIndex.B;
	private const int TicksPerModification = 60;

	private List<ModificationData>? _modDataList;

	private Thing Weapon => job.GetTarget(WeaponInd).Thing;

	public override bool TryMakePreToilReservations(bool errorOnFailed)
	{
		// Экипированное оружие не резервируем — как Self
		if (job.targetQueueB.NullOrEmpty())
			return true;

		var reserved = job.targetQueueB
			.Where(t => pawn.Reserve(t.Thing, job, 1, -1, null, errorOnFailed))
			.ToList();
		job.targetQueueB = reserved.Any() ? reserved : null;
		return true;
	}

	public override void Notify_Starting()
	{
		base.Notify_Starting();
		_modDataList = (job.source as ModificationJobSource)?.ModDataList;
		job.source = null;
	}

	public override void ExposeData()
	{
		base.ExposeData();
		Scribe_Collections.Look(ref _modDataList, "modDataList", LookMode.Deep);
	}

	protected override IEnumerable<Toil> MakeNewToils()
	{
		if (_modDataList == null || _modDataList.Count == 0)
		{
			Log.Error("[CWF Stockpile Modules] Job started without ModDataList.");
			yield break;
		}

		if (!job.targetQueueB.NullOrEmpty())
		{
			var haulLoop = Toils_General.Label();
			yield return haulLoop;
			yield return Toils_JobTransforms.ExtractNextTargetFromQueue(ModuleInd);
			yield return Toils_Goto.GotoThing(ModuleInd, PathEndMode.ClosestTouch)
				.FailOnDespawnedNullOrForbidden(ModuleInd);
			yield return Toils_General.Do(TryCarryCurrentModule);
			yield return Toils_General.Do(MoveCarriedModuleToInventory);
			yield return Toils_Jump.JumpIfHaveTargetInQueue(ModuleInd, haulLoop);
		}

		var modDataList = _modDataList;
		var wait = Toils_General.Wait(TicksPerModification * modDataList.Count);
		wait.WithProgressBarToilDelay(WeaponInd);
		wait.AddEndCondition(() =>
			modDataList.Where(m => m.Type == ModificationType.Install)
				.Any(m => pawn.inventory.innerContainer.All(t => t.def != m.ModuleDef))
				? JobCondition.Incompletable
				: JobCondition.Ongoing);
		wait.AddFinishAction(() =>
		{
			if (ended)
				return;
			if (!Weapon.TryGetComp<CompDynamicTraits>(out var comp))
				return;

			PerformModifications(comp, modDataList);
			Messages.Message(
				"CWF_ModificationComplete".Translate(pawn.Named("PAWN"), Weapon.Named("WEAPON")),
				new LookTargets(pawn, Weapon),
				MessageTypeDefOf.PositiveEvent);
			SoundDefOf.Replant_Complete.PlayOneShot(new TargetInfo(pawn.Position, pawn.Map));
		});
		yield return wait;
	}

	private void TryCarryCurrentModule()
	{
		var thing = job.GetTarget(ModuleInd).Thing;
		if (thing != null && !thing.Destroyed && thing.stackCount > 0)
			pawn.carryTracker.TryStartCarry(thing, 1);
	}

	private void MoveCarriedModuleToInventory()
	{
		var carried = pawn.carryTracker.CarriedThing;
		if (carried != null)
			pawn.inventory.innerContainer.TryAddOrTransfer(carried);
	}

	private void PerformModifications(CompDynamicTraits comp, List<ModificationData> modList)
	{
		foreach (var item in modList.Where(md => md.Type == ModificationType.Uninstall))
		{
			comp.UninstallTrait(item.Part);
			var made = ThingMaker.MakeThing(item.ModuleDef);
			if (!pawn.inventory.innerContainer.TryAdd(made))
				GenPlace.TryPlaceThing(made, pawn.Position, pawn.Map, ThingPlaceMode.Near);
		}

		foreach (var modData in modList.Where(md => md.Type == ModificationType.Install))
		{
			var module = pawn.inventory.innerContainer.FirstOrDefault(t => t.def == modData.ModuleDef);
			if (module == null)
			{
				Log.Error($"[CWF Stockpile Modules] '{modData.ModuleDef.defName}' missing after haul.");
				continue;
			}

			comp.InstallTrait(modData.Part, modData.Trait);
			module.SplitOff(1).Destroy();
		}
	}
}
