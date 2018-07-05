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
        private readonly static String _TOKEN = ReadToken();
        private readonly static String _PREFIX = "!m ";

        private static Queue _msgQueue;
        private static object _lock = new object();

        private static String ReadToken() {
            string tokenPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "token");
            Console.WriteLine(tokenPath);
            Console.ReadKey();
            return File.ReadAllText(tokenPath).Trim();
        }

        public static void Main(string[] args)
            => new Program().MainAsync().GetAwaiter().GetResult();

        public async Task MainAsync() {
            if (String.IsNullOrWhiteSpace(_TOKEN)) {
                Console.Error.WriteLine("Discord token empty or not found. The bot is unable to connect.");
                await Task.CompletedTask;
            }

            var client = new DiscordSocketClient();
            client.Log += Log;
            client.MessageReceived += MessageReceived;
            
            Server watsonLocal = new Server("localhost", 9000, false, DefaultRoute, false);
            watsonLocal.AddStaticRoute("get", "/hello/", HelloRoute);
            watsonLocal.AddStaticRoute("get", "/queue/", QueueRoute);
            
            _msgQueue = new Queue();

            await client.LoginAsync(TokenType.Bot, _TOKEN);
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
