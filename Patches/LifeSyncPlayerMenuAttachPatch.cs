using System.Collections;
using HarmonyLib;
using LifeSyncGamesSubnautica.Services;
using LifeSyncGamesSubnautica.UI;
using UnityEngine;

namespace LifeSyncGamesSubnautica.Patches
{
    /// <summary>
    /// Ancla el menú IMGUI al <see cref="Player"/> real (Valheim: UI ligada al jugador en escena), no al DDOL del plugin.
    /// </summary>
    [HarmonyPatch(typeof(Player), nameof(Player.Awake))]
    internal static class LifeSyncPlayerMenuAttachPatch
    {
        [HarmonyPostfix]
        public static void Postfix(Player __instance)
        {
            if (__instance == null)
            {
                return;
            }

            __instance.StartCoroutine(AttachMenuWhenMainRoutine(__instance));
        }

        private static IEnumerator AttachMenuWhenMainRoutine(Player player)
        {
            for (var i = 0; i < 120; i++)
            {
                yield return null;
                if (player == null)
                {
                    yield break;
                }

                if (Player.main == null || player != Player.main)
                {
                    continue;
                }

                if (player.GetComponent<LifeSyncLoginMenu>() != null)
                {
                    yield break;
                }

                player.gameObject.AddComponent<LifeSyncLoginMenu>();
                LifeSyncGamesSubnauticaPlugin.Log.LogInfo("[LifeSync] Menú login vinculado a Player.main (tecla en Update del jugador).");

                // Aplica los bonus persistentes (p.ej. vida/oxígeno máximos por canjes previos) sobre el Player real.
                if (!PlayerStatsApplier.ApplyMaxHealthBonus(rescaleCurrent: false))
                {
                    LifeSyncGamesSubnauticaPlugin.Log.LogWarning(
                        "[LifeSync][Stats] No se pudo aplicar el bonus de maxHealth al iniciar (LiveMixin no listo).");
                }

                if (!PlayerStatsApplier.ApplyMaxOxygenBonus(fillToFull: false))
                {
                    LifeSyncGamesSubnauticaPlugin.Log.LogWarning(
                        "[LifeSync][Stats] No se pudo aplicar el bonus de oxígeno al iniciar (Oxygen no listo).");
                }

                yield break;
            }
        }
    }
}
