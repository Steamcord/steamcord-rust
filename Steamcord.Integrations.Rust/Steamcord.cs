// ReSharper disable once CheckNamespace

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Oxide.Core.Libraries;
using Oxide.Plugins.SteamcordApi;
using Oxide.Plugins.SteamcordHttp;
using Oxide.Plugins.SteamcordRewards;

namespace Oxide.Plugins
{
    [Info("Steamcord", "Steamcord Team", "1.0.0")]
    public class Steamcord : CovalencePlugin
    {
        private static Steamcord _instance;
        private Configuration _config;
        private ISteamcordApiClient _steamcordApiClient;
        private Timer _timer;

        public Steamcord()
        {
            _instance = this;
        }

        private void Init()
        {
            _steamcordApiClient =
                new SteamcordApiClient(_config.Api.Token, _config.Api.BaseUri, new HttpRequestQueue());

            _steamcordApiClient.PlayersReceived += (sender, e) =>
            {
                // provision rewards
            };

            _timer = timer.Every(60, () => _steamcordApiClient.GetPlayers());
        }

        #region HTTP

        private class HttpRequestQueue : IHttpRequestQueue
        {
            public void PushRequest(string uri, Action<int, string> callback, string body = null,
                Dictionary<string, string> headers = null,
                HttpRequestType type = HttpRequestType.Get)
            {
                _instance.webrequest.Enqueue(uri, body,
                    (status, responseBody) => callback?.Invoke(status, responseBody), _instance, GetRequestMethod(type),
                    headers);
            }

            private RequestMethod GetRequestMethod(HttpRequestType requestType)
            {
                switch (requestType)
                {
                    case HttpRequestType.Get:
                        return RequestMethod.GET;
                    case HttpRequestType.Post:
                        return RequestMethod.POST;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(requestType), requestType, null);
                }
            }
        }

        #endregion

        #region Config

        protected override void LoadConfig()
        {
            base.LoadConfig();
            _config = Config.ReadObject<Configuration>();
            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            _config = Configuration.CreateDefault();
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(_config);
        }

        private class Configuration
        {
            public ApiOptions Api { get; set; }

            public IEnumerable<Reward> Rewards { get; set; }

            public static Configuration CreateDefault()
            {
                return new Configuration
                {
                    Api = new ApiOptions
                    {
                        Token = "<your api token>",
                        BaseUri = "https://steamcord.io/api"
                    },
                    Rewards = new[]
                    {
                        new Reward(new[]
                        {
                            Requirement.DiscordGuildMember,
                            Requirement.SteamGroupMember
                        }, "steamcord.discord_steam_member"),
                        new Reward(Requirement.DiscordGuildBooster, "steamcord.discord_booster")
                    }
                };
            }

            public class ApiOptions
            {
                public string Token { get; set; }
                public string BaseUri { get; set; }
            }
        }

        #endregion

        #region Lang

        protected override void LoadDefaultMessages()
        {
            _langService.RegisterMessages();
        }

        private class LangService : ILangService
        {
            public void RegisterMessages()
            {
                _instance.lang.RegisterMessages(new Dictionary<string, string>
                {
                    [Message.Error] = "Something went wrong, please try again later.",
                    [Message.ClaimNoRewards] = "We couldn't find a matching player, is your Steam account linked?",
                    [Message.ClaimRewards] = "Thank you for linking your accounts!"
                }, _instance);
            }

            public void MessagePlayer(IPlayer player, string key)
            {
                player.Message(_instance.lang.GetMessage(key, _instance, player.Id));
            }
        }

        #endregion
    }
}

namespace Oxide.Plugins.SteamcordApi
{
    #region Player

    public class Player
    {
        public int PlayerId { get; set; }
        public IEnumerable<DiscordAccount> DiscordAccounts { get; set; }
        public IEnumerable<SteamAccount> SteamAccounts { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime UpdatedDate { get; set; }
    }

    public class DiscordAccount
    {
        public string DiscordId { get; set; }
        public string Username { get; set; }
    }

    public class SteamAccount
    {
        public string SteamId { get; set; }
    }

    #endregion

    public class PlayerReceivedEventArgs : EventArgs
    {
        public PlayerReceivedEventArgs(Player player, bool isCommandCallback, string querySteamId)
        {
            Player = player;
            IsCommandCallback = isCommandCallback;
            QuerySteamId = querySteamId;
        }

        public bool IsCommandCallback { get; set; }
        public Player Player { get; set; }
        public string QuerySteamId { get; set; }
    }

