using System.Globalization;
using System.Text;
using UnityEngine;

namespace MyFirstSubnauticaMod.Services
{
    /// <summary>Instantánea de estadísticas del jugador (mismas columnas que el CSV local legacy).</summary>
    internal struct PlayerStatsSnapshot
    {
        internal static readonly string[] CsvHeader =
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
            "depth",
            "game_mode",
            "save_slot",
            "biome",
            "motor_mode",
            "player_mode",
            "is_underwater",
            "is_swimming",
            "is_in_sub",
            "is_inside_walkable",
            "in_seamoth",
            "in_exosuit",
            "is_piloting",
            "vehicle_context",
            "game_depth",
            "depth_level",
            "max_depth",
            "water_temperature",
            "radiation_amount",
            "infection_amount",
            "infection_revealed",
            "is_day",
            "day_night_time",
            "session_time_played",
            "inventory_item_count",
            "inventory_capacity",
            "stomach"
        };

        public string UtcTime;
        public float GameTime;
        public int PlayerId;
        public float Health;
        public float HealthMax;
        public int HealthBonusCfg;
        public float Oxygen;
        public float OxygenMax;
        public int OxygenBonusCfg;
        public float Food;
        public float Water;
        public float KnifeDamageMultiplierCfg;
        public int KnifeBonusDamageCfg;
        public float KnifeDamageHeld;
        public float KnifeDamageInventoryMax;
        public int FlashlightCapacityBonusPctCfg;
        public float FlashlightDrainReductionCfg;
        public float FlashlightCapacityTarget;
        public float FlashlightDrainTarget;
        public float FlashlightBatteryCharge;
        public float FlashlightBatteryCapacity;
        public int SeaglideCapacityBonusPctCfg;
        public float SeaglideSpeedBonusCfg;
        public float SeaglideCapacityTarget;
        public float SeaglideSpeedTarget;
        public float SeaglideBatteryCharge;
        public float SeaglideBatteryCapacity;
        public float PosX;
        public float PosY;
        public float PosZ;
        public float Depth;
        public string GameMode;
        public string SaveSlot;
        public string Biome;
        public string MotorMode;
        public string PlayerMode;
        public bool IsUnderwater;
        public bool IsSwimming;
        public bool IsInSub;
        public bool IsInsideWalkable;
        public bool InSeamoth;
        public bool InExosuit;
        public bool IsPiloting;
        public string VehicleContext;
        public float GameDepth;
        public float DepthLevel;
        public float MaxDepth;
        public float WaterTemperature;
        public float RadiationAmount;
        public float InfectionAmount;
        public bool InfectionRevealed;
        public bool IsDay;
        public float DayNightTime;
        public float SessionTimePlayed;
        public int InventoryItemCount;
        public int InventoryCapacity;
        public float Stomach;

