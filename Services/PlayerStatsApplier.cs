using UnityEngine;

namespace MyFirstSubnauticaMod.Services
{
    /// <summary>
    /// Aplica modificadores locales a las estadísticas del jugador (vida máxima y oxígeno máximo).
    /// El asset <c>LiveMixinData</c> se clona la primera vez para no afectar a otros LiveMixin del juego.
    /// </summary>
    internal static class PlayerStatsApplier
    {
        private static bool _liveMixinDataCloned;
        private static float _originalMaxHealth;
        private static bool _originalOxygenCaptured;
        private static float _originalOxygenCapacity;

        /// <summary>
        /// Aplica el bonus actual de vida máxima (<see cref="MyFirstSubnauticaModPlugin.PlayerMaxHealthBonus"/>)
        /// sobre <c>Player.main.liveMixin.data.maxHealth</c>. Si <paramref name="rescaleCurrent"/> es true,
        /// reescala la vida actual proporcionalmente para que el porcentaje no cambie tras subir el tope.
        /// Devuelve true si pudo aplicar.
        /// </summary>
        internal static bool ApplyMaxHealthBonus(bool rescaleCurrent)
        {
            var player = Player.main;
            if (player == null)
            {
                return false;
            }

            var live = player.liveMixin;
            if (live == null || live.data == null)
            {
                return false;
            }

            if (!_liveMixinDataCloned)
            {
                _originalMaxHealth = live.data.maxHealth;
                live.data = Object.Instantiate(live.data);
                _liveMixinDataCloned = true;
                MyFirstSubnauticaModPlugin.Log.LogInfo(
                    $"[LifeSync][Stats] LiveMixinData clonado. maxHealth base = {_originalMaxHealth}.");
            }

            var bonus = MyFirstSubnauticaModPlugin.PlayerMaxHealthBonus.Value;
            var oldMax = live.data.maxHealth;
            var newMax = _originalMaxHealth + bonus;
            if (Mathf.Approximately(newMax, oldMax))
            {
                return true;
            }

            live.data.maxHealth = newMax;

            if (rescaleCurrent && oldMax > 0f)
            {
                var ratio = Mathf.Clamp01(live.health / oldMax);
                live.health = ratio * newMax;
            }

            MyFirstSubnauticaModPlugin.Log.LogInfo(
                $"[LifeSync][Stats] maxHealth: {oldMax} → {newMax} (bonus={bonus}). " +
                $"health actual = {live.health:0.##}.");
            return true;
        }

        /// <summary>
        /// Suma <paramref name="delta"/> a <see cref="MyFirstSubnauticaModPlugin.PlayerMaxHealthBonus"/>
        /// y aplica el resultado en caliente. Pensado para canjes desde el menú LifeSync.
        /// </summary>
        internal static void IncrementMaxHealthAndApply(int delta)
        {
            MyFirstSubnauticaModPlugin.PlayerMaxHealthBonus.Value += delta;
            MyFirstSubnauticaModPlugin.Instance?.Config.Save();
            ApplyMaxHealthBonus(rescaleCurrent: false);
        }

        /// <summary>
        /// Aplica el bonus actual de oxígeno máximo (<see cref="MyFirstSubnauticaModPlugin.PlayerMaxOxygenBonus"/>)
        /// sobre el componente <c>Oxygen</c> del jugador (oxygenCapacity). Si <paramref name="fillToFull"/> es true,
        /// rellena el oxígeno disponible hasta el nuevo tope. Devuelve true si pudo aplicar.
        /// </summary>
        internal static bool ApplyMaxOxygenBonus(bool fillToFull)
        {
            var player = Player.main;
            if (player == null)
            {
                return false;
            }

            var oxygen = player.GetComponent<Oxygen>() ?? player.GetComponentInChildren<Oxygen>();
            if (oxygen == null)
            {
                return false;
            }

            if (!_originalOxygenCaptured)
            {
                _originalOxygenCapacity = oxygen.oxygenCapacity;
                _originalOxygenCaptured = true;
                MyFirstSubnauticaModPlugin.Log.LogInfo(
                    $"[LifeSync][Stats] oxygenCapacity base = {_originalOxygenCapacity}.");
            }

            var bonus = MyFirstSubnauticaModPlugin.PlayerMaxOxygenBonus.Value;
            var oldCapacity = oxygen.oxygenCapacity;
            var newCapacity = _originalOxygenCapacity + bonus;
            if (Mathf.Approximately(newCapacity, oldCapacity) && !fillToFull)
            {
                return true;
            }

            oxygen.oxygenCapacity = newCapacity;
            if (fillToFull)
            {
                oxygen.oxygenAvailable = newCapacity;
            }
            else
            {
                oxygen.oxygenAvailable = Mathf.Min(oxygen.oxygenAvailable, newCapacity);
            }

            MyFirstSubnauticaModPlugin.Log.LogInfo(
                $"[LifeSync][Stats] oxygenCapacity: {oldCapacity} → {newCapacity} (bonus={bonus}). " +
                $"oxygenAvailable = {oxygen.oxygenAvailable:0.##}.");
            return true;
        }

        /// <summary>
        /// Suma <paramref name="delta"/> a <see cref="MyFirstSubnauticaModPlugin.PlayerMaxOxygenBonus"/>
        /// y aplica el resultado en caliente, rellenando el oxígeno disponible. Pensado para canjes LifeSync.
        /// </summary>
        internal static void IncrementMaxOxygenAndApply(int delta)
        {
            MyFirstSubnauticaModPlugin.PlayerMaxOxygenBonus.Value += delta;
            MyFirstSubnauticaModPlugin.Instance?.Config.Save();
            ApplyMaxOxygenBonus(fillToFull: true);
        }

        /// <summary>Cura la barra de vida del jugador al máximo actual (<c>liveMixin.ResetHealth()</c>).</summary>
        internal static bool HealToFull()
        {
            var player = Player.main;
            if (player == null || player.liveMixin == null)
            {
                return false;
            }

            player.liveMixin.ResetHealth();
            MyFirstSubnauticaModPlugin.Log.LogInfo(
                $"[LifeSync][Stats] Vida curada al máximo. health = {player.liveMixin.health:0.##}.");
            return true;
        }

        /// <summary>Rellena el oxígeno del jugador hasta la capacidad total (<c>oxygenMgr.Restore()</c>).</summary>
        internal static bool RestoreOxygenToFull()
        {
            var player = Player.main;
            if (player == null || player.oxygenMgr == null)
            {
                return false;
            }

            player.oxygenMgr.Restore();
            MyFirstSubnauticaModPlugin.Log.LogInfo("[LifeSync][Stats] Oxígeno rellenado al máximo.");
            return true;
        }
    }
}
