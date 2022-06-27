// Copyright 2022 Steamcord LLC

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

        private const string DiscordSteamMemberGroup = "discord-steam-member";
        private const string DiscordBoosterGroup = "discord-booster";

        private ILangService _langService;
        private IPermissionsService _permissionsService;
        private IPlayer _player;
        private IRewardsService _rewardsService;

        [Test]
        public void ProvisionRewards_WhenScPlayerIsNull_MessagesPlayer()
        {
            // Act
            _rewardsService.ProvisionRewards(_player, null);
            
            // Assert
            _langService.Received().Message(_player, Message.ClaimNoRewards);
        }
        
        [Test]
        public void ProvisionRewards_WhenScPlayerIsNull_RemovesFromGroups()
        {
            // Act
            _rewardsService.ProvisionRewards(_player, null);
            
            // Assert
            _permissionsService.Received().RemoveFromGroup(_player, DiscordSteamMemberGroup);
            _permissionsService.Received().RemoveFromGroup(_player, DiscordBoosterGroup);
        }

        [Test]
        public void ProvisionRewards_WhenPlayerIsEligibleForOneReward_AddsToGroup()
        {
            // Arrange
            var scPlayer = new SteamcordPlayer
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

            // Act
            _rewardsService.ProvisionRewards(_player, scPlayer);

            // Assert
            _permissionsService.Received().AddToGroup(_player, DiscordSteamMemberGroup);
            _permissionsService.DidNotReceive().AddToGroup(_player, DiscordBoosterGroup);
        }
        
        [Test]
        public void ProvisionRewards_WhenPlayerIsEligibleForOneReward_RemovesFromGroup()
        {
            // Arrange
            var scPlayer = new SteamcordPlayer
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

            // Act
            _rewardsService.ProvisionRewards(_player, scPlayer);

            // Assert
            _permissionsService.Received().RemoveFromGroup(_player, DiscordBoosterGroup);
            _permissionsService.DidNotReceive().RemoveFromGroup(_player, DiscordSteamMemberGroup);
        }
        
        [Test]
        public void ProvisionRewards_WhenPlayerIsEligibleForOneReward_MessagesPlayer()
        {
            // Arrange
            var scPlayer = new SteamcordPlayer
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

            // Act
            _rewardsService.ProvisionRewards(_player, scPlayer);

            // Assert
            _langService.Received().Message(_player, Message.ClaimRewards);
        }

        [Test]
        public void ProvisionRewards_WhenPlayerIsEligibleForAllRewards_AddsToGroups()
        {
            // Arrange
            var scPlayer = new SteamcordPlayer
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

            // Act
            _rewardsService.ProvisionRewards(_player, scPlayer);

            // Assert
            _permissionsService.Received().AddToGroup(_player, DiscordSteamMemberGroup);
            _permissionsService.Received().AddToGroup(_player, DiscordBoosterGroup);
            _permissionsService.DidNotReceiveWithAnyArgs().RemoveFromGroup(default, default);
        }
        
        [Test]
        public void ProvisionRewards_WhenPlayerIsEligibleForAllRewards_MessagesPlayer()
        {
            // Arrange
            var scPlayer = new SteamcordPlayer
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

            // Act
            _rewardsService.ProvisionRewards(_player, scPlayer);

            // Assert
            _langService.Received().Message(_player, Message.ClaimRewards);
        }

        [Test]
        public void ProvisionRewards_WhenPlayerIsNotEligible_MessagesPlayer()
        {
            // Arrange
            var scPlayer = new SteamcordPlayer
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

            // Act
            _rewardsService.ProvisionRewards(_player, scPlayer);

            // Assert
            _langService.Received().Message(_player, Message.ClaimNoRewards);
        }
    }
}