    public interface ISteamcordApiClient
    {
        /// <summary>
        ///     Invoked when the Steamcord API responds to <c>GetPlayerBySteamId</c>.
        /// </summary>
        event EventHandler<PlayerReceivedEventArgs> PlayerReceived;

        /// <summary>
        ///     Gets the player from the Steamcord API and raises the <c>PlayerReceived</c> event.
        ///     See <see href="https://steamcord.io/docs/api-reference/players-resource.html#get-all-players">the docs</see>.
        /// </summary>
        void GetPlayerBySteamId(string steamId, bool isCommand = false);

        /// <summary>
        ///     See <see href="https://steamcord.io/docs/api-reference/steam-group-queue.html#push-a-steam-id">the docs</see>.
        /// </summary>
        /// <param name="steamIds"></param>
        void PushSteamIdsOntoQueue(string[] steamIds);
    }

    public class SteamcordApiClient : ISteamcordApiClient
    {
        private readonly string _baseUri;
        private readonly Dictionary<string, string> _headers;
        private readonly IHttpRequestQueue _httpRequestQueue;

        public SteamcordApiClient(string apiToken, string baseUri, IHttpRequestQueue httpRequestQueue)
        {
            _headers = new Dictionary<string, string>
            {
                ["Authorization"] = $"Bearer {apiToken}"
            };

            _baseUri = baseUri;
            _httpRequestQueue = httpRequestQueue;
        }

        public event EventHandler<PlayerReceivedEventArgs> PlayerReceived;

        public void GetPlayerBySteamId(string steamId, bool isCommand = false)
        {
            _httpRequestQueue.PushRequest($"{_baseUri}/players?steamId={steamId}", (status, body) =>
            {
                if (status != 200) return;

                var players = JsonConvert.DeserializeObject<Player[]>(body);
                OnPlayersReceived(new PlayerReceivedEventArgs(players.SingleOrDefault(), isCommand, steamId));
            }, headers: _headers);
        }

        public void PushSteamIdsOntoQueue(string[] steamIds)
        {
            if (steamIds.Length == 0) throw new ArgumentException();

            _httpRequestQueue.PushRequest($"{_baseUri}/steam-id-queue", headers: _headers, type: HttpRequestType.Post);
        }

        private void OnPlayersReceived(PlayerReceivedEventArgs e)
        {
            PlayerReceived?.Invoke(this, e);
        }
    }
}

namespace Oxide.Plugins.SteamcordHttp
{
    public enum HttpRequestType
    {
        Get,
        Post
    }

    public interface IHttpRequestQueue
    {
        void PushRequest(string uri, Action<int, string> callback = null, string body = null,
            Dictionary<string, string> headers = null, HttpRequestType type = HttpRequestType.Get);
    }
}

namespace Oxide.Plugins.SteamcordLang
{
    public static class Message
    {
        public const string Error = nameof(Error);
        public const string ClaimNoRewards = nameof(ClaimNoRewards);
        public const string ClaimRewards = nameof(ClaimRewards);
    }

    public interface ILangService
    {
        void RegisterMessages();
        void MessagePlayer(IPlayer player, string key);
    }
}

namespace Oxide.Plugins.SteamcordRewards
{
    public enum Requirement
    {
        Discord,
        DiscordGuildMember,
        DiscordGuildBooster,
        Steam,
        SteamGroupMember
    }

    public class Reward
    {
        public Reward(Requirement requirement, string groupName)
        {
            Requirements = new[] {requirement};
            GroupName = groupName;
        }

        public Reward(IEnumerable<Requirement> requirements, string groupName)
        {
            Requirements = requirements;
            GroupName = groupName;
        }

        [JsonConstructor]
        private Reward()
        {
        }

        [JsonProperty(ItemConverterType = typeof(StringEnumConverter))]
        public IEnumerable<Requirement> Requirements { get; set; }

        public string GroupName { get; set; }
    }

    public interface IRewardsService
    {
        bool IsEligible(Player player, Reward reward);
    }

    public class RewardsService : IRewardsService
    {
        public bool IsEligible(Player player, Reward reward)
        {
            return reward.Requirements.All(requirement => IsEligible(player, requirement));
        }

        private static bool IsEligible(Player player, Requirement requirement)
        {
            switch (requirement)
            {
                case Requirement.Discord:
                    return player.DiscordAccount != null;
                case Requirement.Steam:
                    return player.SteamAccount != null;
                case Requirement.DiscordGuildMember:
                case Requirement.DiscordGuildBooster:
                case Requirement.SteamGroupMember:
                    throw new NotImplementedException();
                default:
                    throw new ArgumentOutOfRangeException(nameof(requirement), requirement, null);
            }
        }
    }
}