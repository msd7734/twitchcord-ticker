﻿using Discord;
using Discord.WebSocket;

using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Linq;

using WatsonWebserver;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using Newtonsoft.Json.Schema;
using Newtonsoft.Json.Linq;

namespace TwitchTicker
{
    public class Program
    {
        private readonly static string _MSG_PREFIX = "!m ";
        private readonly static string _ADMIN_PREFIX = "!a ";

        private static Queue _msgQueue;
        private static object _lock = new object();

        private readonly static ArgDef[] _ARG_DEFS = new ArgDef[] {
            new ArgDefFlag("encrypt-only", "e"),
            new ArgDefKeyValue("password", "p"),
            new ArgDefKeyValue("token", "t")
        };

        private static AdminList _adminList;

        private static List<IDisposable> _disposables;

        private static string StoreToken(string token, string password) {
            BotToken.WriteToken(token, password);
            return BotToken.GetTokenString();
        }

        private static string PromptAndStoreToken() {
            Console.Write("The Discord bot token needs to be stored. Enter the token: ");
            string token = Console.ReadLine();

            Console.WriteLine(
                Environment.NewLine + 
                "The token will be stored encrypted. You may choose to a use a password to encrypt it. " + 
                "If you don't use a password, a default encryption scheme will be used (this is less secure). " +
                Environment.NewLine
            );
            
            Console.Write("Use a password? (y/n) ");
            string yesno = Console.ReadLine();

            bool usePassword = (yesno.Length > 0 && yesno.Substring(0,1).ToLower() == "y");

            string password = String.Empty;
            if (usePassword) {
                do {
                    Console.Write("Enter your password: ");
                    password = Console.ReadLine();
                } while (password.Length < 1);
                
                Console.WriteLine("WARNING: If you forget your password you will have to delete the token file and re-encrypt the token.");
            }
            else {
                Console.WriteLine("Using default encryption.");
            }

            BotToken.WriteToken(token, password);
            return BotToken.GetTokenString();
        }

        public static string HandleTokenDecrypt() {
            if (BotToken.UsingPassword()) {
                bool retry = true;
                while (retry) {
                    Console.Write("Enter your password: ");
                    string password = Console.ReadLine();
                    bool verified = BotToken.DecryptAndVerify(password);
                    if (BotToken.GetTokenState() == BotTokenState.Corrupted) {
                        Console.WriteLine("Checksum mismatch. (New bot version or corrupted file)");
                        return PromptAndStoreToken();
                    }
                    else if (!verified) {
                        Console.Write("Could not decrypt token with that password. Try again? (y/n) ");
                        string yesno = Console.ReadLine();
                        retry = (yesno.Length > 0 && yesno.Substring(0,1).ToLower() == "y");
                    }
                    else {
                        Console.WriteLine("Token decrypted.");
                        return BotToken.GetTokenString();
                    }
                }
                // User decided to stop retrying
                string pathToTokenFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "token").ToString();
                Console.WriteLine($"Store a new token by deleting the token file at {pathToTokenFile}");
            }
            else {
                bool verified = BotToken.DecryptAndVerify();
                if (!verified) {
                    Console.WriteLine("Checksum mismatch. (New bot version or corrupted file)");
                    return PromptAndStoreToken();
                }
                else {
                    Console.WriteLine("Token decrypted.");
                    return BotToken.GetTokenString();
                }
            }

            // Couldn't get a token
            return "";
        }

        public static void Main(string[] args)
            => new Program().MainAsync(args).ContinueWith(task => CleanUp()).GetAwaiter().GetResult();

        public async Task MainAsync(string[] cmdArgs) {
            // List of items to clean up when the bot exits
            _disposables = new List<IDisposable>();

            ArgParse args = new ArgParse();
            args.AddDefs(_ARG_DEFS);
            args.Parse(cmdArgs);
            
            bool encryptOnlyMode = args.GetFlag("encrypt-only");
            string givenPassword = args.GetValue("password");
            string givenToken = args.GetValue("token");

            string token = String.Empty;

            if (encryptOnlyMode) {
                Console.WriteLine("Encrypt-only mode.");
                if (givenToken != String.Empty) {
                    if (givenPassword != String.Empty) {
                        Console.WriteLine("Using password.");
                    }
                    else {
                        Console.WriteLine("Not using password.");
                    }
                    StoreToken(givenToken, givenPassword);
                    Console.WriteLine("Token stored successfully.");
                }
                else {
                    Console.WriteLine("Encrypt-only mode must be used with a token. Run with option --token <string>.");
                }
                return;
            }
            else if (givenToken != String.Empty) {
                Console.WriteLine("Storing token.");
                if (givenPassword != String.Empty) {
                    Console.WriteLine("Using password.");
                }
                else {
                    Console.WriteLine("Not using password.");
                }
                StoreToken(givenToken, givenPassword);
                token = givenToken;
            }
            else {
                // Handle user-mode
                BotToken.ReadToken();
                switch(BotToken.GetTokenState()) {
                    case BotTokenState.Unchecked:
                    case BotTokenState.Valid:
                        Console.WriteLine("Loading Discord bot token.");
                        token = HandleTokenDecrypt();
                        break;
                    case BotTokenState.Missing:
                    case BotTokenState.Outdated:
                    case BotTokenState.Corrupted:
                        token = PromptAndStoreToken();
                        break;
                }
            }

            if (token == String.Empty) {
                Console.WriteLine("Stopping. (Press any key to end)");
                Console.ReadKey();
                return;
            }

            var config = new DiscordSocketConfig();
            config.AlwaysDownloadUsers = true;

            var client = new DiscordSocketClient(config);
            client.Log += Log;
            client.Ready += Ready;
            client.MessageReceived += MessageReceived;

            _adminList = new AdminList(client);
            _disposables.Add(_adminList);
            
            Server watsonLocal = new Server("localhost", 9000, false, DefaultRoute, false);
            watsonLocal.AddStaticRoute("get", "/hello/", HelloRoute);
            watsonLocal.AddStaticRoute("get", "/queue/", QueueRoute);
            
            _msgQueue = new Queue();

            await client.LoginAsync(TokenType.Bot, token);

            await client.StartAsync();

            await Task.Delay(-1);
        }

