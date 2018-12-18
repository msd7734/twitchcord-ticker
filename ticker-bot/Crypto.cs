using System;
using System.Reflection;
using System.IO;
using System.Security.Cryptography;

namespace TwitchTicker {
    public class Crypto {
        private byte[] _c;
        private byte[] _s;
        
        private Crypto(byte[] c, byte[] s) {
            _c = c;
            _s = s;
        }

        public static Crypto Init() {
            byte[] refl = getReflData();
            try {
                int slen = 8;
                int clen = refl.Length - slen;
                byte[] s = new byte[slen];
                byte[] c = new byte[clen];

                Array.Copy(refl, c, clen);
                Array.Copy(refl, clen, refl, 0, slen);

                return new Crypto(c, s);
            } catch (IndexOutOfRangeException e) {
                throw new BotTokenException("The bot could not be run because the access token cannot be properly stored.");
            }
            
        }

        private static byte[] getReflData()
            => File.ReadAllBytes(Assembly.GetExecutingAssembly().Location);
    }
}