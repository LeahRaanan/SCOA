using System.Security.Cryptography;
using System.Text;

namespace SCOA.Services
{
    public class SecurityService
    {
        private readonly byte[] _aesKey;
        private const int SaltSize = 16;
        private const int HashSize = 32;
        private const int Iterations = 100_000;

        public SecurityService(string base64AesKey)
        {
            if (string.IsNullOrWhiteSpace(base64AesKey))
                throw new ArgumentNullException(nameof(base64AesKey), "AES key must not be empty.");

            _aesKey = Convert.FromBase64String(base64AesKey);

            if (_aesKey.Length != 16 && _aesKey.Length != 24 && _aesKey.Length != 32)
                throw new ArgumentException("AES key must be 128, 192, or 256 bits (16/24/32 bytes).");
        }

        // ════════════════════════════════════════════════════════════════════
        //  PASSWORD — PBKDF2 + HMACSHA256
        // ════════════════════════════════════════════════════════════════════

        public string HashPassword(string plainPassword)
        {
            if (string.IsNullOrEmpty(plainPassword))
                throw new ArgumentException("Password must not be empty.", nameof(plainPassword));

            byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
            byte[] hash = Pbkdf2(plainPassword, salt);

            byte[] combined = new byte[SaltSize + HashSize];
            Buffer.BlockCopy(salt, 0, combined, 0, SaltSize);
            Buffer.BlockCopy(hash, 0, combined, SaltSize, HashSize);

            return Convert.ToBase64String(combined);
        }

        public bool VerifyPassword(string plainPassword, string storedHash)
        {
            if (string.IsNullOrEmpty(plainPassword) || string.IsNullOrEmpty(storedHash))
                return false;

            byte[] combined = Convert.FromBase64String(storedHash);
            if (combined.Length != SaltSize + HashSize) return false;

            byte[] salt = combined[..SaltSize];
            byte[] expectedHash = combined[SaltSize..];
            byte[] actualHash = Pbkdf2(plainPassword, salt);

            return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
        }

        private static byte[] Pbkdf2(string password, byte[] salt) =>
            Rfc2898DeriveBytes.Pbkdf2(
                Encoding.UTF8.GetBytes(password),
                salt,
                Iterations,
                HashAlgorithmName.SHA256,
                HashSize);

        // ════════════════════════════════════════════════════════════════════
        //  EMAIL — AES-256-CBC (הצפנה הפיכה לשליחת מיילים)
        // ════════════════════════════════════════════════════════════════════

        public string EncryptEmail(string plainEmail)
        {
            if (string.IsNullOrEmpty(plainEmail))
                throw new ArgumentException("Email must not be empty.", nameof(plainEmail));

            using var aes = Aes.Create();
            aes.Key = _aesKey;
            aes.GenerateIV();

            using var encryptor = aes.CreateEncryptor();
            byte[] plainBytes = Encoding.UTF8.GetBytes(plainEmail);
            byte[] cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

            byte[] result = new byte[aes.IV.Length + cipherBytes.Length];
            Buffer.BlockCopy(aes.IV, 0, result, 0, aes.IV.Length);
            Buffer.BlockCopy(cipherBytes, 0, result, aes.IV.Length, cipherBytes.Length);

            return Convert.ToBase64String(result);
        }

        public string DecryptEmail(string encryptedEmail)
        {
            if (string.IsNullOrEmpty(encryptedEmail))
                throw new ArgumentException("Encrypted email must not be empty.", nameof(encryptedEmail));

            byte[] combined = Convert.FromBase64String(encryptedEmail);
            if (combined.Length < 17)
                throw new ArgumentException("Encrypted email data is too short.");

            byte[] iv = combined[..16];
            byte[] cipherBytes = combined[16..];

            using var aes = Aes.Create();
            aes.Key = _aesKey;
            aes.IV = iv;

            using var decryptor = aes.CreateDecryptor();
            byte[] plainBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);

            return Encoding.UTF8.GetString(plainBytes);
        }

        // ════════════════════════════════════════════════════════════════════
        //  EMAIL HASH — SHA-256 חד-כיווני לחיפוש במסד
        // ════════════════════════════════════════════════════════════════════

        public string HashEmail(string plainEmail)
        {
            if (string.IsNullOrEmpty(plainEmail))
                throw new ArgumentException("Email must not be empty.", nameof(plainEmail));

            byte[] bytes = SHA256.HashData(
                Encoding.UTF8.GetBytes(plainEmail.ToLowerInvariant().Trim())
            );
            return Convert.ToBase64String(bytes);
        }
    }
}