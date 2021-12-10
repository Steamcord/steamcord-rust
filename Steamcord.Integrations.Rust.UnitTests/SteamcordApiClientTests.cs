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
            "[{\"playerId\":1,\"discordAccount\":{\"discordId\":\"304797177538936832\",\"username\":\"Jacob#3500\"},\"steamAccount\":{\"steamId\":\"76561198117837537\"},\"createdDate\":\"2021-10-31 17:34:46.896816\",\"modifiedDate\":\"2021-10-31 17:45:49.823991\"}]";

        private IHttpRequestQueue _httpRequestQueue;
        private ISteamcordApiClient _steamcordApiClient;

        [TestCase(200)]
        [TestCase(403)]
        public void GetPlayers_WhenApiReturnsOk_RaisesPlayersReceivedEvent(int status)
        {
            _httpRequestQueue.WhenForAnyArgs(x => x.PushRequest(default))
                .Do(x =>
                {
                    var callback = x.Arg<Action<int, string>>();
                    callback?.Invoke(status, ResponseBody);
                });

            var wasRaised = false;
            _steamcordApiClient.PlayersReceived += (sender, e) => wasRaised = true;
            _steamcordApiClient.GetPlayers();

            Assert.AreEqual(status == 200, wasRaised);
        }

        [Test]
        public void GetPlayers_WhenApiReturnsOnePlayer_RaisesPlayersReceivedEventWithPlayer()
        {
            _httpRequestQueue.WhenForAnyArgs(x => x.PushRequest(default))
                .Do(x =>
                {
                    var callback = x.Arg<Action<int, string>>();
                    callback?.Invoke(200, ResponseBody);
                });

            _steamcordApiClient.PlayersReceived += (sender, e) =>
            {
                Assert.IsNotEmpty(e.Players);
                var player = e.Players.First();
                Assert.AreEqual(player.PlayerId, 1);
                Assert.AreEqual(player.DiscordAccount.DiscordId, "304797177538936832");
                Assert.AreEqual(player.SteamAccount.SteamId, "76561198117837537");
            };
        }

        [Test]
        public void PushSteamIdsOntoQueue_WhenSteamIdsIsEmpty_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                _steamcordApiClient.PushSteamIdsOntoQueue(Array.Empty<string>()));
        }
    }
}