using HarmonyLib;

namespace LifeSyncGamesSubnautica.Patches
{
    // TODO Review this file and update to your own requirements, or remove it altogether if not required

    /// <summary>
    /// Sample Harmony Patch class. Suggestion is to use one file per patched class
    /// though you can include multiple patch classes in one file.
    /// Below is included as an example, and should be replaced by classes and methods
    /// for your mod.
    /// </summary>
    [HarmonyPatch(typeof(PlayerTool))]
    internal class PlayerToolPatches
    {
        /// <summary>
        /// Patches the Player Awake method with prefix code.
        /// </summary>
        /// <param name="__instance"></param>
        [HarmonyPatch(nameof(PlayerTool.Awake))]
        [HarmonyPrefix]
        public static bool Awake_Prefix(PlayerTool __instance)
        {
            LifeSyncGamesSubnauticaPlugin.Log.LogInfo("In PlayerTool Awake method Prefix.");
            if(__instance.GetType() == typeof(Knife))
            {
                Knife knife = __instance as Knife;
                LifeSyncGamesSubnauticaPlugin.Log.LogInfo($"Knife damage Before: {knife.damage}");
                float damageMultiplier = LifeSyncGamesSubnauticaPlugin.KnifeDamageMultiplier.Value;
                int bonusDamage = LifeSyncGamesSubnauticaPlugin.KnifeBonusDamage.Value;
                knife.damage = knife.damage * damageMultiplier + bonusDamage;
                LifeSyncGamesSubnauticaPlugin.Log.LogInfo(
                    $"Knife damage After: {knife.damage} (mult={damageMultiplier}, bonus={bonusDamage})");
            }
            return true;
        }
    }
}