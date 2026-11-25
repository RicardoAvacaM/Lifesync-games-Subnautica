using System.Collections;
using UnityEngine;

namespace LifeSyncGamesSubnautica.Services
{
    /// <summary>
    /// Tras 1 h de juego seguido en partida, aplica −5 vida/oxígeno máx. permanentes cada 30 min extra.
    /// Solo corre si <see cref="LifeSyncGamesSubnauticaPlugin.ContinuousPlayPenaltyEnabled"/> está activo.
    /// </summary>
    internal sealed class ContinuousPlayPenaltyService : MonoBehaviour
    {
        // TEMPORAL PARA PRUEBAS (restaurar: 3600 / 1800 / 600 / 300).
        // Aviso HUD a los 5 min de juego; primera penalización a los 15 min; siguientes cada 6 min (aviso 5 min antes).
        internal const float FirstPenaltySeconds = 900f;
        internal const float RepeatPenaltySeconds = 360f;
        internal const int PenaltyPerStep = 5;
        internal const float FirstAdvanceWarningSeconds = 600f;
        internal const float RepeatAdvanceWarningSeconds = 300f;

        internal static ContinuousPlayPenaltyService Instance { get; private set; }

        private static float _continuousPlaySeconds;
        private static int _penaltiesAppliedThisStreak;
        private static bool _advanceWarningShown;

        private Coroutine _trackRoutine;

        internal static float ContinuousPlaySeconds => _continuousPlaySeconds;

        internal static int PenaltiesAppliedThisStreak => _penaltiesAppliedThisStreak;

        internal static float SecondsUntilNextPenalty
        {
            get
            {
                if (!IsEnabled())
                {
                    return FirstPenaltySeconds;
                }

                return Mathf.Max(0f, GetNextThresholdSeconds() - _continuousPlaySeconds);
            }
        }

        internal static void EnsureOnHost(GameObject host)
        {
            if (host == null)
            {
                return;
            }

            if (host.GetComponent<ContinuousPlayPenaltyService>() != null)
            {
                return;
            }

            host.AddComponent<ContinuousPlayPenaltyService>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
        }

        private void Start()
        {
            RestartTracking();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        internal static void OnSettingChanged()
        {
            if (!IsEnabled())
            {
                ResetStreak();
                Instance?.StopTracking();
                return;
            }

            Instance?.RestartTracking();
        }

        private static bool IsEnabled()
        {
            return LifeSyncGamesSubnauticaPlugin.ContinuousPlayPenaltyEnabled != null &&
                   LifeSyncGamesSubnauticaPlugin.ContinuousPlayPenaltyEnabled.Value;
        }

        private void RestartTracking()
        {
            StopTracking();
            if (!IsEnabled())
            {
                return;
            }

            _trackRoutine = StartCoroutine(TrackRoutine());
        }

        private void StopTracking()
        {
            if (_trackRoutine == null)
            {
                return;
            }

            StopCoroutine(_trackRoutine);
            _trackRoutine = null;
        }

        private static void ResetStreak()
        {
            _continuousPlaySeconds = 0f;
            _penaltiesAppliedThisStreak = 0;
            _advanceWarningShown = false;
        }

        private static float GetNextThresholdSeconds()
        {
            if (_penaltiesAppliedThisStreak <= 0)
            {
                return FirstPenaltySeconds;
            }

            return FirstPenaltySeconds + _penaltiesAppliedThisStreak * RepeatPenaltySeconds;
        }

        private IEnumerator TrackRoutine()
        {
            var wait = new WaitForSeconds(1f);
            while (IsEnabled())
            {
                yield return wait;
                TickOneSecond();
            }

            _trackRoutine = null;
        }

        private static void TickOneSecond()
        {
            if (!IsEnabled())
            {
                return;
            }

            if (Player.main == null)
            {
                ResetStreak();
                return;
            }

            // Menú LifeSync pausa con timeScale=0; no cuenta como juego seguido.
            if (Time.timeScale <= 0f)
            {
                return;
            }

            _continuousPlaySeconds += 1f;
            TryShowAdvanceWarning();

            if (_continuousPlaySeconds < GetNextThresholdSeconds())
            {
                return;
            }

            if (!PlayerStatsApplier.TryApplyFatiguePenalty(PenaltyPerStep))
            {
                LifeSyncGamesSubnauticaPlugin.Log.LogInfo(
                    "[LifeSync][Fatigue] Penalización omitida (tope mínimo de vida/oxígeno alcanzado).");
                NotifyHud("LifeSync: fatiga al mínimo; no se resta más vida/oxígeno máx.");
            }
            else
            {
                LifeSyncGamesSubnauticaPlugin.Log.LogInfo(
                    $"[LifeSync][Fatigue] Penalización aplicada (−{PenaltyPerStep} vida/oxígeno máx. permanentes). " +
                    $"Tiempo seguido={FormatMinutes(_continuousPlaySeconds)}, " +
                    $"penalizaciones en racha={_penaltiesAppliedThisStreak + 1}.");
                NotifyHud(
                    $"LifeSync: fatiga −{PenaltyPerStep} vida máx. y −{PenaltyPerStep} oxígeno máx. permanentes.");
            }

            _penaltiesAppliedThisStreak++;
            _advanceWarningShown = false;
        }

        private static float GetAdvanceWarningLeadSeconds()
        {
            return _penaltiesAppliedThisStreak <= 0
                ? FirstAdvanceWarningSeconds
                : RepeatAdvanceWarningSeconds;
        }

        private static void TryShowAdvanceWarning()
        {
            if (_advanceWarningShown)
            {
                return;
            }

            var lead = GetAdvanceWarningLeadSeconds();
            if (_continuousPlaySeconds + 0.5f < GetNextThresholdSeconds() - lead)
            {
                return;
            }

            _advanceWarningShown = true;
            if (_penaltiesAppliedThisStreak <= 0)
            {
                NotifyHud(
                    $"LifeSync: llevas casi 1 h seguida. En 10 min bajarán {PenaltyPerStep} de vida y oxígeno máximos.");
            }
            else
            {
                NotifyHud(
                    $"LifeSync: en 5 min se aplicará otra penalización (−{PenaltyPerStep} vida y oxígeno máx.).");
            }
        }

        private static void NotifyHud(string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                return;
            }

            try
            {
                ErrorMessage.AddMessage(message);
            }
            catch (System.Exception ex)
            {
                LifeSyncGamesSubnauticaPlugin.Log.LogWarning(
                    $"[LifeSync][Fatigue] No se pudo mostrar aviso HUD: {ex.Message}");
            }
        }

        internal static string FormatMinutes(float seconds)
        {
            var totalMinutes = Mathf.FloorToInt(seconds / 60f);
            var hours = totalMinutes / 60;
            var minutes = totalMinutes % 60;
            if (hours > 0)
            {
                return $"{hours}h {minutes}m";
            }

            return $"{minutes}m";
        }
    }
}
