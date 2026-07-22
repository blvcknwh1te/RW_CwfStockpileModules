using HarmonyLib;
using Verse;

namespace CwfStockpileModules;

[StaticConstructorOnStartup]
public static class Bootstrap
{
	static Bootstrap()
	{
		new Harmony("local.cwf.stockpilemodules").PatchAll();
		Log.Message("[CWF Stockpile Modules] active: stockpile source transpiler + SpecDatabase trait-stat apply.");
	}
}
