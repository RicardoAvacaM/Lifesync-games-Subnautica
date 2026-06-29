using System;
using System.Globalization;
using System.IO;
using System.Text;
using BepInEx;
using UnityEngine;

namespace MyFirstSubnauticaMod.Services
{
    /// <summary>
    /// Cada 30 s (tiempo real), si hay sesión LifeSync (Bearer token), escribe una fila CSV
    /// con estadísticas del jugador y bonus del mod en <c>BepInEx/plugins/MyFirstSubnauticaMod/logger/</c>.
    /// </summary>
    internal sealed class PlayerStatsCsvLogger : MonoBehaviour
    {
        private const float SampleIntervalSeconds = 30f;

        private static readonly string[] CsvHeader =
        {
            "utc_time",
            "game_time",
            "player_id",
            "health",
            "health_max",
            "health_bonus_cfg",
            "oxygen",
            "oxygen_max",
            "oxygen_bonus_cfg",
            "food",
            "water",
            "knife_damage_multiplier_cfg",
            "knife_bonus_damage_cfg",
            "knife_damage_held",
            "knife_damage_inventory_max",
            "flashlight_capacity_bonus_pct_cfg",
            "flashlight_drain_reduction_cfg",
            "flashlight_capacity_target",
            "flashlight_drain_target",
            "flashlight_battery_charge",
            "flashlight_battery_capacity",
            "seaglide_capacity_bonus_pct_cfg",
            "seaglide_speed_bonus_cfg",
            "seaglide_capacity_target",
            "seaglide_speed_target",
            "seaglide_battery_charge",
            "seaglide_battery_capacity",
            "pos_x",
            "pos_y",
            "pos_z",
            "depth"
        };

        private string _csvPath;
        private bool _headerWritten;

        /// <summary>Ancla el logger al <see cref="Player"/> real (el host DDOL del plugin no sobrevive al cargar partida).</summary>
        internal static void EnsureOnPlayer(Player player)
        {
            if (player == null || player.GetComponent<PlayerStatsCsvLogger>() != null)
            {
                return;
            }

            player.gameObject.AddComponent<PlayerStatsCsvLogger>();
            MyFirstSubnauticaModPlugin.Log.LogInfo("[LifeSync][Logger] Vinculado a Player.main.");
        }

        private void Start()
        {
            StartCoroutine(LoggerRoutine());
        }

        private System.Collections.IEnumerator LoggerRoutine()
        {
            yield return null;
            TryWriteSample();

            var wait = new WaitForSecondsRealtime(SampleIntervalSeconds);
            while (true)
            {
                yield return wait;
                TryWriteSample();
            }
        }

        private static bool IsLifeSyncLoggedIn()
        {
            return !string.IsNullOrWhiteSpace(MyFirstSubnauticaModPlugin.LifeSyncApiBearerToken.Value);
        }

        private static bool _loggedSkipNoAuth;

