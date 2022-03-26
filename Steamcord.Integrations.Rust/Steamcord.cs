// Copyright 2021 Steamcord LLC

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Oxide.Core;
using Oxide.Core.Libraries;
using Oxide.Core.Libraries.Covalence;
using Oxide.Plugins.SteamcordApi;
using Oxide.Plugins.SteamcordHttp;
using Oxide.Plugins.SteamcordLang;
using Oxide.Plugins.SteamcordLogging;
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

        public Steamcord()
        {
            _instance = this;
            _langService = new LangService();
        }

        public IRewardsService RewardsService { get; set; }
        public ISteamcordApiClient ApiClient { get; set; }

        private void Init()
        {
            RewardsService = new RewardsService(_langService, new PermissionsService(), _config.Rewards);

            ApiClient =
                new SteamcordApiClient(_config.Api.Token, _config.Api.BaseUri, new HttpRequestQueue(new Logger()));

            if (_config.ChatCommandsEnabled)
                foreach (var chatCommand in _config.ChatCommands)
                    AddUniversalCommand(chatCommand, nameof(ClaimCommand));

            foreach (var group in _config.Rewards.Select(reward => reward.Group))
                if (permission.CreateGroup(group, group, 0))
                    Puts($"Created Oxide group \"{group}\".");
        }

        private void OnServerInitialized()
        {
            if (_config.UpdateSteamGroups)
                // Do not change this interval. The Steam group queue is rate limited aggressively.
                timer.Every(15 * 60,
                    () =>
                    {
                        if (players.Connected.Any())
                            ApiClient.EnqueueSteamIds(players.Connected.Select(player => player.Id));
                    });
        }

        private void Unload()
        {
            // Oxide/uMod requirement
            _instance = null;
        }

        private bool ClaimCommand(IPlayer player)
        {
            ApiClient.GetPlayerBySteamId(player.Id,
                steamcordPlayer => { RewardsService.ProvisionRewards(player, steamcordPlayer); },
                (status, _) => { _langService.Message(player, Message.Error); });

            return true;
        }

        #region HTTP

        private class HttpRequestQueue : IHttpRequestQueue
        {
            private readonly ILogger _logger;

            public HttpRequestQueue(ILogger logger)
            {
                _logger = logger;
            }

            public void EnqueueRequest(string uri, Action<int, string> callback, string body = null,
                Dictionary<string, string> headers = null, HttpRequestType type = HttpRequestType.Get)
            {
                _instance.webrequest.Enqueue(uri, body,
                    (status, responseBody) =>
                    {
                        if (status != 200 && status != 204) _logger.LogError(GetErrorMessage(status));

                        callback?.Invoke(status, responseBody);
                    }, _instance, GetRequestMethod(type),
                    headers);
            }

            private static string GetErrorMessage(int status)
            {
                switch (status)
                {
                    case 401:
                        return "Received an unauthorized response, check your API token.";
                    case 403:
                        return "Received a forbidden response, check your subscription status.";
                    case 429:
                        return "Received a rate limit response.";
                    default:
                        return $"Received unexpected status {status}.";
                }
            }

            private static RequestMethod GetRequestMethod(HttpRequestType requestType)
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

        #region Logging

        private class Logger : ILogger
        {
            public void LogError(string message)
            {
                Interface.Oxide.LogError($"[{nameof(Steamcord)}] {message}");
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
            _config = new Configuration();
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(_config);
        }

        private class Configuration
        {
            public ApiOptions Api { get; set; } = new ApiOptions
            {
                Token = "<your api token>",
                BaseUri = "https://api.steamcord.io"
            };

            public IEnumerable<string> ChatCommands { get; set; } = new[] {"claim"};
            public bool ChatCommandsEnabled { get; set; } = true;

            public IEnumerable<Reward> Rewards { get; set; } = new[]
            {
                new Reward(new[]
                {
                    Requirement.DiscordGuildMember,
                    Requirement.SteamGroupMember
                }, "discord-steam-member"),
                new Reward(Requirement.DiscordGuildBooster, "discord-booster")
            };

            public bool UpdateSteamGroups { get; set; } = true;

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
                        "We couldn't find a matching player, link your accounts at [#9d46ff]steamcord.link/your-slug[/#].",
                    [SteamcordLang.Message.ClaimRewards] = "Thank you for linking your accounts!"
                }, _instance);
            }

            public void Message(IPlayer player, string key)
            {
                player.Message(_instance.covalence.FormatText(_instance.lang.GetMessage(key, _instance, player.Id)));
            }
        }

        #endregion
    }
}

namespace Oxide.Plugins.SteamcordApi
{
    #region Player

    public class SteamcordPlayer
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
        ///     See <see href="https://docs.steamcord.io/api-reference/players-resource.html#get-all-players">the docs</see>.
        /// </summary>
        /// <param name="steamId"></param>
        /// <param name="success"></param>
        /// <param name="error"></param>
        void GetPlayerBySteamId(string steamId, Action<SteamcordPlayer> success = null,
            Action<int, string> error = null);

        /// <summary>
        ///     See <see href="https://docs.steamcord.io/api-reference/steam-group-queue.html#enqueue-steam-ids">the docs</see>.
        /// </summary>
        /// <param name="steamIds"></param>
        void EnqueueSteamIds(IEnumerable<string> steamIds);
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
                ["Authorization"] = $"Bearer {apiToken}",
                ["Content-Type"] = "application/json"
            };

            _baseUri = baseUri;
            _httpRequestQueue = httpRequestQueue;
        }

        public void GetPlayerBySteamId(string steamId, Action<SteamcordPlayer> success = null,
            Action<int, string> error = null)
        {
            _httpRequestQueue.EnqueueRequest($"{_baseUri}/players?steamId={steamId}", (status, body) =>
            {
                if (status != 200)
                {
                    error?.Invoke(status, body);
                    return;
                }

                var players = JsonConvert.DeserializeObject<SteamcordPlayer[]>(body);
                success?.Invoke(players.SingleOrDefault());
            }, headers: _headers);
        }

        public void EnqueueSteamIds(IEnumerable<string> steamIds)
        {
            if (!steamIds.Any()) throw new ArgumentException();

            _httpRequestQueue.EnqueueRequest($"{_baseUri}/steam-groups/queue",
                body: JsonConvert.SerializeObject(steamIds),
                headers: _headers, type: HttpRequestType.Post);
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
        void EnqueueRequest(string uri, Action<int, string> callback = null, string body = null,
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

namespace Oxide.Plugins.SteamcordLogging
{
    public interface ILogger
    {
        void LogError(string message);
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
        void ProvisionRewards(IPlayer player, SteamcordPlayer steamcordPlayer);
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

        public void ProvisionRewards(IPlayer player, SteamcordPlayer steamcordPlayer)
        {
            if (steamcordPlayer == null)
            {
                _langService.Message(player, Message.ClaimNoRewards);

                foreach (var reward in _rewards)
                    _permissionsService.RemoveFromGroup(player, reward.Group);

                return;
            }

            var givenReward = false;
            foreach (var reward in _rewards)
                if (IsEligibleForAll(steamcordPlayer, reward, player.Id))
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

        private static bool IsEligibleForAll(SteamcordPlayer player, Reward reward, string steamId)
        {
            return reward.Requirements.All(requirement => IsEligible(player, requirement, steamId));
        }

        private static bool IsEligible(SteamcordPlayer player, Requirement requirement, string steamId)
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