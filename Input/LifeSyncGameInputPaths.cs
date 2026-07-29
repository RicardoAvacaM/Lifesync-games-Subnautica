using BepInEx.Logging;
using Nautilus.Handlers;
using UnityEngine;

namespace LifeSyncGamesSubnautica.Input
{
    /// <summary>
    /// Convierte <see cref="KeyCode"/> del cfg a rutas de teclado del Input System (Nautilus / GameInputHandler).
    /// </summary>
    internal static class LifeSyncGameInputPaths
    {
        private static bool _warnedUnknown;

        internal static string ToKeyboardPath(KeyCode key, ManualLogSource log)
        {
            switch (key)
            {
                case KeyCode.F1: return GameInputHandler.Paths.Keyboard.F1;
                case KeyCode.F2: return GameInputHandler.Paths.Keyboard.F2;
                case KeyCode.F3: return GameInputHandler.Paths.Keyboard.F3;
                case KeyCode.F4: return GameInputHandler.Paths.Keyboard.F4;
                case KeyCode.F5: return GameInputHandler.Paths.Keyboard.F5;
                case KeyCode.F6: return GameInputHandler.Paths.Keyboard.F6;
                case KeyCode.F7: return GameInputHandler.Paths.Keyboard.F7;
                case KeyCode.F8: return GameInputHandler.Paths.Keyboard.F8;
                case KeyCode.F9: return GameInputHandler.Paths.Keyboard.F9;
                case KeyCode.F10: return GameInputHandler.Paths.Keyboard.F10;
                case KeyCode.F11: return GameInputHandler.Paths.Keyboard.F11;
                case KeyCode.F12: return GameInputHandler.Paths.Keyboard.F12;
                case KeyCode.A: return GameInputHandler.Paths.Keyboard.A;
                case KeyCode.B: return GameInputHandler.Paths.Keyboard.B;
                case KeyCode.C: return GameInputHandler.Paths.Keyboard.C;
                case KeyCode.D: return GameInputHandler.Paths.Keyboard.D;
                case KeyCode.E: return GameInputHandler.Paths.Keyboard.E;
                case KeyCode.F: return GameInputHandler.Paths.Keyboard.F;
                case KeyCode.G: return GameInputHandler.Paths.Keyboard.G;
                case KeyCode.H: return GameInputHandler.Paths.Keyboard.H;
                case KeyCode.I: return GameInputHandler.Paths.Keyboard.I;
                case KeyCode.J: return GameInputHandler.Paths.Keyboard.J;
                case KeyCode.K: return GameInputHandler.Paths.Keyboard.K;
                case KeyCode.L: return GameInputHandler.Paths.Keyboard.L;
                case KeyCode.M: return GameInputHandler.Paths.Keyboard.M;
                case KeyCode.N: return GameInputHandler.Paths.Keyboard.N;
                case KeyCode.O: return GameInputHandler.Paths.Keyboard.O;
                case KeyCode.P: return GameInputHandler.Paths.Keyboard.P;
                case KeyCode.Q: return GameInputHandler.Paths.Keyboard.Q;
                case KeyCode.R: return GameInputHandler.Paths.Keyboard.R;
                case KeyCode.S: return GameInputHandler.Paths.Keyboard.S;
                case KeyCode.T: return GameInputHandler.Paths.Keyboard.T;
                case KeyCode.U: return GameInputHandler.Paths.Keyboard.U;
                case KeyCode.V: return GameInputHandler.Paths.Keyboard.V;
                case KeyCode.W: return GameInputHandler.Paths.Keyboard.W;
                case KeyCode.X: return GameInputHandler.Paths.Keyboard.X;
                case KeyCode.Y: return GameInputHandler.Paths.Keyboard.Y;
                case KeyCode.Z: return GameInputHandler.Paths.Keyboard.Z;
                case KeyCode.Alpha0: return GameInputHandler.Paths.Keyboard.Key0;
                case KeyCode.Alpha1: return GameInputHandler.Paths.Keyboard.Key1;
                case KeyCode.Alpha2: return GameInputHandler.Paths.Keyboard.Key2;
                case KeyCode.Alpha3: return GameInputHandler.Paths.Keyboard.Key3;
                case KeyCode.Alpha4: return GameInputHandler.Paths.Keyboard.Key4;
                case KeyCode.Alpha5: return GameInputHandler.Paths.Keyboard.Key5;
                case KeyCode.Alpha6: return GameInputHandler.Paths.Keyboard.Key6;
                case KeyCode.Alpha7: return GameInputHandler.Paths.Keyboard.Key7;
                case KeyCode.Alpha8: return GameInputHandler.Paths.Keyboard.Key8;
                case KeyCode.Alpha9: return GameInputHandler.Paths.Keyboard.Key9;
                case KeyCode.Escape: return GameInputHandler.Paths.Keyboard.Escape;
                case KeyCode.Tab: return GameInputHandler.Paths.Keyboard.Tab;
                case KeyCode.Space: return GameInputHandler.Paths.Keyboard.Space;
                case KeyCode.Return: return GameInputHandler.Paths.Keyboard.Enter;
                case KeyCode.Backspace: return GameInputHandler.Paths.Keyboard.Backspace;
                case KeyCode.BackQuote: return GameInputHandler.Paths.Keyboard.Backquote;
                default:
                    if (!_warnedUnknown && key != KeyCode.F10)
                    {
                        _warnedUnknown = true;
                        log?.LogWarning($"[LifeSync] KeyCode {key} sin mapeo explícito; usando F10. Añade el mapeo en LifeSyncGameInputPaths o elige F1–F12 / letras / números.");
                    }

                    return GameInputHandler.Paths.Keyboard.F10;
            }
        }
    }
}
