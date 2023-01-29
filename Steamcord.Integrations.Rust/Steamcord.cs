// Copyright 2023 Steamcord LLC

// #define DEBUG

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries;
using Oxide.Plugins.SteamcordApi;
using Oxide.Plugins.SteamcordHttp;
using Oxide.Plugins.SteamcordLogging;
using Oxide.Plugins.SteamcordPermissions;
using Oxide.Plugins.SteamcordRewards;

namespace Oxide.Plugins
{
    [Info("Steamcord", "Steamcord Team", "1.2.0")]
    public class Steamcord : CovalencePlugin
    {
        private const string BaseUri = "https://api.steamcord.io";
        private static Steamcord _instance;

        private Configuration _config;

        public Steamcord()
        {
            _instance = this;
        }

        public IRewardsService RewardsService { get; set; }
        public ISteamcordApiClient ApiClient { get; set; }

        private void Init()
        {
            ApiClient =
                new SteamcordApiClient(_config.Api.Token, BaseUri, _config.IntegrationId,
                    new HttpRequestQueue(new Logger()));

            RewardsService = new RewardsService(new PermissionsService(), ApiClient);

            ApiClient.GetLatestVersion(version =>
            {
                if (version > new Version(Version.ToString()))
                    Puts($@"A newer version ({version}) of this plugin is available!
Download it at https://steamcord.io/dashboard/downloads");
            });
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

            if (!_config.IntegrationId.HasValue) return;

            timer.Every(60, () => ApiClient.GetDeferredActions(actions =>
            {
                var deferredActions = actions as SteamcordAction[] ?? actions.ToArray();

                if (!deferredActions.Any()) return;

                RewardsService.ProvisionDeferredActions(deferredActions);
            }));
        }

        private void Unload()
        {
            // Oxide/uMod requirement
            _instance = null;
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
                        return "Received an unauthorized response, is your API token correct?";
                    case 403:
                        return "Received a forbidden response, is your subscription active?";
                    case 404:
                        return "Received a not found response, is your integration ID correct?";
                    case 429:
                        return "Received a rate limit response.";
                    default:
                        return $"Received unexpected status code: {status}.";
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
                    case HttpRequestType.Put:
                        return RequestMethod.PUT;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(requestType), requestType, null);
                }
            }
        }

        #endregion

        #region Permissions

        private class PermissionsService : IPermissionsService
        {
            public void AddToGroup(string steamId, string group)
            {
                _instance.permission.AddUserGroup(steamId, group);

#if DEBUG
                _instance.Puts($"Added player \"{steamId}\" to group \"{group}\".");
#endif
            }

            public void RemoveFromGroup(string steamId, string group)
            {
                _instance.permission.RemoveUserGroup(steamId, group);

#if DEBUG
                _instance.Puts($"Removed player \"{steamId}\" from group \"{group}\".");
#endif
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
            public ApiOptions Api { get; } = new ApiOptions
            {
                Token = "<your api token>"
            };

            public int? IntegrationId { get; } = null;

            public bool UpdateSteamGroups { get; } = true;

            public class ApiOptions
            {
                public string Token { get; set; }
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

    public class SteamcordAction
    {
        public int Id { get; set; }
        public string DefinitionName { get; set; }
        public SteamcordPlayer Player { get; set; }
        public Dictionary<string, string> Arguments { get; set; }
        public DateTime CreatedDate { get; set; }
    }

    #region Release

    public class Release
    {
        public string Repository { get; set; }
        public string Version { get; set; }
    }

    #endregion

    public interface ISteamcordApiClient
    {
        /// <summary>
        ///     Gets the latest plugin version from the Steamcord API and invokes the provided callback.
        /// </summary>
        /// <param name="callback"></param>
        void GetLatestVersion(Action<Version> callback);

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

        /// <summary>
        ///     See <see href="https://docs.steamcord.io/api-reference/action-queue.html#get-deferred-actions">the docs</see>.
        /// </summary>
        /// <param name="callback"></param>
        void GetDeferredActions(Action<IEnumerable<SteamcordAction>> callback);

        /// <summary>
        ///     See
        ///     <see href="https://docs.steamcord.io/api-reference/action-queue.html#acknowledge-deferred-actions">the docs</see>.
        /// </summary>
        /// <param name="actions"></param>
        void AckDeferredActions(IEnumerable<int> actions);
    }

    public class SteamcordApiClient : ISteamcordApiClient
    {
        private readonly string _baseUri;
        private readonly Dictionary<string, string> _headers;
        private readonly IHttpRequestQueue _httpRequestQueue;
        private readonly int? _integrationId;

        public SteamcordApiClient(string apiToken, string baseUri, int? integrationId,
            IHttpRequestQueue httpRequestQueue)
        {
            _headers = new Dictionary<string, string>
            {
                ["Authorization"] = $"Bearer {apiToken}",
                ["Content-Type"] = "application/json"
            };

            _baseUri = baseUri;
            _integrationId = integrationId;
            _httpRequestQueue = httpRequestQueue;
        }

        public void GetLatestVersion(Action<Version> callback)
        {
            _httpRequestQueue.EnqueueRequest($"{_baseUri}/releases/latest", (status, body) =>
            {
                if (status != 200) return;

                var releases = JsonConvert.DeserializeObject<Release[]>(body);
                var rustIntegration = releases.SingleOrDefault(release => release.Repository == "steamcord-rust");

                if (rustIntegration == null) return;

                callback(new Version(rustIntegration.Version));
            });
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

        public void GetDeferredActions(Action<IEnumerable<SteamcordAction>> callback)
        {
            _httpRequestQueue.EnqueueRequest($"{_baseUri}/integrations/{_integrationId}/queue", (status, body) =>
            {
                if (status != 200) return;

                var actions = JsonConvert.DeserializeObject<SteamcordAction[]>(body);
                callback?.Invoke(actions);
            }, headers: _headers);
        }

        public void AckDeferredActions(IEnumerable<int> actionIds)
        {
            _httpRequestQueue.EnqueueRequest($"{_baseUri}/integrations/{_integrationId}/ack",
                body: JsonConvert.SerializeObject(actionIds), headers: _headers, type: HttpRequestType.Put);
        }
    }
}

namespace Oxide.Plugins.SteamcordHttp
{
    public enum HttpRequestType
    {
        Get,
        Post,
        Put
    }

    public interface IHttpRequestQueue
    {
        void EnqueueRequest(string uri, Action<int, string> callback = null, string body = null,
            Dictionary<string, string> headers = null, HttpRequestType type = HttpRequestType.Get);
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
        void AddToGroup(string steamId, string group);
        void RemoveFromGroup(string steamId, string group);
    }
}

namespace Oxide.Plugins.SteamcordRewards
{
    public interface IRewardsService
    {
        void ProvisionDeferredActions(IEnumerable<SteamcordAction> actions);
    }

    public class RewardsService : IRewardsService
    {
        private readonly ISteamcordApiClient _apiClient;
        private readonly IPermissionsService _permissionsService;

        public RewardsService(IPermissionsService permissionsService, ISteamcordApiClient apiClient)
        {
            _permissionsService = permissionsService;
            _apiClient = apiClient;
        }

        public void ProvisionDeferredActions(IEnumerable<SteamcordAction> actions)
        {
            var provisionedActionIds = new HashSet<int>();

            foreach (var action in actions.OrderBy(action => action.CreatedDate))
            {
                switch (action.DefinitionName)
                {
                    case "addOxideGroup":
                        foreach (var steamAccount in action.Player.SteamAccounts)
                            _permissionsService.AddToGroup(steamAccount.SteamId, action.Arguments["groupName"]);
                        break;
                    case "removeOxideGroup":
                        foreach (var steamAccount in action.Player.SteamAccounts)
                            _permissionsService.RemoveFromGroup(steamAccount.SteamId, action.Arguments["groupName"]);
                        break;
                }

                provisionedActionIds.Add(action.Id);
            }

            _apiClient.AckDeferredActions(provisionedActionIds);
        }
    }
}