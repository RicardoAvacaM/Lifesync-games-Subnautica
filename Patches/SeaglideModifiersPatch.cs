using HarmonyLib;
using MyFirstSubnauticaMod.Services;

namespace MyFirstSubnauticaMod.Patches
{
    /// <summary>
    /// Reaplica la capacidad de batería del deslizador cada vez que un <see cref="Seaglide"/> arranca,
    /// ya que <c>Battery._capacity</c> es por instancia y se reinicializa.
    /// </summary>
    [HarmonyPatch(typeof(Seaglide), "Start")]
    internal static class SeaglideBatteryPatch
    {
        [HarmonyPostfix]
        public static void Postfix(Seaglide __instance)
        {
            if (__instance == null)
            {
                return;
            }

            SeaglideModifiers.ApplyBatteryToSeaglide(__instance);
        }
    }

    /// <summary>
    /// Reaplica la velocidad máxima del deslizador sobre <c>PlayerController.seaglideForwardMaxSpeed</c>
    /// al arrancar el controlador del jugador (el valor se lee al entrar en MotorMode.Seaglide).
    /// </summary>
    [HarmonyPatch(typeof(PlayerController), "Start")]
    internal static class SeaglideSpeedPatch
    {
        [HarmonyPostfix]
        public static void Postfix(PlayerController __instance)
        {
            if (__instance == null)
            {
                return;
            }

            SeaglideModifiers.ApplySpeedToController(__instance);
        }
    }
}
