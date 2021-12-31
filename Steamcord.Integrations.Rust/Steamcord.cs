﻿// ReSharper disable once CheckNamespace

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Oxide.Core.Libraries;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Oxide.Plugins.SteamcordApi;
using Oxide.Plugins.SteamcordHttp;
using Oxide.Plugins.SteamcordLang;
using Oxide.Plugins.SteamcordPermissions;
using Oxide.Plugins.SteamcordRewards;

namespace Oxide.Plugins
{
    [Info("Steamcord", "Steamcord Team", "1.0.0")]
    public class Steamcord : CovalencePlugin
    {
        private static Steamcord _instance;
        private readonly ILangService _langService;

        private Configuration _config;
        private IRewardsService _rewardsService;
        private ISteamcordApiClient _steamcordApiClient;

        public Steamcord()
        {
            _instance = this;
            _langService = new LangService();
        }

        private void Init()
        {
            _rewardsService = new RewardsService(_langService, new PermissionsService(), _config.Rewards);

            _steamcordApiClient =
                new SteamcordApiClient(_config.Api.Token, _config.Api.BaseUri, new HttpRequestQueue());

            AddUniversalCommand(_config.ChatCommand, nameof(ClaimCommand));

            timer.Every(5 * 60,
                () =>
                {
                    if (players.Connected.Any())
                        _steamcordApiClient.PushSteamIdsOntoQueue(players.Connected.Select(player => player.Id));
                });
        }

        private bool ClaimCommand(IPlayer player)
        {
            _steamcordApiClient.GetPlayerBySteamId(player.Id,
                steamcordPlayer => { _rewardsService.ProvisionRewards(player, steamcordPlayer); }, (status, _) =>
                {
                    // error
                });

            return true;
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

        #region Permissions

        private class PermissionsService : IPermissionsService
        {
            public void AddToGroup(IPlayer player, string group)
            {
                _instance.permission.AddUserGroup(player.Id, group);
            }

            public void RemoveFromGroup(IPlayer player, string group)
            {
                _instance.permission.RemoveUserGroup(player.Id, group);
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
            public string ChatCommand { get; set; }
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
                    ChatCommand = "claim",
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
                    [SteamcordLang.Message.Error] = "Something went wrong, please try again later.",
                    [SteamcordLang.Message.ClaimNoRewards] =
                        "We couldn't find a matching player, is your Steam account linked?",
                    [SteamcordLang.Message.ClaimRewards] = "Thank you for linking your accounts!"
                }, _instance);
            }

            public void Message(IPlayer player, string key)
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
    }

    public class DiscordAccount
    {
        public string DiscordId { get; set; }
        public bool IsGuildMember { get; set; }
        public bool IsGuildBooster { get; set; }
    }

    public class SteamAccount
    {
        public string SteamId { get; set; }
        public bool IsSteamGroupMember { get; set; }
    }

    #endregion

    public interface ISteamcordApiClient
    {
        /// <summary>
        ///     Gets the player from the Steamcord API and invokes one of the provided callbacks.
        ///     See <see href="https://steamcord.io/docs/api-reference/players-resource.html#get-all-players">the docs</see>.
        /// </summary>
        void GetPlayerBySteamId(string steamId, Action<Player> success = null, Action<int, string> error = null);

        /// <summary>
        ///     See <see href="https://steamcord.io/docs/api-reference/steam-group-queue.html#push-a-steam-id">the docs</see>.
        /// </summary>
        /// <param name="steamIds"></param>
        void PushSteamIdsOntoQueue(IEnumerable<string> steamIds);
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

        public void GetPlayerBySteamId(string steamId, Action<Player> success = null,
            Action<int, string> error = null)
        {
            _httpRequestQueue.PushRequest($"{_baseUri}/players?steamId={steamId}", (status, body) =>
            {
                if (status != 200)
                {
                    error?.Invoke(status, body);
                    return;
                }

                var players = JsonConvert.DeserializeObject<Player[]>(body);
                success?.Invoke(players.SingleOrDefault());
            }, headers: _headers);
        }

        public void PushSteamIdsOntoQueue(IEnumerable<string> steamIds)
        {
            if (!steamIds.Any()) throw new ArgumentException();

            _httpRequestQueue.PushRequest($"{_baseUri}/steam-id-queue", headers: _headers, type: HttpRequestType.Post);
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
        void Message(IPlayer player, string key);
    }
}

namespace Oxide.Plugins.SteamcordPermissions
{
    public interface IPermissionsService
    {
        void AddToGroup(IPlayer player, string group);
        void RemoveFromGroup(IPlayer player, string group);
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
        public Reward(Requirement requirement, string group)
        {
            Requirements = new[] {requirement};
            Group = group;
        }

        public Reward(IEnumerable<Requirement> requirements, string group)
        {
            Requirements = requirements;
            Group = group;
        }

        [JsonConstructor]
        private Reward()
        {
        }

        [JsonProperty(ItemConverterType = typeof(StringEnumConverter))]
        public IEnumerable<Requirement> Requirements { get; set; }

        public string Group { get; set; }
    }

    public interface IRewardsService
    {
        void ProvisionRewards(IPlayer player, Player steamcordPlayer);
    }

    public class RewardsService : IRewardsService
    {
        private readonly ILangService _langService;
        private readonly IPermissionsService _permissionsService;
        private readonly IEnumerable<Reward> _rewards;

        public RewardsService(ILangService langService, IPermissionsService permissionsService,
            IEnumerable<Reward> rewards)
        {
            _langService = langService;
            _permissionsService = permissionsService;
            _rewards = rewards;
        }

        public void ProvisionRewards(IPlayer player, Player steamcordPlayer)
        {
            if (steamcordPlayer == null)
            {
                _langService.Message(player, Message.ClaimNoRewards);
                return;
            }

            var givenReward = false;
            foreach (var reward in _rewards)
                if (IsEligible(steamcordPlayer, reward, player.Id))
                {
                    _permissionsService.AddToGroup(player, reward.Group);
                    givenReward = true;
                }
                else
                {
                    _permissionsService.RemoveFromGroup(player, reward.Group);
                }

            _langService.Message(player, givenReward ? Message.ClaimRewards : Message.ClaimNoRewards);
        }

        private bool IsEligible(Player player, Reward reward, string steamId)
        {
            return reward.Requirements.All(requirement => IsEligible(player, requirement, steamId));
        }

        private static bool IsEligible(Player player, Requirement requirement, string steamId)
        {
            switch (requirement)
            {
                case Requirement.Discord:
                    return player.DiscordAccounts.Any();
                case Requirement.Steam:
                    return player.SteamAccounts.Any();
                case Requirement.DiscordGuildMember:
                    return player.DiscordAccounts.Any(discordAccount => discordAccount.IsGuildMember);
                case Requirement.DiscordGuildBooster:
                    return player.DiscordAccounts.Any(discordAccount => discordAccount.IsGuildBooster);
                case Requirement.SteamGroupMember:
                    return player.SteamAccounts.Any(steamAccount =>
                        steamAccount.SteamId == steamId && steamAccount.IsSteamGroupMember);
                default:
                    throw new ArgumentOutOfRangeException(nameof(requirement), requirement, null);
            }
        }
    }
}