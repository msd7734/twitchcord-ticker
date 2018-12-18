using System;

namespace TwitchTicker {
    public class BotTokenException : Exception {
        public BotTokenException() : base("There was an error handling the Discord bot's access token.") { }
        public BotTokenException(string message) : base(message) { } 
    }

}