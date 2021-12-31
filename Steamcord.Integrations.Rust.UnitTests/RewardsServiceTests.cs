using NSubstitute;
using NUnit.Framework;
using Oxide.Core.Libraries.Covalence;
using Oxide.Plugins.SteamcordApi;
using Oxide.Plugins.SteamcordLang;
using Oxide.Plugins.SteamcordPermissions;
using Oxide.Plugins.SteamcordRewards;

namespace Steamcord.Integrations.Rust.UnitTests
{
    [TestFixture]
    public class RewardsServiceTests
    {
        [SetUp]
        public void SetUp()
        {
            _langService = Substitute.For<ILangService>();
            _permissionsService = Substitute.For<IPermissionsService>();
            _player = Substitute.For<IPlayer>();
            _player.Id.Returns("1");

            var rewards = new[]
            {
                new Reward(new[]
                {
                    Requirement.DiscordGuildMember,
                    Requirement.SteamGroupMember
                }, DiscordSteamMemberGroup),
                new Reward(new[]
                {
                    Requirement.DiscordGuildBooster
                }, DiscordBoosterGroup)
            };

            _rewardsService = new RewardsService(_langService, _permissionsService, rewards);
        }

        private const string DiscordSteamMemberGroup = "discord_steam_member";
        private const string DiscordBoosterGroup = "discord_booster";

        private ILangService _langService;
        private IPermissionsService _permissionsService;
        private IPlayer _player;
        private IRewardsService _rewardsService;

        [Test]
        public void ProvisionRewards_WhenScPlayerIsNull_MessagesPlayer()
        {
            _rewardsService.ProvisionRewards(_player, null);
            _langService.Received().Message(_player, Message.ClaimNoRewards);
        }

        [Test]
        public void ProvisionRewards_WhenPlayerIsEligibleForOneReward_AddsToGroupsAndMessagesPlayer()
        {
            var scPlayer = new Player
            {
                PlayerId = 1,
                DiscordAccounts = new[]
                {
                    new DiscordAccount
                    {
                        DiscordId = "1",
                        IsGuildMember = true,
                        IsGuildBooster = false
                    }
                },
                SteamAccounts = new[]
                {
                    new SteamAccount
                    {
                        SteamId = "1",
                        IsSteamGroupMember = true
                    }
                }
            };

            _rewardsService.ProvisionRewards(_player, scPlayer);

            _permissionsService.Received().AddToGroup(_player, DiscordSteamMemberGroup);
            _permissionsService.DidNotReceive().AddToGroup(_player, DiscordBoosterGroup);
            _langService.Received().Message(_player, Message.ClaimRewards);
        }

        [Test]
        public void ProvisionRewards_WhenPlayerIsEligibleForAllRewards_AddsToGroupsAndMessagesPlayer()
        {
            var scPlayer = new Player
            {
                PlayerId = 1,
                DiscordAccounts = new[]
                {
                    new DiscordAccount
                    {
                        DiscordId = "1",
                        IsGuildMember = true,
                        IsGuildBooster = true
                    }
                },
                SteamAccounts = new[]
                {
                    new SteamAccount
                    {
                        SteamId = "1",
                        IsSteamGroupMember = true
                    }
                }
            };

            _rewardsService.ProvisionRewards(_player, scPlayer);

            _permissionsService.Received().AddToGroup(_player, DiscordSteamMemberGroup);
            _permissionsService.Received().AddToGroup(_player, DiscordBoosterGroup);
            _permissionsService.DidNotReceiveWithAnyArgs().RemoveFromGroup(default, default);
            _langService.Received().Message(_player, Message.ClaimRewards);
        }

        [Test]
        public void ProvisionRewards_WhenPlayerIsNotEligible_MessagesPlayer()
        {
            var scPlayer = new Player
            {
                PlayerId = 1,
                DiscordAccounts = new[]
                {
                    new DiscordAccount
                    {
                        DiscordId = "1"
                    }
                },
                SteamAccounts = new[]
                {
                    new SteamAccount
                    {
                        SteamId = "1"
                    }
                }
            };

            _rewardsService.ProvisionRewards(_player, scPlayer);

            _permissionsService.DidNotReceiveWithAnyArgs().AddToGroup(default, default);
            _permissionsService.Received().RemoveFromGroup(_player, DiscordSteamMemberGroup);
            _permissionsService.Received().RemoveFromGroup(_player, DiscordBoosterGroup);
            _langService.Received().Message(_player, Message.ClaimNoRewards);
        }
    }
}