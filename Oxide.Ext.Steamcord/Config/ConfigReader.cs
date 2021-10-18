using System.IO;
using Newtonsoft.Json;

namespace Oxide.Ext.Steamcord.Config
{
    internal class ConfigReader
    {
        private readonly string _path;

        public ConfigReader(string path)
        {
            _path = path;
        }

        public SteamcordConfig GetOrWriteConfig()
        {
            try
            {
                return JsonConvert.DeserializeObject<SteamcordConfig>(File.ReadAllText(_path));
            }
            catch (FileNotFoundException ex)
            {
                return WriteDefaultConfig();
            }
        }

        private SteamcordConfig WriteDefaultConfig()
        {
            var serializer = new JsonSerializer();
            var defaultConfig = new SteamcordConfig();
            using (var streamWriter = new StreamWriter(_path))
            using (var jsonWriter = new JsonTextWriter(streamWriter))
            {
                serializer.Serialize(jsonWriter, defaultConfig);
            }

            return defaultConfig;
        }
    }
}