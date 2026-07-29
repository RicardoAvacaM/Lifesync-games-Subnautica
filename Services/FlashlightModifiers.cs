using UnityEngine;

namespace LifeSyncGamesSubnautica.Services
{
    /// <summary>
    /// Aplica modificadores locales a la linterna del jugador:
    /// capacidad de batería (<c>Battery._capacity</c>) y consumo (<c>ToggleLights.energyPerSecond</c>).
    /// Las bases se capturan una sola vez para que los porcentajes/restas sean relativos al valor original
    /// y no se acumulen entre invocaciones de <c>FlashLight.Start</c>.
    /// </summary>
    internal static class FlashlightModifiers
    {
        /// <summary>Consumo mínimo permitido (la mecánica de drenaje nunca deja el consumo por debajo de esto).</summary>
        internal const float MinDrainPerSecond = 0.2f;

        private static bool _baseCapacityCaptured;
        private static float _baseCapacity = 100f;
        private static bool _baseDrainCaptured;
        private static float _baseDrain = 1f;

        /// <summary>Capacidad objetivo de batería según el bonus de porcentaje acumulado.</summary>
        internal static float GetTargetCapacity()
        {
            var percent = LifeSyncGamesSubnauticaPlugin.FlashlightCapacityBonusPercent.Value;
            return _baseCapacity * (1f + percent / 100f);
        }

        /// <summary>Consumo objetivo (energía/seg) según la reducción acumulada, con tope inferior.</summary>
        internal static float GetTargetDrain()
        {
            var reduction = LifeSyncGamesSubnauticaPlugin.FlashlightDrainReduction.Value;
            return Mathf.Max(MinDrainPerSecond, _baseDrain - reduction);
        }

        /// <summary>
        /// Aplica capacidad y consumo a la linterna indicada. Captura las bases la primera vez.
        /// Devuelve true si pudo aplicar al menos uno de los dos.
        /// </summary>
        internal static bool ApplyToFlashlight(FlashLight flashlight)
        {
            if (flashlight == null)
            {
                return false;
            }

            var appliedSomething = false;

            var em = flashlight.GetComponent<EnergyMixin>();
            if (em != null && em.GetBattery() is Battery battery)
            {
                if (!_baseCapacityCaptured)
                {
                    _baseCapacity = battery._capacity;
                    _baseCapacityCaptured = true;
                    LifeSyncGamesSubnauticaPlugin.Log.LogInfo(
                        $"[LifeSync][Flashlight] Capacidad base capturada = {_baseCapacity}.");
                }

                var target = GetTargetCapacity();
                var hadFull = Mathf.Approximately(battery._charge, battery._capacity);
                battery._capacity = target;
                if (hadFull || battery._charge > target)
                {
                    battery._charge = target;
                }

                appliedSomething = true;
            }

            if (flashlight.toggleLights != null)
            {
                if (!_baseDrainCaptured)
                {
                    _baseDrain = flashlight.toggleLights.energyPerSecond;
                    _baseDrainCaptured = true;
                    LifeSyncGamesSubnauticaPlugin.Log.LogInfo(
                        $"[LifeSync][Flashlight] Consumo base capturado = {_baseDrain}/s.");
                }

                flashlight.toggleLights.energyPerSecond = GetTargetDrain();
                appliedSomething = true;
            }

            if (appliedSomething)
            {
                LifeSyncGamesSubnauticaPlugin.Log.LogInfo(
                    $"[LifeSync][Flashlight] Aplicado. capacity≈{GetTargetCapacity():0.##}, drain={GetTargetDrain():0.###}/s.");
            }

            return appliedSomething;
        }

        /// <summary>Busca una <see cref="FlashLight"/> activa en escena y le aplica los modificadores (canje en caliente).</summary>
        internal static bool ApplyToActiveFlashlight()
        {
            var flashlight = Object.FindObjectOfType<FlashLight>();
            if (flashlight == null)
            {
                return false;
            }

            return ApplyToFlashlight(flashlight);
        }

        /// <summary>Suma +5% de capacidad y reaplica en caliente. Para canjes LifeSync.</summary>
        internal static void IncrementCapacityAndApply(int percentDelta)
        {
            LifeSyncGamesSubnauticaPlugin.FlashlightCapacityBonusPercent.Value += percentDelta;
            LifeSyncGamesSubnauticaPlugin.Instance?.Config.Save();
            ApplyToActiveFlashlight();
        }

        /// <summary>
        /// Resta <paramref name="drainDelta"/> al consumo (acumulado), clampeado para no bajar de
        /// <see cref="MinDrainPerSecond"/>, y reaplica en caliente. Para canjes LifeSync.
        /// </summary>
        internal static void IncrementDrainReductionAndApply(float drainDelta)
        {
            var maxReduction = _baseDrain - MinDrainPerSecond;
            var newReduction = LifeSyncGamesSubnauticaPlugin.FlashlightDrainReduction.Value + drainDelta;
            LifeSyncGamesSubnauticaPlugin.FlashlightDrainReduction.Value = Mathf.Clamp(newReduction, 0f, maxReduction);
            LifeSyncGamesSubnauticaPlugin.Instance?.Config.Save();
            ApplyToActiveFlashlight();
        }
    }
}
