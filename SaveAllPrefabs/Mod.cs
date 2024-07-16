using Colossal.IO.AssetDatabase;
using Colossal.Logging;
using Game;
using Game.Modding;
using Game.SceneFlow;
using Game.UI.Editor;
using SaveAllPrefabs.Systems;

namespace SaveAllPrefabs
{
    public class Mod : IMod
    {
        public static ILog log = LogManager.GetLogger($"{nameof(SaveAllPrefabs)}.{nameof(Mod)}").SetShowsErrorsInUI(false);
        private Setting m_Setting;

        public void OnLoad(UpdateSystem updateSystem)
        {
            log.Info(nameof(OnLoad));

            if (GameManager.instance.modManager.TryGetExecutableAsset(this, out var asset))
                log.Info($"Current mod asset at {asset.path}");

            m_Setting = new Setting(this);
            m_Setting.RegisterInOptionsUI();
            GameManager.instance.localizationManager.AddSource("en-US", new LocaleEN(m_Setting));

            updateSystem.UpdateAt<PatchedEditorHierarchyUISystem>(SystemUpdatePhase.UIUpdate);

            AssetDatabase.global.LoadSettings(nameof(SaveAllPrefabs), m_Setting, new Setting(this));

            void updateEnabledSystems(Game.Settings.Setting _setting)
            {
                updateSystem.World.GetExistingSystemManaged<PatchedEditorHierarchyUISystem>().Enabled = m_Setting.ModEnabled;
                updateSystem.World.GetExistingSystemManaged<EditorHierarchyUISystem>().Enabled = !m_Setting.ModEnabled;
            }

            updateEnabledSystems(null);

            m_Setting.onSettingsApplied += updateEnabledSystems;
        }

        public void OnDispose()
        {
            log.Info(nameof(OnDispose));
            if (m_Setting != null)
            {
                m_Setting.UnregisterInOptionsUI();
                m_Setting = null;
            }
        }
    }
}
