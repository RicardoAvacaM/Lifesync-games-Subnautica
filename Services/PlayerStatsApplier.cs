using UnityEngine;

namespace MyFirstSubnauticaMod.Services
{
    /// <summary>
    /// Aplica modificadores locales a las estadísticas del jugador (vida máxima por ahora).
    /// El asset <c>LiveMixinData</c> se clona la primera vez para no afectar a otros LiveMixin del juego.
    /// </summary>
    internal static class PlayerStatsApplier
    {
        private static bool _liveMixinDataCloned;
        private static float _originalMaxHealth;

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
    }
}
