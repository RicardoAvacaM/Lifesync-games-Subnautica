using LifeSyncGamesSubnautica.Services;
using NUnit.Framework;

namespace LifeSyncGamesSubnautica.Tests
{
    [TestFixture]
    public class DpapiProtectTests
    {
        [Test]
        public void ProtectToStored_Empty_ReturnsEmpty()
        {
            Assert.That(DpapiProtect.ProtectToStored(null), Is.EqualTo(string.Empty));
            Assert.That(DpapiProtect.ProtectToStored(string.Empty), Is.EqualTo(string.Empty));
        }

        [Test]
        public void ProtectToStored_Plaintext_UsesPrefix()
        {
            var stored = DpapiProtect.ProtectToStored("eyJhbGciOiJIUzI1NiJ9.payload");
            Assert.That(DpapiProtect.IsProtected(stored), Is.True);
            Assert.That(stored, Does.Not.Contain("eyJhbGciOiJIUzI1NiJ9"));
        }

        [Test]
        public void TryUnprotect_Roundtrip_ReturnsOriginal()
        {
            const string plain = "eyJhbGciOiJIUzI1NiJ9.payload.signature";
            var stored = DpapiProtect.ProtectToStored(plain);
            Assert.That(DpapiProtect.TryUnprotect(stored, out var restored), Is.True);
            Assert.That(restored, Is.EqualTo(plain));
        }

        [Test]
        public void TryUnprotect_LegacyPlaintext_ReturnsSame()
        {
            const string legacy = "eyJhbGciOiJIUzI1NiJ9.legacy";
            Assert.That(DpapiProtect.IsProtected(legacy), Is.False);
            Assert.That(DpapiProtect.TryUnprotect(legacy, out var restored), Is.True);
            Assert.That(restored, Is.EqualTo(legacy));
        }

        [Test]
        public void TryUnprotect_Empty_ReturnsEmpty()
        {
            Assert.That(DpapiProtect.TryUnprotect(string.Empty, out var restored), Is.True);
            Assert.That(restored, Is.EqualTo(string.Empty));
        }

        [Test]
        public void TryUnprotect_CorruptBlob_ReturnsFalse()
        {
            Assert.That(DpapiProtect.TryUnprotect("dpapi:@@@not-base64@@@", out var restored), Is.False);
            Assert.That(restored, Is.EqualTo(string.Empty));
        }

        [Test]
        public void ProtectToStored_CsvRow_Roundtrip()
        {
            const string row = "2026-11-18T19:00:00,12.5,42,80,100";
            var stored = DpapiProtect.ProtectToStored(row);
            Assert.That(stored, Does.Not.Contain("80,100"));
            Assert.That(DpapiProtect.TryUnprotect(stored, out var restored), Is.True);
            Assert.That(restored, Is.EqualTo(row));
        }
    }
}
