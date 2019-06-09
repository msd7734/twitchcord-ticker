using System;
using System.Threading.Tasks;
using System.Reflection;

using Discord;
using Discord.Commands;
using Discord.WebSocket;

namespace TwitchTicker {
    
    public class CommandHandler {
        private readonly DiscordSocketClient _client;
        private readonly CommandService _commands;

        public CommandHandler(DiscordSocketClient client, CommandService commands) {
            _client = client;
            _commands = commands;
        }

        public async Task InstallCommandsAsync() {
            _client.MessageReceived += HandleCommandAsync;
            await _commands.AddModulesAsync(Assembly.GetEntryAssembly(), null);
        }

        private async Task HandleCommandAsync(SocketMessage messageParam) {
            var message = messageParam as SocketUserMessage;
            if (message == null) {
                return;
            }

            int argPos = 0;

            if (!message.HasCharPrefix('!', ref argPos) || message.Author.IsBot) {
                return;
            }

            var context = new SocketCommandContext(_client, message);

            var result = await _commands.ExecuteAsync(context, argPos, null);
        }
    }
}