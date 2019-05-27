using Discord;
using Discord.WebSocket;

using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections;
using System.Text;
using System.IO;

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
        private readonly static string _PREFIX = "!m ";

        private static Queue _msgQueue;
        private static object _lock = new object();

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
            => new Program().MainAsync().GetAwaiter().GetResult();

        public async Task MainAsync() {
            
            BotToken.ReadToken();
            
            string token = String.Empty;
            switch(BotToken.GetTokenState()) {
                case BotTokenState.Unchecked:
                case BotTokenState.Valid:
                    Console.WriteLine("Successfully read Discord bot token.");
                    token = HandleTokenDecrypt();
                    //token = ReadToken();
                    break;
                case BotTokenState.Missing:
                case BotTokenState.Outdated:
                case BotTokenState.Corrupted:
                    token = PromptAndStoreToken();
                    break;
            }

            if (token == String.Empty) {
                Console.WriteLine("Stopping. (Press any key to end)");
                Console.ReadKey();
                return;
            }

            var client = new DiscordSocketClient();
            client.Log += Log;
            client.MessageReceived += MessageReceived;
            
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

        private Task MessageReceived(SocketMessage msg) {
            if (msg.Content.StartsWith(_PREFIX)) {
                lock(_lock) {
                    String content = msg.Content;
                    int plen = _PREFIX.Length;
                    content = content.Substring(plen, content.Length - plen);
                    _msgQueue.Enqueue(new DiscordMessage(msg.Author.Id, content));
                }
                Console.WriteLine("--> SERVER: Queued Discord message");
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
