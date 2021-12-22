using System;
using System.Linq;
using NSubstitute;
using NUnit.Framework;
using Oxide.Plugins.SteamcordApi;
using Oxide.Plugins.SteamcordHttp;

namespace Steamcord.Integrations.Rust.UnitTests
{
    [TestFixture]
    public class SteamcordApiClientTests
    {
        [SetUp]
        public void SetUp()
        {
            _httpRequestQueue = Substitute.For<IHttpRequestQueue>();

            _steamcordApiClient = new SteamcordApiClient(ApiToken, BaseUri, _httpRequestQueue);
        }

        private const string ApiToken = "apiToken";
        private const string BaseUri = "baseUri";

        private const string ResponseBody =
            "[{\"playerId\":1,\"discordAccounts\":[{\"discordId\":\"304797177538936832\",\"username\":\"Jacob#3500\"}],\"steamAccounts\":[{\"steamId\":\"76561198117837537\"}],\"createdDate\":\"2021-10-31 17:34:46.896816\",\"modifiedDate\":\"2021-10-31 17:45:49.823991\"}]";

        private IHttpRequestQueue _httpRequestQueue;
        private ISteamcordApiClient _steamcordApiClient;

        [TestCase(200)]
        [TestCase(403)]
        public void GetPlayerBySteamId_WhenApiReturnsOk_RaisesPlayerReceivedEvent(int status)
        {
            _httpRequestQueue.WhenForAnyArgs(x => x.PushRequest(default))
                .Do(x =>
                {
                    var callback = x.Arg<Action<int, string>>();
                    callback?.Invoke(status, ResponseBody);
                });

            var wasRaised = false;
            _steamcordApiClient.PlayerReceived += (sender, e) => wasRaised = true;
            _steamcordApiClient.GetPlayerBySteamId("76561198117837537");

            Assert.AreEqual(status == 200, wasRaised);
        }

        [Test]
        public void GetPlayers_WhenApiReturnsOnePlayer_RaisesPlayerReceivedEventWithPlayer()
        {
            _httpRequestQueue.WhenForAnyArgs(x => x.PushRequest(default))
                .Do(x =>
                {
                    var callback = x.Arg<Action<int, string>>();
                    callback?.Invoke(200, ResponseBody);
                });

            _steamcordApiClient.PlayerReceived += (sender, e) =>
            {
                Assert.IsNotNull(e.Player);
                Assert.AreEqual(e.Player.PlayerId, 1);
                Assert.AreEqual(e.Player.DiscordAccounts.Single().DiscordId, "304797177538936832");
                Assert.AreEqual(e.Player.SteamAccounts.Single().SteamId, "76561198117837537");
            };

            _steamcordApiClient.GetPlayerBySteamId("76561198117837537");
        }

        [Test]
        public void PushSteamIdsOntoQueue_WhenSteamIdsIsEmpty_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() =>
                _steamcordApiClient.PushSteamIdsOntoQueue(Array.Empty<string>()));
        }
    }
}