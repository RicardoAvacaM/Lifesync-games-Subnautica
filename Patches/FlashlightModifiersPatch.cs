using HarmonyLib;
using MyFirstSubnauticaMod.Services;

namespace MyFirstSubnauticaMod.Patches
{
    /// <summary>
    /// Reaplica los modificadores de la linterna (capacidad de batería y consumo) cada vez que
    /// una <see cref="FlashLight"/> arranca, ya que <c>Battery._capacity</c> y
    /// <c>ToggleLights.energyPerSecond</c> se reinicializan por instancia.
    /// </summary>
    [HarmonyPatch(typeof(FlashLight), "Start")]
    internal static class FlashlightModifiersPatch
    {
        [HarmonyPostfix]
        public static void Postfix(FlashLight __instance)
        {
            if (__instance == null)
            {
                return;
            }

            FlashlightModifiers.ApplyToFlashlight(__instance);
        }
    }
}
