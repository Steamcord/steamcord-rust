using System;
using System.IO;
using System.Reflection;
using Oxide.Core;
using Oxide.Core.Extensions;
using Oxide.Ext.Steamcord.Config;

namespace Oxide.Ext.Steamcord
{
    public class SteamcordExtension : Extension
    {
        public SteamcordExtension(ExtensionManager manager) : base(manager)
        {
        }

        public override string Name => "Steamcord Extension";
        public override string Author => "Steamcord Team";

        public override VersionNumber Version =>
            new VersionNumber(AssemblyVersion.Major, AssemblyVersion.Minor, AssemblyVersion.Build);

        private static Version AssemblyVersion => Assembly.GetExecutingAssembly().GetName().Version;

        public override void Load()
        {
            var path = Path.Combine(Interface.Oxide.ConfigDirectory, nameof(Steamcord));
            var config = new ConfigReader(path).GetOrWriteConfig();
        }
    }
}