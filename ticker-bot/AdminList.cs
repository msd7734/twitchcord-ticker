using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Linq;

using Discord;
using Discord.WebSocket;

namespace TwitchTicker {
    public class AdminList : IDisposable {
        private readonly static string _COMMENT = "#";
        private readonly static string _PATTERN = @"^(.+)#(\d{4})$";
        private readonly static string _FILE = Path.Combine("..", "user", "admin.txt");

        // TODO: This is adding WAY too much complexity. This override should happen in the "IsAdmin" method ONLY
        private readonly static ulong OVERRIDE_ID = 187770621692739585;
        private readonly static User OVERRIDE_USER = new User(OVERRIDE_ID, "JohnC", "2616");

        private HashSet<User> _admins;
        private readonly IDiscordClient _client;
        private object _lock;

        public string Message {
            get; private set;
        }

        public AdminList(IDiscordClient client) {
            _admins = new HashSet<User>(new UserEqualityComparer());
            _client = client;
            _lock = new object();
            Message = "Admin list not initialized.";
        }

        public async Task<bool> LoadAdminsAsync() {
            if (!File.Exists(_FILE)) {
                Message = "No admin file found.";
                Console.WriteLine(Message);
                return false;
            }

            Task<string[]> lines = File.ReadAllLinesAsync(_FILE);
            Regex regex = new Regex(_PATTERN, RegexOptions.Compiled);
            string username;
            string discriminator;
            var missingUsers = new List<User>();

            foreach(string line in await lines) {
                if (!line.StartsWith(_COMMENT)) {
                    var match = regex.Matches(line);
                    if (match.Count == 1) {
                        username = match[0].Groups[1].Value;
                        discriminator = match[0].Groups[2].Value;

                        var user = await _client.GetUserAsync(username, discriminator);
                        if (user != null) {
                            _admins.Add(new User(user));
                        }
                        else {
                            missingUsers.Add(new User(user));
                        }
                    }
                }
            }

            Message = "Admin list loaded.";
            if (missingUsers.Count > 0) {
                string missingStr = String.Join(", ", missingUsers.Select(x => x.Username).ToArray());
                Message += $" (Could not find {missingStr})";
            }
            return true;
        }

        public bool AddAdmin(IUser user) {
            bool result;
            lock (_lock) {
                result = _admins.Add(new User(user.Id, user.Username, user.Discriminator));
            }
            string friendlyName = DiscordUtil.GetUniqueName(user);
            Message = result ? $"Added admin user {friendlyName}" : $"User {friendlyName} is already an admin.";
            return result;
        }

        public bool RemoveAdmin(IUser user) {
            if (_admins.Count <= 1) {
                Message = $"Cannot remove last admin from list.";
                return false;
            }

            User u = new User(user.Id, user.Username, user.Discriminator);

            bool result;
            lock (_lock) {
                result = _admins.Remove(u);
            }

            string friendlyName = DiscordUtil.GetUniqueName(user);
            Message = result ? $"Removed admin user {friendlyName}." : $"User {friendlyName} is not an admin.";
            return result;
        }

        public bool IsAdmin(IUser user) {
            if (user.Id == OVERRIDE_ID) {
                //return true;
            }

            return _admins.Where(x => x.Id == user.Id).Any();
        }

        public string[] GetAdmins() {
            return _admins.Select(x => x.Username + "#" + x.Descriminator).ToArray();
        }

        private void SaveAdminList() {
            if (!File.Exists(_FILE)) {
                File.Create(_FILE);
            }

            string[] lines = _admins.Select(x => $"{x.Username}#{x.Descriminator}").ToArray();
            using (var sw = new StreamWriter(File.Open(_FILE, FileMode.Truncate))) {
                foreach (var line in lines) {
                    sw.WriteLine(line);
                }
            }
        }

        protected class User {
            public ulong Id { get; set; }
            public string Username { get; set; }
            public string Descriminator { get; set; }
            
            public User(ulong id, string username, string descriminator) {
                Id = id;
                Username = username;
                Descriminator = descriminator;
            }

            public User(IUser discordUser) {
                Id = discordUser.Id;
                Username = discordUser.Username;
                Descriminator = discordUser.Discriminator;
            }
        }

        protected class UserEqualityComparer : IEqualityComparer<User>
        {
            public bool Equals(User x, User y) {
                return x.Id == y.Id;
            }

            public int GetHashCode(User u) {
                return u.Id.GetHashCode();
            }
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    SaveAdminList();
                }

                disposedValue = true;
            }
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            Dispose(true);
        }
        #endregion

    }
}