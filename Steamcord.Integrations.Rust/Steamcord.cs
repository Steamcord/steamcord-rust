// ReSharper disable once CheckNamespace

using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("Steamcord", "Steamcord Team", "1.0.0")]
    internal class Steamcord : CovalencePlugin
    {
        private Configuration _config;
        private Timer _timer;

        private void Init()
        {
            _timer = timer.Every(60f, () =>
            {
                var headers = new Dictionary<string, string>
                {
                    ["Authorization"] = $"Bearer {_config.Api.Token}"
                };

                webrequest.Enqueue($"{_config.Api.BaseUrl}/api/players", null, (status, body) =>
                {
                    if (status == 403)
                    {
                        _timer.Destroy();
                    }

                    // provision rewards
                }, this, headers: headers);
            });
        }

        private enum Reward
        {
            Discord,
            DiscordGuildMember,
            DiscordGuildBooster,
            Steam,
            SteamGroupMember
        }

        #region Configuration

        protected override void LoadConfig()
        {
            base.LoadConfig();
            _config = Config.ReadObject<Configuration>();
            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            _config = new Configuration();
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(_config);
        }

        private class Configuration
        {
            public ApiOptions Api { get; set; } = new ApiOptions();

            public Dictionary<string, string> Rewards { get; set; }

            public static Configuration CreateDefault()
            {
                return new Configuration
                {
                    Api = new ApiOptions
                    {
                        Token = "<your api token>",
                        BaseUrl = "https://steamcord.io/api"
                    },
                    Rewards = new Dictionary<string, string>
                    {
                        [nameof(Reward.Discord)] = "steamcord.discord",
                        [nameof(Reward.DiscordGuildMember)] = "steamcord.discordguildmember",
                        [nameof(Reward.DiscordGuildBooster)] = "steamcord.discordguildbooster",
                        [nameof(Reward.Steam)] = "steamcord.steam",
                        [nameof(Reward.SteamGroupMember)] = "steamcord.steamgroupmember"
                    }
                };
            }

            public class ApiOptions
            {
                public string Token { get; set; }
                public string BaseUrl { get; set; }
            }
        }

        #endregion
    }
}