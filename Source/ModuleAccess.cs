using System.Collections.Generic;
using System.Linq;
using CWF;
using RimWorld;
using Verse;
using Verse.AI;

namespace CwfStockpileModules;

/// <summary>SSOT: доступность модуля на карте (как CWF FindBestAvailableModuleFor).</summary>
public static class ModuleAccess
{
	private static List<ThingDef>? _moduleDefs;

	public static Pawn? GetWeaponOwner(Thing weapon)
	{
		return weapon.ParentHolder switch
		{
			Pawn_EquipmentTracker eq => eq.pawn,
			Pawn_InventoryTracker inv => inv.pawn,
			_ => null
		};
	}

	public static bool InventoryHasModule(Pawn pawn, ThingDef moduleDef)
	{
		return pawn.inventory.innerContainer.Any(t => t.def == moduleDef);
	}

	public static Thing? FindReachableModule(ThingDef moduleDef, Pawn pawn, IntVec3 root, Map map)
	{
		if (map == null || moduleDef == null)
			return null;

		return GenClosest.ClosestThingReachable(
			root,
			map,
			ThingRequest.ForDef(moduleDef),
			PathEndMode.ClosestTouch,
			TraverseParms.For(pawn, Danger.Deadly, TraverseMode.ByPawn, false, false, false, true),
			9999f,
			t => !t.IsForbidden(pawn) && !t.IsBurning() && pawn.CanReserve(t),
			null,
			0,
			-1,
			false,
			RegionType.Set_Passable,
			false,
			false);
	}

	/// <summary>
	/// Источник для BuildInstallOptions при owner != null:
	/// инвентарь + по одному reachable-экземпляру каждого TraitModule с карты.
	/// Дальше CWF сам фильтрует по compatibleModuleDefs и зовёт CreateInstallAction.
	/// </summary>
	public static IEnumerable<Thing> EnumerateModuleThingsForEquippedUi(Thing weapon, Pawn owner)
	{
		if (owner == null)
			yield break;

		foreach (var thing in owner.inventory.innerContainer)
			yield return thing;

		if (weapon == null)
			yield break;

		var map = weapon.MapHeld;
		if (map == null)
			yield break;

		var root = weapon.PositionHeld;
		var seenDefs = new HashSet<ThingDef>();
		foreach (var thing in owner.inventory.innerContainer)
			seenDefs.Add(thing.def);

		foreach (var moduleDef in ModuleDefs())
		{
			if (!seenDefs.Add(moduleDef))
				continue;

			var found = FindReachableModule(moduleDef, owner, root, map);
			if (found != null)
				yield return found;
		}
	}

	private static List<ThingDef> ModuleDefs()
	{
		if (_moduleDefs != null)
			return _moduleDefs;

		_moduleDefs = DefDatabase<ThingDef>.AllDefsListForReading
			.Where(d => d.GetModExtension<TraitModuleExtension>() != null)
			.ToList();
		return _moduleDefs;
	}
}
