using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Base.Core;
using Base.Levels;
using Harmony;
using Newtonsoft.Json;
using PhoenixPoint.Geoscape.Achievements;
using PhoenixPoint.Geoscape.Core;
using PhoenixPoint.Geoscape.Entities;
using PhoenixPoint.Geoscape.Levels;
using PhoenixPoint.Geoscape.Levels.Objectives;
using PhoenixPoint.Geoscape.View.ViewControllers.AugmentationScreen;
using PhoenixPoint.Tactical.Entities;
using PhoenixPoint.Tactical.Entities.Effects;
using PhoenixPoint.Tactical.Entities.Equipments;
using UnityEngine;

// ReSharper disable UnassignedField.Global 
// ReSharper disable InconsistentNaming
// ReSharper disable UnusedType.Global

namespace AcidFix
{
    public class Mod
    {
        public static Settings Settings;

        public static void Init()
        {
            var harmony = HarmonyInstance.Create("ca.gnivler.gPPFixes");
            harmony.PatchAll();
            var path = Assembly.GetExecutingAssembly().Location;
            var configFile = new FileInfo(path).DirectoryName + "/gPPFixes.json";
            Settings = JsonConvert.DeserializeObject<Settings>(File.ReadAllText(configFile));
        }
    }

    public class Settings
    {
        public bool Acid;
        public bool Regeneration;
    }

    // original method doesn't spill damage through armor, so it takes 2 turns to apply hp damage
    // so that appears in conflict with the description of Acid Damage in the game
    [HarmonyPatch(typeof(AcidDamageEffect), "AddTarget")]
    public class AcidDamageEffectAddTargetPatch
    {
        public static bool Prefix(AcidDamageEffect __instance, DamageAccumulation accum, IDamageReceiver recv, Vector3 damageOrigin, Vector3 impactForce, CastHit impactHit)
        {
            try
            {
                if (!Mod.Settings.Acid) return true;
                var currentArmor = recv.GetArmor().Value;
                var hasArmor = currentArmor > 0;
                var totalDamage = accum.Amount * accum.SourceMultiplier;
                var armorDamage = 0f;
                if (hasArmor)
                {
                    armorDamage = Math.Min(totalDamage, currentArmor);
                }


                var healthDamage = totalDamage - armorDamage;
                //FileLog.Log($"({accum.Amount} * {accum.SourceMultiplier}) hit on {recv.GetDisplayName()}\ncurrentArmor {currentArmor}\ntotalDamage {totalDamage}\narmorDamage {armorDamage}\nhealthDamage {healthDamage}");
                var data = new DamageAccumulation.TargetData
                {
                    Target = recv,
                    AmountApplied = totalDamage,
                    DamageResult = new DamageResult
                    {
                        Source = __instance.Source,
                        ArmorDamage = armorDamage,
                        HealthDamage = healthDamage,
                        ImpactForce = impactForce,
                        ImpactHit = impactHit,
                        DamageOrigin = damageOrigin,
                        DamageTypeDef = __instance.AcidDamageEffectDef.DamageTypeDef
                    }
                };
                accum.AddGeneratedTarget(data);
            }
            catch (Exception e)
            {
                FileLog.Log(e.ToString());
            }

            return false;
        }
    }

    [HarmonyPatch(typeof(GeoAchievementTracker), "OnMissionEnded")]
    public class GeoAchievementTrackerOnMissionEndedPatch
    {
        public static void Postfix(GeoMission mission)
        {
            try
            {
                if (!Mod.Settings.Regeneration) return;
                //FileLog.Log(mission.Squad.Soldiers.Count().ToString());
                foreach (var phoenixFactionSoldier in mission.Squad.Soldiers)
                {
                    // FileLog.Log($"{phoenixFactionSoldier.DisplayName}");
                    // FileLog.Log(phoenixFactionSoldier.GetBodyParts().ToArray()[0].name);
                    // Human_Torso_BodyPartDef

                    // phoenixFactionSoldier.GetTacticalAbilities().Do(x => FileLog.Log(x.name));
                    // Heavy_ClassProficiency_AbilityDef
                    // Brawler_AbilityDef
                    // WarCry_AbilityDef

                    // phoenixFactionSoldier.ArmourItems.FirstOrDefault(x => x.CommonItemData.GetDisplayName() == "Regeneration Torso")?.ItemDef.Abilities.Do(x => FileLog.Log(x.name));
                    // Regeneration_Torso_Passive_AbilityDef
                    // phoenixFactionSoldier.ArmourItems.FirstOrDefault(x => x.CommonItemData.GetDisplayName() == "Regeneration Torso")?.ItemDef.Abilities.Do(x => FileLog.Log(x.Guid));
                    // d7f6a180-f767-ed74-18f9-22a90ba2828c
                    // phoenixFactionSoldier.ArmourItems.FirstOrDefault(x => x.CommonItemData.GetDisplayName() == "Regeneration Torso")?.ItemDef.Abilities.Do(x => FileLog.Log(x.ResourcePath));
                    // Defs/Tactical/Actors/_Common/Abilities/Regeneration_Torso_Passive_AbilityDef

                    if (phoenixFactionSoldier.ArmourItems.Any(x =>
                        x.CommonItemData.ItemDef.Abilities.Any(y => y.Guid == "d7f6a180-f767-ed74-18f9-22a90ba2828c")))
                    {
                        //FileLog.Log($"Healing {phoenixFactionSoldier.DisplayName}.");
                        phoenixFactionSoldier.Heal(float.MaxValue);
                    }
                }
            }
            catch (Exception e)
            {
                FileLog.Log(e.ToString());
            }
        }
    }
}
