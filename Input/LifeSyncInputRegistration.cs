using System;
using BepInEx.Logging;
using Nautilus.Handlers;
using UnityEngine;

namespace LifeSyncGamesSubnautica.Input
{
    /// <summary>
    /// Registra una entrada en el sistema <see cref="GameInput"/> de Subnautica (vía Nautilus).
    /// </summary>
    internal static class LifeSyncInputRegistration
    {
        internal static GameInput.Button LoginMenuButton { get; private set; }

        internal static bool IsLoginMenuRegistered { get; private set; }

        private static bool _loggedRegisterFailure;

        /// <summary>
        /// Idempotente; reintenta si el primer intento fue demasiado pronto (p. ej. en Awake).
        /// </summary>
        internal static void EnsureRegistered(ManualLogSource log, KeyCode requestedKey)
        {
            if (IsLoginMenuRegistered)
            {
                return;
            }

            try
            {
                Register(log, requestedKey);
            }
            catch (Exception ex)
            {
                if (!_loggedRegisterFailure)
                {
                    _loggedRegisterFailure = true;
                    log.LogWarning($"[LifeSync] GameInput login aún no registrado ({ex.Message}). Se reintentará en partida.");
                }
            }
        }

        internal static void Register(ManualLogSource log, KeyCode requestedKey)
        {
            if (IsLoginMenuRegistered)
            {
                return;
            }

            var path = LifeSyncGameInputPaths.ToKeyboardPath(requestedKey, log);

            LoginMenuButton = EnumHandler.AddEntry<GameInput.Button>("LifeSyncMyFirstModLoginMenu")
                .CreateInput()
                .WithKeyboardBinding(path)
                .AvoidConflicts(GameInput.Device.Keyboard)
                .WithCategory("LifeSync Games");

            IsLoginMenuRegistered = true;

            log.LogInfo(
                $"[LifeSync] Menú login: entrada GameInput registrada (cfg KeyCode = {requestedKey}). " +
                "Reasigna en Opciones → Controles → LifeSync Games. Tras cambiar el KeyCode en el .cfg hace falta reiniciar.");
        }
    }
}
