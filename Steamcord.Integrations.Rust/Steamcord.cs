// ReSharper disable once CheckNamespace

extern alias NewtonsoftJson;
using System;
using System.Collections.Generic;
using NewtonsoftJson::Newtonsoft.Json;
using Oxide.Core.Libraries;
using Oxide.Plugins.SteamcordHttp;
using Oxide.Plugins.SteamcordService;

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

        private enum Reward
        {
            Discord,
            DiscordGuildMember,
            DiscordGuildBooster,
            Steam,
            SteamGroupMember
        }

        #region Config

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
                        BaseUri = "https://steamcord.io/api"
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
                public string BaseUri { get; set; }
            }
        }

        #endregion
        
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
                        throw new ArgumentException();
                }
            }
        }

        #endregion
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

namespace Oxide.Plugins.SteamcordService
{
    #region Player

    public class Player
    {
        public int PlayerId { get; set; }
        public DiscordAccount DiscordAccount { get; set; }
        public SteamAccount SteamAccount { get; set; }
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

    public class PlayersReceivedEventArgs : EventArgs
    {
        public PlayersReceivedEventArgs(IEnumerable<Player> players)
        {
            Players = players;
        }

        public IEnumerable<Player> Players { get; set; }
    }

    public interface ISteamcordApiClient
    {
        /// <summary>
        ///     Invoked when the Steamcord API responds to <c>GetPlayers</c>.
        /// </summary>
        event EventHandler<PlayersReceivedEventArgs> PlayersReceived;

        /// <summary>
        ///     Gets players from the Steamcord API and raises the <c>PlayersReceived</c> event.
        ///     See <see href="https://steamcord.io/docs/api-reference/players-resource.html#get-all-players">the docs</see>.
        /// </summary>
        void GetPlayers();

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

        public event EventHandler<PlayersReceivedEventArgs> PlayersReceived;

        public void GetPlayers()
        {
            _httpRequestQueue.PushRequest($"{_baseUri}/players", (status, body) =>
            {
                if (status != 200) return;

                var players = JsonConvert.DeserializeObject<Player[]>(body);
                OnPlayersReceived(new PlayersReceivedEventArgs(players));
            }, headers: _headers);
        }

        public void PushSteamIdsOntoQueue(string[] steamIds)
        {
            if (steamIds.Length == 0) throw new ArgumentException();

            _httpRequestQueue.PushRequest($"{_baseUri}/steam-id-queue", headers: _headers, type: HttpRequestType.Post);
        }

        private void OnPlayersReceived(PlayersReceivedEventArgs e)
        {
            PlayersReceived?.Invoke(this, e);
        }
    }
}