using Discord;

namespace TwitchTicker {
    public class DiscordUtil {
        private DiscordUtil() {}

        public static string GetUniqueName(IUser user) {
            return user.Username + "#" + user.Discriminator;
        }
    }
}