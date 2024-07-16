using System.Collections.Generic;
using Colossal;
using Colossal.IO.AssetDatabase;
using Game.Modding;
using Game.Settings;

namespace SaveAllPrefabs
{
    [FileLocation(nameof(SaveAllPrefabs))]
    [SettingsUIGroupOrder(kEnabledGroup)]
    [SettingsUIShowGroupName()]
    public class Setting : ModSetting
    {
        public const string kSection = "Main";

        public const string kEnabledGroup = "Enabled";

        public Setting(IMod mod) : base(mod)
        {

        }

        [SettingsUISection(kSection, kEnabledGroup)]
        public bool ModEnabled { get; set; }

        public override void SetDefaults()
        {
            ModEnabled = true;
        }
    }

    public class LocaleEN : IDictionarySource
    {
        private readonly Setting m_Setting;
        public LocaleEN(Setting setting)
        {
            m_Setting = setting;
        }
        public IEnumerable<KeyValuePair<string, string>> ReadEntries(IList<IDictionaryEntryError> errors, Dictionary<string, int> indexCounts)
        {
            return new Dictionary<string, string>
            {
                { m_Setting.GetSettingsLocaleID(), "Save All Prefabs" },
                { m_Setting.GetOptionTabLocaleID(Setting.kSection), "Main" },

                { m_Setting.GetOptionGroupLocaleID(Setting.kEnabledGroup), "Is enabled" },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.ModEnabled)), "Enabled mod" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.ModEnabled)), $"Uncheck to hide non-objects in the editor hierarchy" },

                { "Editor.NETWORKCONTAINER", "Networks" },
                { "Editor.AREACONTAINER", "Areas" },
                { "Editor.OTHERCONTAINER", "Other" },
            };
        }

        public void Unload()
        {

        }
    }
}
