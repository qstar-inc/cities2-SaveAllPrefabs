using Colossal.IO.AssetDatabase;
using Colossal.Logging;
using Game;
using Game.Modding;
using Game.SceneFlow;
using Game.UI.Editor;
using HarmonyLib;
using SaveAllPrefabs.Systems;

namespace SaveAllPrefabs
{
    public class Mod : IMod
    {
        public static ILog log = LogManager
            .GetLogger($"{nameof(SaveAllPrefabs)}.{nameof(Mod)}")
            .SetShowsErrorsInUI(false);
        private Harmony harmony;
        private UpdateSystem updateSystem;

        public void OnLoad(UpdateSystem updateSystem)
        {
            log.Info(nameof(OnLoad));

            if (GameManager.instance.modManager.TryGetExecutableAsset(this, out var asset))
                log.Info($"Current mod asset at {asset.path}");

            GameManager.instance.localizationManager.AddSource("en-US", new LocaleEN());

            this.updateSystem = updateSystem;

            updateSystem.UpdateAt<PatchedEditorHierarchyUISystem>(SystemUpdatePhase.UIUpdate);

            updateSystem.World.GetOrCreateSystemManaged<PatchedEditorHierarchyUISystem>().Enabled =
                true;
            updateSystem.World.GetOrCreateSystemManaged<EditorHierarchyUISystem>().Enabled = false;

            harmony = new Harmony("fergusq.save-all-prefabs");
            harmony.PatchAll();
        }

        public void OnDispose()
        {
            log.Info(nameof(OnDispose));
            harmony?.UnpatchAll();

            if (updateSystem != null)
            {
                updateSystem
                    .World.GetOrCreateSystemManaged<PatchedEditorHierarchyUISystem>()
                    .Enabled = false;
                updateSystem.World.GetOrCreateSystemManaged<EditorHierarchyUISystem>().Enabled =
                    true;
            }
        }
    }
}
