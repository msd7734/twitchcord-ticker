using System;

namespace TwitchTicker {
    [Flags]
    public enum BotTokenFlags : byte {
        // Intended to be serialized as one byte - do not add more than 8 flags
        // Don't expect to need more than 8 flags...
        UsePassword = 0x1
    }
}