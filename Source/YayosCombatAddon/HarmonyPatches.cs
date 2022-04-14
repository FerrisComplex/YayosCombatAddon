﻿using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using Verse;
using yayoCombat;

namespace YayosCombatAddon
{
	[StaticConstructorOnStartup]
	public static class HarmonyPatches
	{
		static HarmonyPatches()
		{
			Harmony harmony = new Harmony("syrus.yayoscombataddon");

			harmony.Patch(
				typeof(ThingComp).GetMethod("CompGetGizmosExtra"),
				postfix: new HarmonyMethod(typeof(HarmonyPatches), nameof(HarmonyPatches.ThingComp_CompGetGizmosExtra_Postfix)));

			harmony.Patch(
				typeof(patch_Pawn_TickRare).GetMethod("Postfix", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public),
				transpiler: new HarmonyMethod(typeof(HarmonyPatches), nameof(HarmonyPatches.YC_Patch_Pawn_TickRare_Transpiler)));
			harmony.Patch(
				typeof(patch_CompReloadable_UsedOnce).GetMethod("Prefix", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public),
				transpiler: new HarmonyMethod(typeof(HarmonyPatches), nameof(HarmonyPatches.YC_Patch_CompReloadable_UsedOnce_Transpiler)));
		}

		static IEnumerable<Gizmo> ThingComp_CompGetGizmosExtra_Postfix(IEnumerable<Gizmo> __result, ThingComp __instance)
		{
			if (__instance is CompReloadable reloadable && reloadable.AmmoDef.IsAmmo())
			{
				var thing = reloadable.parent;
				if (thing.Map.designationManager.DesignationOn(thing, YCA_DesignationDefOf.EjectAmmo) == null)
				{
					yield return new Command_Action
					{
						defaultLabel = "SY_YCA.EjectAmmo_label".Translate(),
						defaultDesc = "SY_YCA.EjectAmmo_desc".Translate(),
						icon = YCA_Textures.AmmoEject,
						disabled = reloadable.RemainingCharges == 0,
						disabledReason = "SY_YCA.NoEjectableAmmo".Translate(),
						action = () => thing.Map.designationManager.AddDesignation(new Designation(thing, YCA_DesignationDefOf.EjectAmmo)),
						activateSound  = YCA_SoundDefOf.Designate_EjectAmmo,
				};
				}
			}

			foreach (var gizmo in __result)
				yield return gizmo;
		}

		static IEnumerable<CodeInstruction> YC_Patch_Pawn_TickRare_Transpiler(IEnumerable<CodeInstruction> instructions)
		{
			// Fully replace original functionality with my own patch; hopefully in the most efficient way...maybe?
			yield return new CodeInstruction(OpCodes.Ldarg_0);
			yield return new CodeInstruction(OpCodes.Call, typeof(HarmonyPatches).GetMethod(nameof(Patch_Pawn_TickRare), BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public));
			yield return new CodeInstruction(OpCodes.Ret);
		}
		static IEnumerable<CodeInstruction> YC_Patch_CompReloadable_UsedOnce_Transpiler(IEnumerable<CodeInstruction> instructions)
		{
			// Fully replace original functionality with my own patch; hopefully in the most efficient way...maybe?
			yield return new CodeInstruction(OpCodes.Ldarg_0);
			yield return new CodeInstruction(OpCodes.Call, typeof(HarmonyPatches).GetMethod(nameof(Patch_CompReloadable_UsedOnce), BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public));
			yield return new CodeInstruction(OpCodes.Ret);
		}


		static void Patch_Pawn_TickRare(Pawn __instance)
		{
			if (!yayoCombat.yayoCombat.ammo) 
				return;
			if (!__instance.Drafted) 
				return;
			if (Find.TickManager.TicksGame % 60 != 0) 
				return;
			if (!(__instance.CurJobDef == JobDefOf.Wait_Combat || __instance.CurJobDef == JobDefOf.AttackStatic) || __instance.equipment == null) 
				return;


#warning TODO replace with reload all weapons?
			List<ThingWithComps> ar = __instance.equipment.AllEquipmentListForReading;

			foreach (ThingWithComps t in ar)
			{
				CompReloadable cp = t.TryGetComp<CompReloadable>();

				if (cp != null)
				{
					reloadUtility.tryAutoReload(cp);
					return;
				}
			}
		}
		static bool Patch_CompReloadable_UsedOnce(CompReloadable __instance)
		{
			if (!yayoCombat.yayoCombat.ammo) 
				return true;

			// (base) decrease number of charges
			__instance.remainingCharges--;

			// (base) destroy item if it is empty and supposed to be destroyed when empty
			if (__instance.Props.destroyOnEmpty && __instance.remainingCharges == 0 && !__instance.parent.Destroyed)
				__instance.parent.Destroy(DestroyMode.Vanish);

			// (yayo) guess it's better to make sure the wearer isn't null
			if (__instance.Wearer == null) 
				return false;


			if (__instance.RemainingCharges == 0)
			{
				if (__instance.Wearer.CurJobDef == JobDefOf.Hunt)
				{
#warning TODO try reload from inventory
					__instance.Wearer.jobs.StopAll();
				}
			}

			// (replacement) Replaced with new method
			ReloadUtility.TryAutoReload(__instance);

			return false;
		}
	}
}
