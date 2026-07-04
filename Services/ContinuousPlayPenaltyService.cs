using System.Collections;
using UnityEngine;

namespace MyFirstSubnauticaMod.Services
{
    /// <summary>
    /// Tras 1 h de juego seguido en partida, aplica −5 vida/oxígeno máx. permanentes cada 30 min extra.
    /// Solo corre si <see cref="MyFirstSubnauticaModPlugin.ContinuousPlayPenaltyEnabled"/> está activo.
    /// </summary>
    internal sealed class ContinuousPlayPenaltyService : MonoBehaviour
    {
        internal const float FirstPenaltySeconds = 3600f;
        internal const float RepeatPenaltySeconds = 1800f;
        internal const int PenaltyPerStep = 5;

        internal static ContinuousPlayPenaltyService Instance { get; private set; }

        private static float _continuousPlaySeconds;
        private static int _penaltiesAppliedThisStreak;

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
            return MyFirstSubnauticaModPlugin.ContinuousPlayPenaltyEnabled != null &&
                   MyFirstSubnauticaModPlugin.ContinuousPlayPenaltyEnabled.Value;
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

            if (_continuousPlaySeconds < GetNextThresholdSeconds())
            {
                return;
            }

            if (!PlayerStatsApplier.TryApplyFatiguePenalty(PenaltyPerStep))
            {
                MyFirstSubnauticaModPlugin.Log.LogInfo(
                    "[LifeSync][Fatigue] Penalización omitida (tope mínimo de vida/oxígeno alcanzado).");
            }
            else
            {
                MyFirstSubnauticaModPlugin.Log.LogInfo(
                    $"[LifeSync][Fatigue] Penalización aplicada (−{PenaltyPerStep} vida/oxígeno máx. permanentes). " +
                    $"Tiempo seguido={FormatMinutes(_continuousPlaySeconds)}, " +
                    $"penalizaciones en racha={_penaltiesAppliedThisStreak + 1}.");
            }

            _penaltiesAppliedThisStreak++;
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