        private void TryWriteSample()
        {
            if (!IsLifeSyncLoggedIn())
            {
                if (!_loggedSkipNoAuth)
                {
                    _loggedSkipNoAuth = true;
                    MyFirstSubnauticaModPlugin.Log.LogInfo(
                        "[LifeSync][Logger] Sin sesión LifeSync; no se escribe CSV hasta iniciar sesión (F10).");
                }

                return;
            }

            _loggedSkipNoAuth = false;

            var player = Player.main;
            if (player == null)
            {
                return;
            }

            try
            {
                EnsureCsvReady();
                var row = BuildRow(player);
                File.AppendAllText(_csvPath, row + Environment.NewLine, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                MyFirstSubnauticaModPlugin.Log.LogWarning($"[LifeSync][Logger] No se pudo escribir CSV: {ex.Message}");
            }
        }

        private void EnsureCsvReady()
        {
            if (_headerWritten && !string.IsNullOrEmpty(_csvPath))
            {
                return;
            }

            var loggerDir = Path.Combine(Paths.PluginPath, "MyFirstSubnauticaMod", "logger");
            Directory.CreateDirectory(loggerDir);

            var stamp = DateTime.UtcNow.ToString("yyyy-MM-dd_HHmmss", CultureInfo.InvariantCulture);
            _csvPath = Path.Combine(loggerDir, $"stats_{stamp}.csv");
            File.WriteAllText(_csvPath, string.Join(",", CsvHeader) + Environment.NewLine, Encoding.UTF8);
            _headerWritten = true;

            MyFirstSubnauticaModPlugin.Log.LogInfo($"[LifeSync][Logger] CSV activo: {_csvPath}");
        }

        private static string BuildRow(Player player)
        {
            var live = player.liveMixin;
            var survival = player.GetComponent<Survival>();

            float health = live != null ? live.health : 0f;
            float healthMax = live != null ? live.maxHealth : 0f;

            float oxygenValue = 0f;
            float oxygenMax = 0f;
            var oxygenComponent = player.GetComponent<Oxygen>() ?? player.GetComponentInChildren<Oxygen>();
            if (oxygenComponent != null)
            {
                oxygenValue = oxygenComponent.oxygenAvailable;
                oxygenMax = oxygenComponent.oxygenCapacity;
            }

            float food = survival != null ? survival.food : 0f;
            float water = survival != null ? survival.water : 0f;

            GetKnifeStats(out var knifeHeld, out var knifeInventoryMax);

            GetFlashlightBatteryStats(out var flCharge, out var flCapacity);
            GetSeaglideBatteryStats(out var sgCharge, out var sgCapacity);

            float seaglideSpeedTarget = 0f;
            var controller = player.GetComponent<PlayerController>();
            if (controller != null)
            {
                seaglideSpeedTarget = controller.seaglideForwardMaxSpeed;
            }
            else
            {
                seaglideSpeedTarget = SeaglideModifiers.GetTargetSpeed();
            }

            var pos = player.transform.position;

            var values = new[]
            {
                FormatUtcNow(),
                FormatFloat(Time.timeSinceLevelLoad),
                MyFirstSubnauticaModPlugin.LifeSyncCachedPlayerId.Value.ToString(CultureInfo.InvariantCulture),
                FormatFloat(health),
                FormatFloat(healthMax),
                MyFirstSubnauticaModPlugin.PlayerMaxHealthBonus.Value.ToString(CultureInfo.InvariantCulture),
                FormatFloat(oxygenValue),
                FormatFloat(oxygenMax),
                MyFirstSubnauticaModPlugin.PlayerMaxOxygenBonus.Value.ToString(CultureInfo.InvariantCulture),
                FormatFloat(food),
                FormatFloat(water),
                FormatFloat(MyFirstSubnauticaModPlugin.KnifeDamageMultiplier.Value),
                MyFirstSubnauticaModPlugin.KnifeBonusDamage.Value.ToString(CultureInfo.InvariantCulture),
                FormatFloat(knifeHeld),
                FormatFloat(knifeInventoryMax),
                MyFirstSubnauticaModPlugin.FlashlightCapacityBonusPercent.Value.ToString(CultureInfo.InvariantCulture),
                FormatFloat(MyFirstSubnauticaModPlugin.FlashlightDrainReduction.Value),
                FormatFloat(FlashlightModifiers.GetTargetCapacity()),
                FormatFloat(FlashlightModifiers.GetTargetDrain()),
                FormatFloat(flCharge),
                FormatFloat(flCapacity),
                MyFirstSubnauticaModPlugin.SeaglideCapacityBonusPercent.Value.ToString(CultureInfo.InvariantCulture),
                FormatFloat(MyFirstSubnauticaModPlugin.SeaglideSpeedBonus.Value),
                FormatFloat(SeaglideModifiers.GetTargetCapacity()),
                FormatFloat(seaglideSpeedTarget),
                FormatFloat(sgCharge),
                FormatFloat(sgCapacity),
                FormatFloat(pos.x),
                FormatFloat(pos.y),
                FormatFloat(pos.z),
                FormatFloat(Mathf.Max(0f, -pos.y))
            };

            return string.Join(",", values);
        }

        private static void GetKnifeStats(out float heldDamage, out float inventoryMaxDamage)
        {
            heldDamage = 0f;
            inventoryMaxDamage = 0f;

            var inventory = Inventory.main;
            if (inventory == null || inventory.container == null)
            {
                return;
            }

            var held = inventory.GetHeld();
            if (held != null)
            {
                var heldKnife = held.GetComponent<Knife>();
                if (heldKnife != null)
                {
                    heldDamage = heldKnife.damage;
                }
            }

            foreach (var item in inventory.container)
            {
                if (item == null || item.item == null)
                {
                    continue;
                }

                var knife = item.item.GetComponent<Knife>();
                if (knife == null)
                {
                    continue;
                }

                if (knife.damage > inventoryMaxDamage)
                {
                    inventoryMaxDamage = knife.damage;
                }
            }
        }

        private static void GetFlashlightBatteryStats(out float charge, out float capacity)
        {
            charge = 0f;
            capacity = 0f;

            var flashlight = UnityEngine.Object.FindObjectOfType<FlashLight>();
            if (flashlight == null)
            {
                return;
            }

            var em = flashlight.GetComponent<EnergyMixin>();
            if (em == null || !(em.GetBattery() is Battery battery))
            {
                return;
            }

            charge = battery._charge;
            capacity = battery._capacity;
        }

        private static void GetSeaglideBatteryStats(out float charge, out float capacity)
        {
            charge = 0f;
            capacity = 0f;

            var seaglide = UnityEngine.Object.FindObjectOfType<Seaglide>();
            if (seaglide == null)
            {
                return;
            }

            var em = seaglide.GetComponent<EnergyMixin>();
            if (em == null || !(em.GetBattery() is Battery battery))
            {
                return;
            }

            charge = battery._charge;
            capacity = battery._capacity;
        }

        private static string FormatUtcNow()
        {
            return DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss'Z'", CultureInfo.InvariantCulture);
        }

        private static string FormatFloat(float value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }
    }
}
