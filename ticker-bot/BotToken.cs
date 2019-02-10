using System;
using System.Text;
using System.IO;
using System.Security.Cryptography;

namespace TwitchTicker {
    public class BotToken {
        private readonly static String _FILENAME = "token";
        private readonly static int _FILESIZE = 0x100;
        private readonly static byte[] _HEADER = {0x30, 0x46, 0x45, 0x33, 0x30, 0x61, 0x34, 0x4a, 0x33, 0x38, 0x51};
        private readonly static int _TOKENSIZE = 120;
        private readonly static int _DATASIZE = 120;

        private static BotToken _instance = null;
        private static BotToken _badToken = new BotToken(new byte[0], 0, 0, new byte[0], BotTokenState.Corrupted);

        private byte[] _fileHeader;
        private byte _flags;
        private uint _checksum;
        private byte[] _data;
        private BotTokenState _state;

        private BotToken(byte[] header, byte flags, uint checksum, byte[] data, BotTokenState state = BotTokenState.Unchecked) {
            _fileHeader = header;
            _flags = flags;
            _checksum = checksum;
            _data = data;
            _state = state;
        }

        public static void WriteToken(string tokenStr, string password = "") {
            bool noPassword = (password == String.Empty);
            
            var crypto = Crypto.Init();
            byte[] encrToken;

            if (noPassword) {
                encrToken = crypto.Encrypt(tokenStr);
            }
            else {
                encrToken = crypto.Encrypt(tokenStr, password);
            }
            
            if (File.Exists(_FILENAME)) {
                File.Delete(_FILENAME);
            }

            byte flags = (byte)(noPassword ? 0x0 : BotTokenFlags.UsePassword);

            using (var fs = File.Create(_FILENAME)) {
                using (var writer = new BinaryWriter(fs)) {

                    // Header
                    writer.Write(_HEADER);

                    // Flags
                    writer.Write((byte)flags);

                    // Checksum
                    writer.Write(crypto.Checksum);

                    // Token
                    writer.Write(encrToken);
                }
            }

            _instance = new BotToken(_HEADER, flags, crypto.Checksum, Encoding.ASCII.GetBytes(tokenStr), BotTokenState.Valid);
        }

        public static void ReadToken() {
            if (!File.Exists(_FILENAME)) {
                throw new BotTokenException("Bot token file not found");
            }

            using (var fs = File.OpenRead(_FILENAME)) {
                using (var reader = new BinaryReader(fs)) {

                    try {
                        byte[] fileHeader = reader.ReadBytes(_HEADER.Length);

                        byte flags = reader.ReadByte();

                        uint checksum = reader.ReadUInt32();

                        long dataSize = reader.BaseStream.Length - reader.BaseStream.Position;
                        
                        byte[] data = reader.ReadBytes(Convert.ToInt32(dataSize));

                        _instance = new BotToken(fileHeader, flags, checksum, data);
                    }
                    catch (ArgumentException) {
                        // If we got here, we overran the EOF which means the file is considered corrupted
                        _instance = _badToken;
                    }
                    catch (OverflowException) {
                        // We can safely consider more then Int32.MaxInteger bytes of data to be corrupted
                        _instance = _badToken;
                    }
                }
            }
        }

        public static string GetTokenString() {
            if (_instance == null) {
                throw new InvalidOperationException("BotToken not initialized");
            }
            else if (_instance._state != BotTokenState.Valid) {
                throw new InvalidOperationException($"BotToken is not valid (state: {_instance._state.ToString()})");
            }

            return Encoding.ASCII.GetString(_instance._data);
        }

        public static bool UsingPassword() {
            if (_instance == null) {
                throw new InvalidOperationException("BotToken not initialized");
            }
            else if (_instance._state != BotTokenState.Valid) {
                throw new InvalidOperationException($"BotToken is not valid (state: {_instance._state.ToString()})");
            }
            
            return ((BotTokenFlags)_instance._flags & BotTokenFlags.UsePassword) != 0;
        }

        public static bool Exists() {
            return File.Exists(_FILENAME);
        }

        public static BotTokenState CheckTokenState() {
            if (File.Exists(_FILENAME)) {
                var fileInfo = new FileInfo(_FILENAME);
                if (fileInfo.Length != _FILESIZE) {
                    return BotTokenState.Corrupted;
                }
                byte[] contents = File.ReadAllBytes(_FILENAME);
                return verifyFileContents(contents);
            }
            else {
                File.Create(_FILENAME).Close();
                return BotTokenState.Missing;
            }
        }
        
        private static BotTokenState verifyFileContents(byte[] contents) {
            if (contents.Length != _FILESIZE) {
                return BotTokenState.Corrupted;
            }

            return BotTokenState.Valid;
        }

        private static uint calcChecksum(byte[] data) {
            return 0;
        }
    }
}