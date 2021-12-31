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
        public void GetPlayerBySteamId_WhenApiReturnsOk_InvokesSuccessCallback(int status)
        {
            _httpRequestQueue.WhenForAnyArgs(x => x.PushRequest(default))
                .Do(x =>
                {
                    var callback = x.Arg<Action<int, string>>();
                    callback?.Invoke(status, ResponseBody);
                });

            var successCalled = false;
            var errorCalled = false;
            _steamcordApiClient.GetPlayerBySteamId("76561198117837537", player => successCalled = true,
                (responseStatus, body) => errorCalled = true);

            Assert.AreEqual(status == 200, successCalled);
            Assert.AreEqual(status != 200, errorCalled);
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

            _steamcordApiClient.GetPlayerBySteamId("76561198117837537", player =>
            {
                Assert.IsNotNull(player);
                Assert.AreEqual(player.PlayerId, 1);
                Assert.AreEqual(player.DiscordAccounts.Single().DiscordId, "304797177538936832");
                Assert.AreEqual(player.SteamAccounts.Single().SteamId, "76561198117837537");
            });
        }

        [Test]
        public void PushSteamIdsOntoQueue_WhenSteamIdsIsEmpty_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() =>
                _steamcordApiClient.PushSteamIdsOntoQueue(Array.Empty<string>()));
        }
    }
}