        internal static bool TryCapture(Player player, out PlayerStatsSnapshot snapshot)
        {
            snapshot = default;
            if (player == null)
            {
                return false;
            }

            var live = player.liveMixin;
            var survival = player.GetComponent<Survival>();

            float oxygenValue = 0f;
            float oxygenMax = 0f;
            var oxygenComponent = player.GetComponent<Oxygen>() ?? player.GetComponentInChildren<Oxygen>();
            if (oxygenComponent != null)
            {
                oxygenValue = oxygenComponent.oxygenAvailable;
                oxygenMax = oxygenComponent.oxygenCapacity;
            }

            GetKnifeStats(out var knifeHeld, out var knifeInventoryMax);
            GetFlashlightBatteryStats(out var flCharge, out var flCapacity);
            GetSeaglideBatteryStats(out var sgCharge, out var sgCapacity);

            float seaglideSpeedTarget;
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
            GetInventoryStats(out var inventoryItemCount, out var inventoryCapacity);

            float waterTemperature = 0f;
            var isDay = false;
            float dayNightTime = 0f;
            float sessionTimePlayed = 0f;
            var saveSlot = string.Empty;
            var gameMode = string.Empty;
            try
            {
                GetEnvironmentStats(
                    pos,
                    out waterTemperature,
                    out isDay,
                    out dayNightTime,
                    out sessionTimePlayed,
                    out saveSlot,
                    out gameMode);
            }
            catch
            {
                // Campos de entorno opcionales; no deben bloquear el sample del CSV.
            }

            snapshot = new PlayerStatsSnapshot
            {
                UtcTime = FormatUtcNow(),
                GameTime = Time.timeSinceLevelLoad,
                PlayerId = MyFirstSubnauticaModPlugin.LifeSyncCachedPlayerId.Value,
                Health = live != null ? live.health : 0f,
                HealthMax = live != null ? live.maxHealth : 0f,
                HealthBonusCfg = MyFirstSubnauticaModPlugin.PlayerMaxHealthBonus.Value,
                Oxygen = oxygenValue,
                OxygenMax = oxygenMax,
                OxygenBonusCfg = MyFirstSubnauticaModPlugin.PlayerMaxOxygenBonus.Value,
                Food = survival != null ? survival.food : 0f,
                Water = survival != null ? survival.water : 0f,
                KnifeDamageMultiplierCfg = MyFirstSubnauticaModPlugin.KnifeDamageMultiplier.Value,
                KnifeBonusDamageCfg = MyFirstSubnauticaModPlugin.KnifeBonusDamage.Value,
                KnifeDamageHeld = knifeHeld,
                KnifeDamageInventoryMax = knifeInventoryMax,
                FlashlightCapacityBonusPctCfg = MyFirstSubnauticaModPlugin.FlashlightCapacityBonusPercent.Value,
                FlashlightDrainReductionCfg = MyFirstSubnauticaModPlugin.FlashlightDrainReduction.Value,
                FlashlightCapacityTarget = FlashlightModifiers.GetTargetCapacity(),
                FlashlightDrainTarget = FlashlightModifiers.GetTargetDrain(),
                FlashlightBatteryCharge = flCharge,
                FlashlightBatteryCapacity = flCapacity,
                SeaglideCapacityBonusPctCfg = MyFirstSubnauticaModPlugin.SeaglideCapacityBonusPercent.Value,
                SeaglideSpeedBonusCfg = MyFirstSubnauticaModPlugin.SeaglideSpeedBonus.Value,
                SeaglideCapacityTarget = SeaglideModifiers.GetTargetCapacity(),
                SeaglideSpeedTarget = seaglideSpeedTarget,
                SeaglideBatteryCharge = sgCharge,
                SeaglideBatteryCapacity = sgCapacity,
                PosX = pos.x,
                PosY = pos.y,
                PosZ = pos.z,
                Depth = Mathf.Max(0f, -pos.y),
                GameMode = gameMode,
                SaveSlot = saveSlot,
                Biome = SafeString(() => GetBiomeName(player)),
                MotorMode = SafeString(() => player.motorMode.ToString()),
                PlayerMode = SafeString(() => GetPlayerModeLabel(player)),
                IsUnderwater = SafeBool(() => player.IsUnderwater()),
                IsSwimming = SafeBool(() => player.IsSwimming()),
                IsInSub = SafeBool(() => player.IsInSub()),
                IsInsideWalkable = SafeBool(() => player.IsInsideWalkable()),
                InSeamoth = SafeBool(() => player.inSeamoth),
                InExosuit = SafeBool(() => player.inExosuit),
                IsPiloting = SafeBool(() => player.IsPiloting()),
                VehicleContext = SafeString(() => GetVehicleContext(player)),
                GameDepth = SafeFloat(() => player.GetDepth()),
                DepthLevel = SafeFloat(() => player.depthLevel),
                MaxDepth = SafeFloat(() => player.GetDepth()),
                WaterTemperature = waterTemperature,
                RadiationAmount = SafeFloat(() => player.radiationAmount),
                InfectionAmount = SafeFloat(() => player.GetInfectionAmount()),
                InfectionRevealed = SafeBool(() => player.infectionRevealed),
                IsDay = isDay,
                DayNightTime = dayNightTime,
                SessionTimePlayed = sessionTimePlayed,
                InventoryItemCount = inventoryItemCount,
                InventoryCapacity = inventoryCapacity,
                Stomach = survival != null ? survival.stomach : 0f
            };

            return true;
        }

