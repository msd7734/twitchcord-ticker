using System;
using System.Text;
using System.Reflection;
using System.IO;
using System.Security.Cryptography;

namespace TwitchTicker {
    public class Crypto {

        private readonly static int _IV_SIZE = 16;
        private readonly static int _KEY_SIZE = 32;
        private readonly static CipherMode _MODE = CipherMode.CBC;

        private byte[] _reflBytes;

        public uint Checksum { get; private set; }
        
        private Crypto(byte[] reflBytes) {
            _reflBytes = reflBytes;
            Checksum = 0;
        }

        public static Crypto Init() {
            byte[] refl = File.ReadAllBytes(Assembly.GetExecutingAssembly().Location);
            SHA1 sha = new SHA1Managed();
            return new Crypto(sha.ComputeHash(refl));
        }

        public byte[] Encrypt(string plain) {
            byte[] pt = Encoding.ASCII.GetBytes(plain);
            
            byte[] iv = DeriveIV(_reflBytes);
            byte[] k = DeriveKey(iv);

            byte[] result = ExecEncrypt(pt, iv, k, _MODE);
            SetChecksum(pt, iv);
            return result;
        }

        public byte[] Encrypt(string plain, string pass) {
            byte[] pt = Encoding.ASCII.GetBytes(plain);
            byte[] pw = Encoding.ASCII.GetBytes(pass);

            byte[] iv = DeriveIV(_reflBytes);
            byte[] k = DeriveKey(pw);

            byte[] result = ExecEncrypt(pt, iv, k, _MODE);
            SetChecksum(pt, iv);
            return result;
        }

        public byte[] Decrypt(byte[] cipher) {
            byte[] iv = DeriveIV(_reflBytes);
            byte[] k = DeriveKey(iv);
            
            byte[] result = ExecDecrypt(cipher, iv, k, _MODE);
            SetChecksum(result, iv);
            return result;
        }

        public byte[] Decrypt(byte[] cipher, string pass) {
            byte[] pw = Encoding.ASCII.GetBytes(pass);

            byte[] iv = DeriveIV(_reflBytes);
            byte[] k = DeriveKey(pw);
            
            byte[] result = ExecDecrypt(cipher, iv, k, _MODE);
            SetChecksum(result, iv);
            return result;
        }

        private byte[] ExecEncrypt(byte[] pt, byte[] iv, byte[] k, CipherMode mode) {
            using (var aes = new AesManaged()) {
                aes.IV = iv;
                aes.Key = k;
                aes.Mode = mode;

                ICryptoTransform encryptor = aes.CreateEncryptor();
                using (var ms = new MemoryStream()) {
                    using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write)) {
                        using (var writer = new StreamWriter(cs)) {
                            writer.Write(pt);
                        }
                        return ms.ToArray();
                    }
                }
            }
        }

        private byte[] ExecDecrypt(byte[] pt, byte[] iv, byte[] k, CipherMode mode) {
            using (var aes = new AesManaged()) {
                aes.IV = iv;
                aes.Key = k;
                aes.Mode = mode;

                ICryptoTransform decryptor = aes.CreateDecryptor();
                using (var ms = new MemoryStream()) {
                    using (var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read)) {
                        using (var reader = new StreamReader(cs)) {
                            return Encoding.ASCII.GetBytes(reader.ReadToEnd());
                        }
                    }
                }
            }
        }

        private void SetChecksum(byte[] source, byte[] salt) {
            int mergeSize = source.Length + salt.Length;
            byte[] merged = new byte[mergeSize];
            Array.Copy(source, merged, source.Length);
            Array.Copy(salt, 0, merged, source.Length, salt.Length);

            SHA1 sha = new SHA1Managed();
            byte[] hash = sha.ComputeHash(merged);

            int padSize = sizeof(uint);
            if (hash.Length % padSize != 0) {
                Array.Resize(ref hash, hash.Length + (hash.Length % padSize));
            }

            uint checksum = 0;
            using (var ms = new MemoryStream(hash, writable:false)) {
                using (var reader = new BinaryReader(ms)) {
                    for (long i = 0; i < ms.Length; i += padSize) {
                        checksum ^= reader.ReadUInt32();
                    }
                }
            }

            this.Checksum = checksum;
        }

        private static byte[] DeriveIV(byte[] source) {
            byte[] src = new byte[source.Length];
            Array.Copy(source, src, source.Length);

            if (src.Length < _IV_SIZE) {
                Array.Resize(ref src, _IV_SIZE);
            }

            byte[] iv = new byte[_IV_SIZE];
            Array.Copy(src, iv, _IV_SIZE);

            return iv;
        }

        private static byte[] DeriveKey(byte[] source) {
            if (source.Length < 1) {
                throw new ArgumentException($"Key source is empty");
            }

            SHA256 sha = new SHA256Managed();
            return sha.ComputeHash(source);
        }
    }
}