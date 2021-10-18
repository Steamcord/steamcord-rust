namespace Oxide.Ext.Steamcord.Config
{
    internal class SteamcordConfig
    {
        public ApiConfig Api { get; set; }
        public GroupsConfig Groups { get; set; }

        public class ApiConfig
        {
            public string ApiKey { get; set; } = "<your api key>";
            public string BaseUri { get; set; } = "https://api.steamcord.com";
        }

        public class GroupsConfig
        {
            public string DiscordGroup { get; set; } = "discord";
            public string DiscordBoosterGroup { get; set; } = "booster";
        }
    }
}