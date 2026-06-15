using UnityEngine;

namespace MyFirstSubnauticaMod.Services
{
    /// <summary>
    /// Aplica modificadores locales al deslizador submarino (Seaglide):
    /// capacidad de batería (<c>Battery._capacity</c>) y velocidad máxima hacia adelante
    /// (<c>PlayerController.seaglideForwardMaxSpeed</c>, leída al entrar en MotorMode.Seaglide).
    /// Las bases se capturan una sola vez para que el % de capacidad y la suma de velocidad
    /// sean relativos al valor original y no se acumulen entre invocaciones de los Start.
    /// </summary>
    internal static class SeaglideModifiers
    {
        /// <summary>Velocidad máxima permitida tras los canjes (tope total).</summary>
        internal const float MaxForwardSpeed = 48f;

        private static bool _baseCapacityCaptured;
        private static float _baseCapacity = 100f;
        private static bool _baseSpeedCaptured;
        private static float _baseSpeed = 25f;

        /// <summary>Capacidad objetivo de batería según el bonus de porcentaje acumulado.</summary>
        internal static float GetTargetCapacity()
        {
            var percent = MyFirstSubnauticaModPlugin.SeaglideCapacityBonusPercent.Value;
            return _baseCapacity * (1f + percent / 100f);
        }

        /// <summary>Velocidad objetivo según el bonus acumulado, con tope superior <see cref="MaxForwardSpeed"/>.</summary>
        internal static float GetTargetSpeed()
        {
            var bonus = MyFirstSubnauticaModPlugin.SeaglideSpeedBonus.Value;
            return Mathf.Min(MaxForwardSpeed, _baseSpeed + bonus);
        }

        /// <summary>Aplica la capacidad de batería al deslizador indicado. Captura la base la primera vez.</summary>
        internal static bool ApplyBatteryToSeaglide(Seaglide seaglide)
        {
            if (seaglide == null)
            {
                return false;
            }

            var em = seaglide.GetComponent<EnergyMixin>();
            if (em == null || !(em.GetBattery() is Battery battery))
            {
                return false;
            }

            if (!_baseCapacityCaptured)
            {
                _baseCapacity = battery._capacity;
                _baseCapacityCaptured = true;
                MyFirstSubnauticaModPlugin.Log.LogInfo(
                    $"[LifeSync][Seaglide] Capacidad base capturada = {_baseCapacity}.");
            }

            var target = GetTargetCapacity();
            var hadFull = Mathf.Approximately(battery._charge, battery._capacity);
            battery._capacity = target;
            if (hadFull || battery._charge > target)
            {
                battery._charge = target;
            }

            MyFirstSubnauticaModPlugin.Log.LogInfo(
                $"[LifeSync][Seaglide] Capacidad aplicada ≈ {target:0.##}.");
            return true;
        }

        /// <summary>Aplica la velocidad máxima al <see cref="PlayerController"/> indicado. Captura la base la primera vez.</summary>
        internal static bool ApplySpeedToController(PlayerController controller)
        {
            if (controller == null)
            {
                return false;
            }

            if (!_baseSpeedCaptured)
            {
                _baseSpeed = controller.seaglideForwardMaxSpeed;
                _baseSpeedCaptured = true;
                MyFirstSubnauticaModPlugin.Log.LogInfo(
                    $"[LifeSync][Seaglide] Velocidad base capturada = {_baseSpeed}.");
            }

            controller.seaglideForwardMaxSpeed = GetTargetSpeed();
            MyFirstSubnauticaModPlugin.Log.LogInfo(
                $"[LifeSync][Seaglide] Velocidad aplicada = {controller.seaglideForwardMaxSpeed:0.##}.");
            return true;
        }

        /// <summary>Busca un deslizador activo y le aplica la capacidad (canje en caliente).</summary>
        internal static bool ApplyBatteryToActiveSeaglide()
        {
            return ApplyBatteryToSeaglide(Object.FindObjectOfType<Seaglide>());
        }

        /// <summary>Busca el PlayerController activo y le aplica la velocidad (canje en caliente).</summary>
        internal static bool ApplySpeedToActiveController()
        {
            return ApplySpeedToController(Object.FindObjectOfType<PlayerController>());
        }

        /// <summary>Suma +5% de capacidad y reaplica en caliente. Para canjes LifeSync.</summary>
        internal static void IncrementCapacityAndApply(int percentDelta)
        {
            MyFirstSubnauticaModPlugin.SeaglideCapacityBonusPercent.Value += percentDelta;
            MyFirstSubnauticaModPlugin.Instance?.Config.Save();
            ApplyBatteryToActiveSeaglide();
        }

        /// <summary>
        /// Suma <paramref name="speedDelta"/> a la velocidad (acumulada), clampeada para no superar el tope,
        /// y reaplica en caliente. Para canjes LifeSync.
        /// </summary>
        internal static void IncrementSpeedAndApply(float speedDelta)
        {
            var maxBonus = MaxForwardSpeed - _baseSpeed;
            var newBonus = MyFirstSubnauticaModPlugin.SeaglideSpeedBonus.Value + speedDelta;
            MyFirstSubnauticaModPlugin.SeaglideSpeedBonus.Value = Mathf.Clamp(newBonus, 0f, maxBonus);
            MyFirstSubnauticaModPlugin.Instance?.Config.Save();
            ApplySpeedToActiveController();
        }
    }
}