        private static string SafeString(System.Func<string> getter)
        {
            try
            {
                return getter() ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static bool SafeBool(System.Func<bool> getter)
        {
            try
            {
                return getter();
            }
            catch
            {
                return false;
            }
        }

        private static float SafeFloat(System.Func<float> getter)
        {
            try
            {
                return getter();
            }
            catch
            {
                return 0f;
            }
        }

        internal string ToCsvRow()
        {
            var values = new[]
            {
                UtcTime,
                FormatFloat(GameTime),
                PlayerId.ToString(CultureInfo.InvariantCulture),
                FormatFloat(Health),
                FormatFloat(HealthMax),
                HealthBonusCfg.ToString(CultureInfo.InvariantCulture),
                FormatFloat(Oxygen),
                FormatFloat(OxygenMax),
                OxygenBonusCfg.ToString(CultureInfo.InvariantCulture),
                FormatFloat(Food),
                FormatFloat(Water),
                FormatFloat(KnifeDamageMultiplierCfg),
                KnifeBonusDamageCfg.ToString(CultureInfo.InvariantCulture),
                FormatFloat(KnifeDamageHeld),
                FormatFloat(KnifeDamageInventoryMax),
                FlashlightCapacityBonusPctCfg.ToString(CultureInfo.InvariantCulture),
                FormatFloat(FlashlightDrainReductionCfg),
                FormatFloat(FlashlightCapacityTarget),
                FormatFloat(FlashlightDrainTarget),
                FormatFloat(FlashlightBatteryCharge),
                FormatFloat(FlashlightBatteryCapacity),
                SeaglideCapacityBonusPctCfg.ToString(CultureInfo.InvariantCulture),
                FormatFloat(SeaglideSpeedBonusCfg),
                FormatFloat(SeaglideCapacityTarget),
                FormatFloat(SeaglideSpeedTarget),
                FormatFloat(SeaglideBatteryCharge),
                FormatFloat(SeaglideBatteryCapacity),
                FormatFloat(PosX),
                FormatFloat(PosY),
                FormatFloat(PosZ),
                FormatFloat(Depth),
                GameMode ?? string.Empty,
                SaveSlot ?? string.Empty,
                Biome ?? string.Empty,
                MotorMode ?? string.Empty,
                PlayerMode ?? string.Empty,
                FormatBool(IsUnderwater),
                FormatBool(IsSwimming),
                FormatBool(IsInSub),
                FormatBool(IsInsideWalkable),
                FormatBool(InSeamoth),
                FormatBool(InExosuit),
                FormatBool(IsPiloting),
                VehicleContext ?? string.Empty,
                FormatFloat(GameDepth),
                FormatFloat(DepthLevel),
                FormatFloat(MaxDepth),
                FormatFloat(WaterTemperature),
                FormatFloat(RadiationAmount),
                FormatFloat(InfectionAmount),
                FormatBool(InfectionRevealed),
                FormatBool(IsDay),
                FormatFloat(DayNightTime),
                FormatFloat(SessionTimePlayed),
                InventoryItemCount.ToString(CultureInfo.InvariantCulture),
                InventoryCapacity.ToString(CultureInfo.InvariantCulture),
                FormatFloat(Stomach)
            };

            return string.Join(",", values);
        }

        internal string ToJsonDataObject()
        {
            var sb = new StringBuilder(512);
            sb.Append('{');
            AppendJsonPair(sb, "utc_time", UtcTime, first: true);
            AppendJsonPair(sb, "game_time", FormatFloat(GameTime));
            AppendJsonPair(sb, "player_id", PlayerId.ToString(CultureInfo.InvariantCulture));
            AppendJsonPair(sb, "health", FormatFloat(Health));
            AppendJsonPair(sb, "health_max", FormatFloat(HealthMax));
            AppendJsonPair(sb, "health_bonus_cfg", HealthBonusCfg.ToString(CultureInfo.InvariantCulture));
            AppendJsonPair(sb, "oxygen", FormatFloat(Oxygen));
            AppendJsonPair(sb, "oxygen_max", FormatFloat(OxygenMax));
            AppendJsonPair(sb, "oxygen_bonus_cfg", OxygenBonusCfg.ToString(CultureInfo.InvariantCulture));
            AppendJsonPair(sb, "food", FormatFloat(Food));
            AppendJsonPair(sb, "water", FormatFloat(Water));
            AppendJsonPair(sb, "knife_damage_multiplier_cfg", FormatFloat(KnifeDamageMultiplierCfg));
            AppendJsonPair(sb, "knife_bonus_damage_cfg", KnifeBonusDamageCfg.ToString(CultureInfo.InvariantCulture));
            AppendJsonPair(sb, "knife_damage_held", FormatFloat(KnifeDamageHeld));
            AppendJsonPair(sb, "knife_damage_inventory_max", FormatFloat(KnifeDamageInventoryMax));
            AppendJsonPair(sb, "flashlight_capacity_bonus_pct_cfg", FlashlightCapacityBonusPctCfg.ToString(CultureInfo.InvariantCulture));
            AppendJsonPair(sb, "flashlight_drain_reduction_cfg", FormatFloat(FlashlightDrainReductionCfg));
            AppendJsonPair(sb, "flashlight_capacity_target", FormatFloat(FlashlightCapacityTarget));
            AppendJsonPair(sb, "flashlight_drain_target", FormatFloat(FlashlightDrainTarget));
            AppendJsonPair(sb, "flashlight_battery_charge", FormatFloat(FlashlightBatteryCharge));
            AppendJsonPair(sb, "flashlight_battery_capacity", FormatFloat(FlashlightBatteryCapacity));
            AppendJsonPair(sb, "seaglide_capacity_bonus_pct_cfg", SeaglideCapacityBonusPctCfg.ToString(CultureInfo.InvariantCulture));
            AppendJsonPair(sb, "seaglide_speed_bonus_cfg", FormatFloat(SeaglideSpeedBonusCfg));
            AppendJsonPair(sb, "seaglide_capacity_target", FormatFloat(SeaglideCapacityTarget));
            AppendJsonPair(sb, "seaglide_speed_target", FormatFloat(SeaglideSpeedTarget));
            AppendJsonPair(sb, "seaglide_battery_charge", FormatFloat(SeaglideBatteryCharge));
            AppendJsonPair(sb, "seaglide_battery_capacity", FormatFloat(SeaglideBatteryCapacity));
            AppendJsonPair(sb, "pos_x", FormatFloat(PosX));
            AppendJsonPair(sb, "pos_y", FormatFloat(PosY));
            AppendJsonPair(sb, "pos_z", FormatFloat(PosZ));
            AppendJsonPair(sb, "depth", FormatFloat(Depth));
            AppendJsonPair(sb, "game_mode", GameMode);
            AppendJsonPair(sb, "save_slot", SaveSlot);
            AppendJsonPair(sb, "biome", Biome);
            AppendJsonPair(sb, "motor_mode", MotorMode);
            AppendJsonPair(sb, "player_mode", PlayerMode);
            AppendJsonPair(sb, "is_underwater", FormatBool(IsUnderwater));
            AppendJsonPair(sb, "is_swimming", FormatBool(IsSwimming));
            AppendJsonPair(sb, "is_in_sub", FormatBool(IsInSub));
            AppendJsonPair(sb, "is_inside_walkable", FormatBool(IsInsideWalkable));
            AppendJsonPair(sb, "in_seamoth", FormatBool(InSeamoth));
            AppendJsonPair(sb, "in_exosuit", FormatBool(InExosuit));
            AppendJsonPair(sb, "is_piloting", FormatBool(IsPiloting));
            AppendJsonPair(sb, "vehicle_context", VehicleContext);
            AppendJsonPair(sb, "game_depth", FormatFloat(GameDepth));
            AppendJsonPair(sb, "depth_level", FormatFloat(DepthLevel));
            AppendJsonPair(sb, "max_depth", FormatFloat(MaxDepth));
            AppendJsonPair(sb, "water_temperature", FormatFloat(WaterTemperature));
            AppendJsonPair(sb, "radiation_amount", FormatFloat(RadiationAmount));
            AppendJsonPair(sb, "infection_amount", FormatFloat(InfectionAmount));
            AppendJsonPair(sb, "infection_revealed", FormatBool(InfectionRevealed));
            AppendJsonPair(sb, "is_day", FormatBool(IsDay));
            AppendJsonPair(sb, "day_night_time", FormatFloat(DayNightTime));
            AppendJsonPair(sb, "session_time_played", FormatFloat(SessionTimePlayed));
            AppendJsonPair(sb, "inventory_item_count", InventoryItemCount.ToString(CultureInfo.InvariantCulture));
            AppendJsonPair(sb, "inventory_capacity", InventoryCapacity.ToString(CultureInfo.InvariantCulture));
            AppendJsonPair(sb, "stomach", FormatFloat(Stomach));
            sb.Append('}');
            return sb.ToString();
        }

        private static string GetBiomeName(Player player)
        {
            // GetBiomeString es público; biomeString es private en runtime.
            return player.GetBiomeString() ?? string.Empty;
        }

        /// <summary>
        /// Etiqueta de modo sin leer <c>Player.mode</c> (campo private en Assembly-CSharp runtime).
        /// </summary>
        private static string GetPlayerModeLabel(Player player)
        {
            if (player.IsPiloting())
            {
                return "Piloting";
            }

            if (player.IsInsideWalkable())
            {
                return "Sitting";
            }

            return "Normal";
        }

        private static string GetVehicleContext(Player player)
        {
            if (player.inExosuit)
            {
                return "exosuit";
            }

            if (player.inSeamoth)
            {
                return "seamoth";
            }

            if (player.IsInSubmarine())
            {
                var sub = player.GetCurrentSub();
                if (sub == null)
                {
                    return "cyclops";
                }

                var subName = sub.GetSubName();
                return string.IsNullOrEmpty(subName) ? "cyclops" : "cyclops:" + subName;
            }

            if (player.motorMode == Player.MotorMode.Seaglide)
            {
                return "seaglide";
            }

            if (player.IsPiloting())
            {
                var vehicle = player.GetVehicle();
                return vehicle != null ? vehicle.GetType().Name : "piloting";
            }

            return "on_foot";
        }

        private static void GetInventoryStats(out int itemCount, out int capacity)
        {
            itemCount = 0;
            capacity = 0;

            var inventory = Inventory.main;
            if (inventory?.container == null)
            {
                return;
            }

            itemCount = inventory.container.count;
            capacity = inventory.container.sizeX * inventory.container.sizeY;
        }

        private static void GetEnvironmentStats(
            Vector3 position,
            out float waterTemperature,
            out bool isDay,
            out float dayNightTime,
            out float sessionTimePlayed,
            out string saveSlot,
            out string gameMode)
        {
            waterTemperature = 0f;
            isDay = false;
            dayNightTime = 0f;
            sessionTimePlayed = 0f;
            saveSlot = string.Empty;
            // currentGameMode / currentSlot / timePlayedThisSession son private en runtime.
            gameMode = GetGameModeLabel();

            var temperatureSimulation = WaterTemperatureSimulation.main;
            if (temperatureSimulation != null)
            {
                waterTemperature = temperatureSimulation.GetTemperature(position);
            }

            var dayNightCycle = DayNightCycle.main;
            if (dayNightCycle != null)
            {
                isDay = dayNightCycle.IsDay();
                dayNightTime = dayNightCycle.GetDayNightCycleTime();
            }

            var saveLoadManager = SaveLoadManager.main;
            if (saveLoadManager == null)
            {
                return;
            }

            saveSlot = saveLoadManager.GetCurrentSlot() ?? string.Empty;
            sessionTimePlayed = saveLoadManager.timePlayedTotal;
        }

        private static string GetGameModeLabel()
        {
            try
            {
                GameModeOption mode;
                GameModeOption cheats;
                GameModeUtils.GetGameMode(out mode, out cheats);
                return mode.ToString();
            }
            catch
            {
                return string.Empty;
            }
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
                if (knife == null || !(knife.damage > inventoryMaxDamage))
                {
                    continue;
                }

                inventoryMaxDamage = knife.damage;
            }
        }

        private static void GetFlashlightBatteryStats(out float charge, out float capacity)
        {
            charge = 0f;
            capacity = 0f;

            var flashlight = Object.FindObjectOfType<FlashLight>();
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

            var seaglide = Object.FindObjectOfType<Seaglide>();
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
            return System.DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);
        }

        private static string FormatFloat(float value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static string FormatBool(bool value)
        {
            return value ? "1" : "0";
        }

        private static void AppendJsonPair(StringBuilder sb, string key, string value, bool first = false)
        {
            if (!first)
            {
                sb.Append(',');
            }

            sb.Append('"').Append(EscapeJson(key)).Append("\":");
            if (value == null)
            {
                sb.Append("null");
                return;
            }

            sb.Append('"').Append(EscapeJson(value)).Append('"');
        }

        private static string EscapeJson(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }
}
