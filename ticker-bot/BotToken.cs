using System;
using System.IO;
using System.Security.Cryptography;

namespace TwitchTicker {
    public class BotToken {
        private readonly static String _FILENAME = "token";
        private readonly static int _FILESIZE = 0x100;
        private readonly static byte[] _HEADER = {0x30, 0x46, 0x45, 0x33, 0x30, 0x61, 0x34, 0x4a, 0x33, 0x38, 0x51};
        private readonly static int _DATASIZE = _FILESIZE - (_HEADER.Length + sizeof(uint));

        private static BotToken _instance;

        private byte[] _fileHeader;
        private uint _checksum;
        private byte[] _data;


        public String TokenString { get; private set; }

        private BotToken(String plaintextTokenStr) { 
            this.TokenString = plaintextTokenStr;
        }

        public static bool CheckToken() {
            bool tokenReady;

            if (File.Exists(_FILENAME)) {
                byte[] contents = File.ReadAllBytes(_FILENAME);
                if (contents.Length == 0) {
                    tokenReady = false;
                }
                else {
                    tokenReady = verifyFileContents(contents);
                }
            }
            else {
                File.Create(_FILENAME).Close();
                tokenReady = false;
            }

            return tokenReady;
        }
        
        private static bool verifyFileContents(byte[] contents) {
            if (contents.Length != _FILESIZE) {
                return false;
            }

            return false;
        }

        private static uint calcChecksum(byte[] data) {
            return 0;
        }
    }
}