using System;
using System.Security.Cryptography;
using System.Text;

namespace LifeSyncGamesSubnautica.Services
{
    /// <summary>
    /// Cifrado en reposo con DPAPI (<see cref="ProtectedData"/>) atado a la cuenta de Windows actual.
    /// El blob persistido lleva prefijo <see cref="StoredPrefix"/> para distinguirlo de texto legado.
    /// </summary>
    internal static class DpapiProtect
    {
        internal const string StoredPrefix = "dpapi:";

        /// <summary>Entropía extra (no es la clave; la clave la custodia Windows).</summary>
        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("LifeSync-Games-Subnautica.v1");

        internal static bool IsProtected(string stored)
        {
            return !string.IsNullOrEmpty(stored) &&
                   stored.StartsWith(StoredPrefix, StringComparison.Ordinal);
        }

        /// <summary>Vacío se persiste vacío. Cualquier otro texto se cifra para el usuario de Windows actual.</summary>
        internal static string ProtectToStored(string plaintext)
        {
            if (string.IsNullOrEmpty(plaintext))
            {
                return string.Empty;
            }

            var bytes = Encoding.UTF8.GetBytes(plaintext);
            var protectedBytes = ProtectedData.Protect(bytes, Entropy, DataProtectionScope.CurrentUser);
            return StoredPrefix + Convert.ToBase64String(protectedBytes);
        }

        /// <summary>
        /// Descifra un blob <see cref="StoredPrefix"/> o, si el texto no está protegido, lo trata como legado en claro.
        /// </summary>
        internal static bool TryUnprotect(string stored, out string plaintext)
        {
            plaintext = string.Empty;
            if (string.IsNullOrEmpty(stored))
            {
                return true;
            }

            if (!IsProtected(stored))
            {
                plaintext = stored;
                return true;
            }

            try
            {
                var b64 = stored.Substring(StoredPrefix.Length);
                var protectedBytes = Convert.FromBase64String(b64);
                var bytes = ProtectedData.Unprotect(protectedBytes, Entropy, DataProtectionScope.CurrentUser);
                plaintext = Encoding.UTF8.GetString(bytes);
                return true;
            }
            catch (Exception)
            {
                plaintext = string.Empty;
                return false;
            }
        }
    }
}
