using UnityEngine;

namespace LifeSyncGamesSubnautica.Services
{
    /// <summary>
    /// Aplica modificadores locales a las estadísticas del jugador (vida máxima y oxígeno máximo).
    /// El asset <c>LiveMixinData</c> se clona la primera vez para no afectar a otros LiveMixin del juego.
    /// </summary>
    internal static class PlayerStatsApplier
    {
        internal const int MinEffectiveMaxHealth = 30;
        internal const int MinEffectiveMaxOxygen = 20;

        private static bool _liveMixinDataCloned;
        private static float _originalMaxHealth;
        private static bool _originalOxygenCaptured;
        private static float _originalOxygenCapacity;

        /// <summary>
        /// Aplica el bonus actual de vida máxima (<see cref="LifeSyncGamesSubnauticaPlugin.PlayerMaxHealthBonus"/>)
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
                LifeSyncGamesSubnauticaPlugin.Log.LogInfo(
                    $"[LifeSync][Stats] LiveMixinData clonado. maxHealth base = {_originalMaxHealth}.");
            }

            var bonus = LifeSyncGamesSubnauticaPlugin.PlayerMaxHealthBonus.Value;
            var penalty = GetHealthPenalty();
            var oldMax = live.data.maxHealth;
            var newMax = ComputeEffectiveMaxHealth(_originalMaxHealth, bonus, penalty);
            if (Mathf.Approximately(newMax, oldMax))
            {
                return true;
            }

            live.data.maxHealth = newMax;

            if (live.health > newMax)
            {
                live.health = newMax;
            }

            if (rescaleCurrent && oldMax > 0f)
            {
                var ratio = Mathf.Clamp01(live.health / oldMax);
                live.health = ratio * newMax;
            }