        private Task Log(LogMessage msg) {
            Console.WriteLine(msg.ToString());
            return Task.CompletedTask;
        }

        private Task Ready() {
            return _adminList.LoadAdminsAsync()
                             .ContinueWith(task => Console.WriteLine(_adminList.Message));
        }

        private Task MessageReceived(SocketMessage msg) {
            if (msg.Content.StartsWith(_MSG_PREFIX)) {
                lock(_lock) {
                    String content = msg.Content;
                    int plen = _MSG_PREFIX.Length;
                    content = content.Substring(plen, content.Length - plen);
                    _msgQueue.Enqueue(new DiscordMessage(msg.Author.Id, content));
                }
                string user = msg.Author.Username;
                string discrim = msg.Author.Discriminator;
                Console.WriteLine($"--> SERVER: Queued Discord message (from {user}#{discrim})");
            }
            else if (msg.Content.StartsWith(_ADMIN_PREFIX)) {
                string friendlyName = DiscordUtil.GetUniqueName(msg.Author);
                if (!_adminList.IsAdmin(msg.Author)) {
                    Console.WriteLine($"--> SERVER: Requested admin command but was rejected ({friendlyName})");
                    return Task.CompletedTask;
                }

                String content = msg.Content;
                int plen = _ADMIN_PREFIX.Length;
                content = content.Substring(plen, content.Length - plen);
                string[] tokens = content.Split(" ");
                if (tokens.Length > 1) {
                    if (tokens[0] == "list" && tokens[1] == "admins") {
                        string adminStr = String.Join(", ", _adminList.GetAdmins());
                        Console.WriteLine($"--> SERVER: Requested admins ({friendlyName})");
                        Console.WriteLine($"--> SERVER: {adminStr}");
                        msg.Channel.SendMessageAsync($"Current users with admin permissions are: {adminStr}");
                    }
                    else if (tokens[0] == "add" && tokens[1] == "admin") {
                        var user = msg.MentionedUsers.FirstOrDefault();
                        if (user == null) {
                            Console.WriteLine("--> SERVER: Unknown user.");
                            return Task.CompletedTask;
                        }
                        bool success = _adminList.AddAdmin(user);
                        Console.WriteLine($"--> SERVER: Giving admin to {friendlyName}");
                        Console.WriteLine($"--> SERVER: {_adminList.Message}");
                        if (success) {
                            msg.Channel.SendMessageAsync($"I've added {friendlyName} as an admin.");                        
                        }
                        else {
                            msg.Channel.SendMessageAsync($"I couldn't add that user as an admin: {_adminList.Message}");
                        }
                    }
                    else if (tokens[0] == "remove" && tokens[1] == "admin") {
                        var user = msg.MentionedUsers.FirstOrDefault();
                        if (user == null) {
                            Console.WriteLine("--> SERVER: Unknown user.");
                            return Task.CompletedTask;
                        }
                        bool success = _adminList.RemoveAdmin(user);
                        Console.WriteLine($"--> SERVER: Revoking admin from {friendlyName}");
                        Console.WriteLine($"--> SERVER: {_adminList.Message}");
                        if (success) {
                            msg.Channel.SendMessageAsync($"I've removed {friendlyName} as an admin.");
                        }
                        else {
                            msg.Channel.SendMessageAsync($"I couldn't remove that user as an admin: {_adminList.Message}");
                        }
                    }
                }
                else {
                    Console.WriteLine("--> SERVER: Unknown admin command received.");
                }
            }

            return Task.CompletedTask;
        }

        static HttpResponse DefaultRoute(HttpRequest r)
            => new HttpResponse(r, true, 404, null, "text/plain", "Invalid resource", true);

        static HttpResponse HelloRoute(HttpRequest r) {
            Console.WriteLine("--> SERVER: Ticker pinged local server");
            return new HttpResponse(r, true, 200, null, "text/plain", String.Empty, false);
        }

        static HttpResponse QueueRoute(HttpRequest r) {

            JArray ja = new JArray();

            if (_msgQueue.Count > 0) {
                // get all queued messages and clear queue
                while(_msgQueue.Count > 0) {
                    ja.Add(JsonConvert.SerializeObject(_msgQueue.Dequeue()));
                }
                Console.WriteLine($"--> SERVER: Ticker consumed message queue");
            }
            
            // constructor can accept any type and it will just call Json.Serialize on it
            byte[] data = Encoding.UTF8.GetBytes(ja.ToString());
            return new HttpResponse(r, true, 200, null, "text/plain", ja, false);
        }

        // TODO: Actually capturing a console application close in a dotnet core-friendly way is 
        //  one of the most complex problems in all of computing, apparently.
        //  Reference: https://github.com/dotnet/coreclr/issues/8565#issuecomment-435698397
        private static void CleanUp() {
            foreach (var d in _disposables) {
                d.Dispose();
            }
        }
    }

    public class DiscordMessage {
        [JsonProperty]
        public ulong Id {
            get; set;
        }

        [JsonProperty]
        public string Message {
            get; set;
        }

        public DiscordMessage(ulong id, string message) {
            Id = id;
            Message = message;
        }
    }
}
