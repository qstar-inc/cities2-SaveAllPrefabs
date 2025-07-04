using System.Reflection;
using Colossal.Reflection;
using Game.Prefabs;
using Game.UI.Editor;
using HarmonyLib;
using Unity.Entities;

namespace SaveAllPrefabs.Patches
{
    [HarmonyPatch(typeof(EditorAssetCategorySystem))]
    class EditorAssetCategorySystemPatches
    {
        [HarmonyPatch("GenerateTrackCategories")]
        [HarmonyPostfix]
        static void GenerateTrackCategories_Postfix(EditorAssetCategorySystem __instance)
        {
            MethodInfo GetEntityQuery_mi = typeof(EditorAssetCategorySystem).GetMethod(
                "GetEntityQuery",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.InvokeMethod,
                null,
                CallingConventions.HasThis,
                new[] { typeof(ComponentType[]) },
                null
            );
            MethodInfo AddCategory_mi = typeof(EditorAssetCategorySystem).GetMethod(
                "AddCategory",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.InvokeMethod,
                null,
                CallingConventions.HasThis,
                new[] { typeof(EditorAssetCategory), typeof(EditorAssetCategory) },
                null
            );

            EntityQuery netSectionEntityQuery = (EntityQuery)
                GetEntityQuery_mi.Invoke(
                    __instance,
                    new object[]
                    {
                        new ComponentType[] { ComponentType.ReadOnly<NetSectionData>() },
                    }
                );
            ;
            EditorAssetCategory netSectionCategory = new EditorAssetCategory
            {
                id = "Net Sections",
                entityQuery = netSectionEntityQuery,
                includeChildCategories = false,
            };

            AddCategory_mi.Invoke(__instance, new object[] { netSectionCategory, null });

            EntityQuery netPieceEntityQuery = (EntityQuery)
                GetEntityQuery_mi.Invoke(
                    __instance,
                    new object[] { new ComponentType[] { ComponentType.ReadOnly<NetPieceData>() } }
                );
            ;
            EditorAssetCategory netPieceCategory = new EditorAssetCategory
            {
                id = "Net Pieces",
                entityQuery = netPieceEntityQuery,
                includeChildCategories = false,
            };

            AddCategory_mi.Invoke(__instance, new object[] { netPieceCategory, null });

            EntityQuery netLaneEntityQuery = (EntityQuery)
                GetEntityQuery_mi.Invoke(
                    __instance,
                    new object[] { new ComponentType[] { ComponentType.ReadOnly<NetLaneData>() } }
                );
            ;
            EditorAssetCategory netLaneCategory = new EditorAssetCategory
            {
                id = "Net Lanes",
                entityQuery = netLaneEntityQuery,
                includeChildCategories = false,
            };

            AddCategory_mi.Invoke(__instance, new object[] { netLaneCategory, null });
        }
    }
}
