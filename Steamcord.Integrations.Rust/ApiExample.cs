// Requires: Steamcord

namespace Oxide.Plugins
{
    [Info("API Example", "Steamcord Team", "1.0.0")]
    internal class ApiExample : CovalencePlugin
    {
        [PluginReference] private Steamcord Steamcord;

        private void Loaded()
        {
            Steamcord.ApiClient.GetPlayerBySteamId("76561198117837537", player =>
            {
                Puts($"Found Steamcord player #{player.PlayerId}");
                // You have access to player.DiscordAccounts, player.SteamAccounts, ...
            }, (status, body) =>
            {
                // Additional error logic
            });
        }
    }
}