            LifeSyncGamesSubnauticaPlugin.Log.LogInfo(
                $"[LifeSync][Stats] maxHealth: {oldMax} → {newMax} (bonus={bonus}, penalty={penalty}). " +
                $"health actual = {live.health:0.##}.");
            return true;
        }

        internal static int GetHealthPenalty()
        {
            return LifeSyncGamesSubnauticaPlugin.PlayerMaxHealthPenalty?.Value ?? 0;
        }

        internal static int GetOxygenPenalty()
        {
            return LifeSyncGamesSubnauticaPlugin.PlayerMaxOxygenPenalty?.Value ?? 0;
        }

        internal static float ComputeEffectiveMaxHealth(float originalBase, int bonus, int penalty)
        {
            return Mathf.Max(MinEffectiveMaxHealth, originalBase + bonus - penalty);
        }

        internal static float ComputeEffectiveMaxOxygen(float originalBase, int bonus, int penalty)
        {
            return Mathf.Max(MinEffectiveMaxOxygen, originalBase + bonus - penalty);
        }

        /// <summary>
        /// Resta <paramref name="amount"/> a vida y oxígeno máx. permanentes (cfg), respetando pisos 30/20.
        /// </summary>
        internal static bool TryApplyFatiguePenalty(int amount)
        {
            if (amount <= 0)
            {
                return false;
            }

            var player = Player.main;
            if (player == null)
            {
                return false;
            }

            ApplyMaxHealthBonus(rescaleCurrent: false);
            ApplyMaxOxygenBonus(fillToFull: false);

            var live = player.liveMixin;
            var oxygen = player.GetComponent<Oxygen>() ?? player.GetComponentInChildren<Oxygen>();
            if (live == null || live.data == null || oxygen == null)
            {
                return false;
            }

            var healthBonus = LifeSyncGamesSubnauticaPlugin.PlayerMaxHealthBonus.Value;
            var oxygenBonus = LifeSyncGamesSubnauticaPlugin.PlayerMaxOxygenBonus.Value;
            var healthPenalty = GetHealthPenalty();
            var oxygenPenalty = GetOxygenPenalty();

            var currentHealthMax = live.data.maxHealth;
            var currentOxygenMax = oxygen.oxygenCapacity;
            var targetHealthMax = Mathf.Max(MinEffectiveMaxHealth, currentHealthMax - amount);
            var targetOxygenMax = Mathf.Max(MinEffectiveMaxOxygen, currentOxygenMax - amount);

            if (Mathf.Approximately(targetHealthMax, currentHealthMax) &&
                Mathf.Approximately(targetOxygenMax, currentOxygenMax))
            {
                return false;
            }

            var newHealthPenalty = healthPenalty + (currentHealthMax - targetHealthMax);
            var newOxygenPenalty = oxygenPenalty + (currentOxygenMax - targetOxygenMax);

            LifeSyncGamesSubnauticaPlugin.PlayerMaxHealthPenalty.Value = Mathf.RoundToInt(newHealthPenalty);
            LifeSyncGamesSubnauticaPlugin.PlayerMaxOxygenPenalty.Value = Mathf.RoundToInt(newOxygenPenalty);
            LifeSyncGamesSubnauticaPlugin.Instance?.Config.Save();

            ApplyMaxHealthBonus(rescaleCurrent: false);
            ApplyMaxOxygenBonus(fillToFull: false);

            LifeSyncGamesSubnauticaPlugin.Log.LogInfo(
                $"[LifeSync][Fatigue] Máximos: vida {currentHealthMax:0.##}→{live.data.maxHealth:0.##}, " +
                $"oxígeno {currentOxygenMax:0.##}→{oxygen.oxygenCapacity:0.##} " +
                $"(penalty vida={LifeSyncGamesSubnauticaPlugin.PlayerMaxHealthPenalty.Value}, " +
                $"oxígeno={LifeSyncGamesSubnauticaPlugin.PlayerMaxOxygenPenalty.Value}).");

            return true;
        }

        /// <summary>
        /// Suma <paramref name="delta"/> a <see cref="LifeSyncGamesSubnauticaPlugin.PlayerMaxHealthBonus"/>
        /// y aplica el resultado en caliente. Pensado para canjes desde el menú LifeSync.
        /// </summary>
        internal static void IncrementMaxHealthAndApply(int delta)
        {
            LifeSyncGamesSubnauticaPlugin.PlayerMaxHealthBonus.Value += delta;
            LifeSyncGamesSubnauticaPlugin.Instance?.Config.Save();
            ApplyMaxHealthBonus(rescaleCurrent: false);
        }

        /// <summary>
        /// Aplica el bonus actual de oxígeno máximo (<see cref="LifeSyncGamesSubnauticaPlugin.PlayerMaxOxygenBonus"/>)
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
                LifeSyncGamesSubnauticaPlugin.Log.LogInfo(
                    $"[LifeSync][Stats] oxygenCapacity base = {_originalOxygenCapacity}.");
            }

            var bonus = LifeSyncGamesSubnauticaPlugin.PlayerMaxOxygenBonus.Value;
            var penalty = GetOxygenPenalty();
            var oldCapacity = oxygen.oxygenCapacity;
            var newCapacity = ComputeEffectiveMaxOxygen(_originalOxygenCapacity, bonus, penalty);
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

            LifeSyncGamesSubnauticaPlugin.Log.LogInfo(
                $"[LifeSync][Stats] oxygenCapacity: {oldCapacity} → {newCapacity} (bonus={bonus}, penalty={penalty}). " +
                $"oxygenAvailable = {oxygen.oxygenAvailable:0.##}.");
            return true;
        }

        /// <summary>
        /// Suma <paramref name="delta"/> a <see cref="LifeSyncGamesSubnauticaPlugin.PlayerMaxOxygenBonus"/>
        /// y aplica el resultado en caliente, rellenando el oxígeno disponible. Pensado para canjes LifeSync.
        /// </summary>
        internal static void IncrementMaxOxygenAndApply(int delta)
        {
            LifeSyncGamesSubnauticaPlugin.PlayerMaxOxygenBonus.Value += delta;
            LifeSyncGamesSubnauticaPlugin.Instance?.Config.Save();
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
            LifeSyncGamesSubnauticaPlugin.Log.LogInfo(
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
            LifeSyncGamesSubnauticaPlugin.Log.LogInfo("[LifeSync][Stats] Oxígeno rellenado al máximo.");
            return true;
        }
    }
}
