/*
 * Modified version of the decompiled source of EditorHierarchySystem
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Colossal.Entities;
using Colossal.PSI.Common;
using Colossal.Serialization.Entities;
using Colossal.UI.Binding;
using Game;
using Game.Achievements;
using Game.Common;
using Game.Prefabs;
using Game.Rendering;
using Game.Settings;
using Game.Tools;
using Game.UI;
using Game.UI.Editor;
using Game.UI.Localization;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.Internal;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine.Scripting;

namespace SaveAllPrefabs.Systems
{
    public partial class PatchedEditorHierarchyUISystem : UISystemBase
    {
        private enum CameraMode
        {
            Default,
            FirstPerson,
            Orbit,
        }

        [BurstCompile]
        public struct ObjectHierarchyJob : IJobChunk
        {
            [ReadOnly]
            public EntityTypeHandle m_EntityType;

            [ReadOnly]
            public ComponentTypeHandle<PrefabRef> m_PrefabRefType;

            [ReadOnly]
            public BufferLookup<SubMesh> m_SubMeshes;

            [ReadOnly]
            public NativeParallelHashSet<ItemId> m_ExpandedIds;

            public NativeList<HierarchyItem> m_Hierarchy;

            [ReadOnly]
            public ItemType m_ContainerType;

            [ReadOnly]
            public ItemType m_ItemType;

            public void Execute(
                in ArchetypeChunk chunk,
                int unfilteredChunkIndex,
                bool useEnabledMask,
                in v128 chunkEnabledMask
            )
            {
                NativeArray<Entity> nativeArray = chunk.GetNativeArray(m_EntityType);
                NativeArray<PrefabRef> nativeArray2 = chunk.GetNativeArray(ref m_PrefabRefType);
                for (int i = 0; i < chunk.Count; i++)
                {
                    Entity prefab = nativeArray2[i].m_Prefab;
                    ItemId itemId = default(ItemId);
                    itemId.type = m_ItemType;
                    itemId.entity = nativeArray[i];
                    ItemId itemId2 = itemId;
                    bool flag = m_SubMeshes.HasBuffer(prefab);
                    bool flag2 = flag && m_ExpandedIds.Contains(itemId2);
                    ref NativeList<HierarchyItem> hierarchy = ref m_Hierarchy;
                    HierarchyItem value = new HierarchyItem
                    {
                        id = itemId2,
                        level = 1,
                        expandable = flag,
                        expanded = flag2,
                        selectable = true,
                    };
                    hierarchy.Add(in value);
                    if (flag2 && m_SubMeshes.TryGetBuffer(prefab, out var bufferData))
                    {
                        for (int j = 0; j < bufferData.Length; j++)
                        {
                            ref NativeList<HierarchyItem> hierarchy2 = ref m_Hierarchy;
                            value = new HierarchyItem
                            {
                                id = new ItemId
                                {
                                    type = ItemType.SubMesh,
                                    entity = nativeArray[i],
                                    subIndex = j,
                                },
                                level = 2,
                                selectable = true,
                            };
                            hierarchy2.Add(in value);
                        }
                    }
                }
            }

            void IJobChunk.Execute(
                in ArchetypeChunk chunk,
                int unfilteredChunkIndex,
                bool useEnabledMask,
                in v128 chunkEnabledMask
            )
            {
                Execute(in chunk, unfilteredChunkIndex, useEnabledMask, in chunkEnabledMask);
            }
        }

        public class PanelItem
        {
            public ItemType type;

            public byte level;

            public IEditorPanel panel;

            public PanelItem(ItemType type, byte level, IEditorPanel panel)
            {
                this.type = type;
                this.level = level;
                this.panel = panel;
            }
        }

        public class Viewport : IJsonWritable
        {
            public int startIndex;

            public List<ViewportItem> items = new List<ViewportItem>();

            public void Write(IJsonWriter writer)
            {
                writer.TypeBegin(GetType().FullName);
                writer.PropertyName("startIndex");
                writer.Write(startIndex);
                writer.PropertyName("items");
                writer.Write((IList<ViewportItem>)items);
                writer.TypeEnd();
            }
        }

        public struct ViewportItem : IJsonWritable
        {
            public ItemId id;

            public byte level;

            public bool expandable;

            public bool expanded;

            public LocalizedString name;

            public bool selectable;

            public bool saveable;

            public void Write(IJsonWriter writer)
            {
                writer.TypeBegin(GetType().FullName);
                writer.PropertyName("id");
                writer.Write(id);
                writer.PropertyName("level");
                writer.Write(level);
                writer.PropertyName("expandable");
                writer.Write(expandable);
                writer.PropertyName("expanded");
                writer.Write(expanded);
                writer.PropertyName("name");
                writer.Write(name);
                writer.PropertyName("selectable");
                writer.Write(selectable);
                writer.PropertyName("saveable");
                writer.Write(saveable);
                writer.TypeEnd();
            }

            public bool EqualsHierarchy(HierarchyItem other)
            {
                if (
                    id == other.id
                    && level == other.level
                    && expandable == other.expandable
                    && expanded == other.expanded
                )
                {
                    return selectable == other.selectable;
                }
                return false;
            }
        }

        public struct HierarchyItem : IComparable<HierarchyItem>
        {
            public ItemId id;

            public byte level;

            public bool expandable;

            public bool expanded;

            public bool selectable;

            public int CompareTo(HierarchyItem other)
            {
                return id.CompareTo(other.id);
            }
        }

        public struct ItemId : IJsonWritable, IJsonReadable, IEquatable<ItemId>, IComparable<ItemId>
        {
            public ItemType type;

            public Entity entity;

            public int subIndex;

            public bool isContainer =>
                type == ItemType.ObjectContainer
                || type == ItemType.AreaContainer
                || type == ItemType.NetworkContainer
                || type == ItemType.OtherContainer;

            public ItemId(ItemType type, Entity entity = default(Entity), int subIndex = 0)
            {
                this.type = type;
                this.entity = entity;
                this.subIndex = subIndex;
            }

            public void Write(IJsonWriter writer)
            {
                writer.TypeBegin(GetType().FullName);
                writer.PropertyName("type");
                writer.Write((int)type);
                writer.PropertyName("entity");
                writer.Write(entity);
                writer.PropertyName("subIndex");
                writer.Write(subIndex);
                writer.TypeEnd();
            }

            public void Read(IJsonReader reader)
            {
                reader.ReadMapBegin();
                reader.ReadProperty("type");
                reader.Read(out int value);
                type = (ItemType)value;
                reader.ReadProperty("entity");
                reader.Read(out entity);
                reader.ReadProperty("subIndex");
                reader.Read(out subIndex);
                reader.ReadMapEnd();
            }

            public bool Equals(ItemId other)
            {
                if (type == other.type && entity.Equals(other.entity))
                {
                    return subIndex == other.subIndex;
                }
                return false;
            }

            public override bool Equals(object obj)
            {
                if (obj is ItemId other)
                {
                    return Equals(other);
                }
                return false;
            }

            public override int GetHashCode()
            {
                return ((((int)type * 397) ^ entity.GetHashCode()) * 397) ^ subIndex;
            }

            public static bool operator ==(ItemId left, ItemId right)
            {
                return left.Equals(right);
            }

            public static bool operator !=(ItemId left, ItemId right)
            {
                return !left.Equals(right);
            }

            public int CompareTo(ItemId other)
            {
                int num = entity.CompareTo(other.entity);
                if (num != 0)
                {
                    return num;
                }
                byte b = (byte)type;
                int num2 = b.CompareTo((byte)other.type);
                if (num2 != 0)
                {
                    return num2;
                }
                return subIndex.CompareTo(other.subIndex);
            }
        }

        public enum ItemType : byte
        {
            None,
            Map,
            Climate,
            Water,
            Resources,
            ObjectContainer,
            Object,
            SubMesh,

            NetworkContainer = 100,
            AreaContainer,
            OtherContainer,
        }

        [NoAlias]
        [BurstCompile]
        private struct EditorHierarchyUISystem_4CF10000_LambdaJob_0_Job : IJob
        {
            public NativeList<HierarchyItem> hierarchy;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void OriginalLambdaBody()
            {
                hierarchy.Sort();
            }

            public void Execute()
            {
                OriginalLambdaBody();
            }
        }

        private struct TypeHandle
        {
            [ReadOnly]
            public EntityTypeHandle __Unity_Entities_Entity_TypeHandle;

            [ReadOnly]
            public ComponentTypeHandle<PrefabRef> __Game_Prefabs_PrefabRef_RO_ComponentTypeHandle;

            [ReadOnly]
            public BufferLookup<SubMesh> __Game_Prefabs_SubMesh_RO_BufferLookup;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void __AssignHandles(ref SystemState state)
            {
                __Unity_Entities_Entity_TypeHandle = state.GetEntityTypeHandle();
                __Game_Prefabs_PrefabRef_RO_ComponentTypeHandle =
                    state.GetComponentTypeHandle<PrefabRef>(isReadOnly: true);
                __Game_Prefabs_SubMesh_RO_BufferLookup = state.GetBufferLookup<SubMesh>(
                    isReadOnly: true
                );
            }
        }

        private const string kGroup = "editorHierarchy";

        public Action<Entity> onSave;

        public Action<Entity> onBulldoze;

        private PrefabSystem m_PrefabSystem;

        private ToolSystem m_ToolSystem;

        private EditorToolUISystem m_EditorToolUISystem;

        private EditorPanelUISystem m_EditorPanelUISystem;

        private CameraUpdateSystem m_CameraUpdateSystem;

        private EntityQuery m_ObjectQuery,
            m_NetworkQuery,
            m_AreaQuery,
            m_OtherQuery;

        private EntityQuery m_ModifiedQuery;

        private GetterValueBinding<Viewport> m_ViewportBinding;

        private NativeList<HierarchyItem> m_Hierarchy;

        private NativeParallelHashSet<ItemId> m_ExpandedIds;

        private int m_TotalCount;

        private ItemId m_SelectedId;

        private Viewport m_Viewport;

        private int m_NextViewportStartIndex;

        private int m_NextViewportEndIndex;

        private bool m_Dirty;

        private ValueBinding<int> m_CameraMode;

        private TypeHandle __TypeHandle;

        public override GameMode gameMode => GameMode.Editor;

        public List<PanelItem> panelItems { get; private set; }

        [Preserve]
        protected override void OnCreate()
        {
            base.OnCreate();
            m_PrefabSystem = base.World.GetOrCreateSystemManaged<PrefabSystem>();
            m_ToolSystem = base.World.GetOrCreateSystemManaged<ToolSystem>();
            m_EditorToolUISystem = base.World.GetOrCreateSystemManaged<EditorToolUISystem>();
            m_EditorPanelUISystem = base.World.GetOrCreateSystemManaged<EditorPanelUISystem>();
            m_CameraUpdateSystem = base.World.GetOrCreateSystemManaged<CameraUpdateSystem>();
            m_ObjectQuery = GetEntityQuery(
                new EntityQueryDesc
                {
                    All = new ComponentType[2]
                    {
                        ComponentType.ReadOnly<PrefabRef>(),
                        ComponentType.ReadOnly<Game.Objects.Object>(),
                    },
                    None = new ComponentType[3]
                    {
                        ComponentType.ReadOnly<Deleted>(),
                        ComponentType.ReadOnly<Owner>(),
                        ComponentType.ReadOnly<Temp>(),
                    },
                }
            );
            m_NetworkQuery = GetEntityQuery(
                new EntityQueryDesc
                {
                    All = new ComponentType[2]
                    {
                        ComponentType.ReadOnly<PrefabRef>(),
                        ComponentType.ReadOnly<Game.Net.Curve>(),
                    },
                    None = new ComponentType[3]
                    {
                        ComponentType.ReadOnly<Deleted>(),
                        ComponentType.ReadOnly<Owner>(),
                        ComponentType.ReadOnly<Temp>(),
                    },
                }
            );
            m_AreaQuery = GetEntityQuery(
                new EntityQueryDesc
                {
                    All = new ComponentType[2]
                    {
                        ComponentType.ReadOnly<PrefabRef>(),
                        ComponentType.ReadOnly<Game.Areas.Area>(),
                    },
                    None = new ComponentType[3]
                    {
                        ComponentType.ReadOnly<Deleted>(),
                        ComponentType.ReadOnly<Owner>(),
                        ComponentType.ReadOnly<Temp>(),
                    },
                }
            );
            m_OtherQuery = GetEntityQuery(
                new EntityQueryDesc
                {
                    All = new ComponentType[1] { ComponentType.ReadOnly<PrefabRef>() },
                    None = new ComponentType[6]
                    {
                        ComponentType.ReadOnly<Deleted>(),
                        ComponentType.ReadOnly<Owner>(),
                        ComponentType.ReadOnly<Temp>(),
                        ComponentType.ReadOnly<Game.Objects.Object>(),
                        ComponentType.ReadOnly<Game.Net.Curve>(),
                        ComponentType.ReadOnly<Game.Areas.Area>(),
                    },
                }
            );
            m_ModifiedQuery = GetEntityQuery(
                new EntityQueryDesc
                {
                    All = new ComponentType[1]
                    {
                        ComponentType.ReadOnly<PrefabRef>(),
                        //ComponentType.ReadOnly<Game.Objects.Object>()
                    },
                    Any = new ComponentType[3]
                    {
                        ComponentType.ReadOnly<Created>(),
                        ComponentType.ReadOnly<Updated>(),
                        ComponentType.ReadOnly<Deleted>(),
                    },
                    None = new ComponentType[2]
                    {
                        ComponentType.ReadOnly<Owner>(),
                        ComponentType.ReadOnly<Temp>(),
                    },
                }
            );
            AddUpdateBinding(
                new GetterValueBinding<int>(
                    "editorHierarchy",
                    "width",
                    () => (SharedSettings.instance?.editor)?.hierarchyWidth ?? 350
                )
            );
            AddUpdateBinding(
                new GetterValueBinding<int>("editorHierarchy", "totalCount", () => m_TotalCount)
            );
            AddUpdateBinding(
                new GetterValueBinding<ItemId>(
                    "editorHierarchy",
                    "selectedId",
                    () => m_SelectedId,
                    new ValueWriter<ItemId>()
                )
            );
            AddBinding(
                m_ViewportBinding = new GetterValueBinding<Viewport>(
                    "editorHierarchy",
                    "viewport",
                    () => m_Viewport,
                    new ValueWriter<Viewport>()
                )
            );
            AddBinding(m_CameraMode = new ValueBinding<int>("editorHierarchy", "cameraMode", 0));
            AddBinding(
                new TriggerBinding<int>(
                    "editorHierarchy",
                    "setWidth",
                    delegate(int width)
                    {
                        EditorSettings editorSettings = SharedSettings.instance?.editor;
                        if (editorSettings != null)
                        {
                            editorSettings.hierarchyWidth = width;
                        }
                    }
                )
            );
            AddBinding(
                new TriggerBinding<int, int>(
                    "editorHierarchy",
                    "setViewportRange",
                    delegate(int startIndex, int endIndex)
                    {
                        m_NextViewportStartIndex = startIndex;
                        m_NextViewportEndIndex = endIndex;
                    }
                )
            );
            AddBinding(
                new TriggerBinding<ItemId>(
                    "editorHierarchy",
                    "setSelectedId",
                    delegate(ItemId id)
                    {
                        m_SelectedId = id;
                        switch (id.type)
                        {
                            case ItemType.Object:
                                m_ToolSystem.selected = id.entity;
                                m_EditorToolUISystem.SelectEntity(id.entity);
                                break;
                            case ItemType.SubMesh:
                                m_ToolSystem.selected = id.entity;
                                m_EditorToolUISystem.SelectEntitySubMesh(id.entity, id.subIndex);
                                break;
                            default:
                                m_ToolSystem.selected = Entity.Null;
                                m_EditorPanelUISystem.activePanel = panelItems
                                    .FirstOrDefault((PanelItem p) => p.type == id.type)
                                    ?.panel;
                                break;
                        }
                        RefreshCameraController((CameraMode)m_CameraMode.value);
                    },
                    new ValueReader<ItemId>()
                )
            );
            AddBinding(
                new TriggerBinding<ItemId, bool>(
                    "editorHierarchy",
                    "setExpanded",
                    delegate(ItemId id, bool expanded)
                    {
                        m_Dirty = true;
                        if (expanded)
                        {
                            m_ExpandedIds.Add(id);
                        }
                        else
                        {
                            m_ExpandedIds.Remove(id);
                        }
                    },
                    new ValueReader<ItemId>()
                )
            );
            AddBinding(
                new TriggerBinding<int>(
                    "editorHierarchy",
                    "toggleCameraMode",
                    delegate(int mode)
                    {
                        ToggleCameraMode((CameraMode)mode);
                    }
                )
            );
            AddBinding(
                new TriggerBinding<Entity>(
                    "editorHierarchy",
                    "save",
                    delegate(Entity entity)
                    {
                        PrefabRef componentData = base.EntityManager.GetComponentData<PrefabRef>(
                            entity
                        );
                        EditorPrefabUtils.SavePrefab(
                            m_PrefabSystem.GetPrefab<PrefabBase>(componentData)
                        );
                        PlatformManager.instance.UnlockAchievement(Achievements.IMadeThis);
                        onSave?.Invoke(entity);
                    }
                )
            );
            AddBinding(
                new TriggerBinding<Entity>(
                    "editorHierarchy",
                    "bulldoze",
                    delegate(Entity entity)
                    {
                        onBulldoze?.Invoke(entity);
                    }
                )
            );
            panelItems = new List<PanelItem>
            {
                new PanelItem(
                    ItemType.Map,
                    0,
                    base.World.GetOrCreateSystemManaged<MapPanelSystem>()
                ),
                new PanelItem(
                    ItemType.Climate,
                    1,
                    base.World.GetOrCreateSystemManaged<ClimatePanelSystem>()
                ),
                new PanelItem(
                    ItemType.Water,
                    1,
                    base.World.GetOrCreateSystemManaged<WaterPanelSystem>()
                ),
                new PanelItem(
                    ItemType.Resources,
                    1,
                    base.World.GetOrCreateSystemManaged<ResourcePanelSystem>()
                ),
            };
            m_Hierarchy = new NativeList<HierarchyItem>(Allocator.Persistent);
            m_ExpandedIds = new NativeParallelHashSet<ItemId>(128, Allocator.Persistent);
            m_Viewport = new Viewport();
        }

        protected override void OnGameLoaded(Context serializationContext)
        {
            m_Dirty = true;
        }

        [Preserve]
        protected override void OnStartRunning()
        {
            base.OnStartRunning();
            m_ExpandedIds.Clear();
        }

        [Preserve]
        protected override void OnDestroy()
        {
            m_Hierarchy.Dispose();
            m_ExpandedIds.Dispose();
            base.OnDestroy();
        }

        [Preserve]
        protected override void OnUpdate()
        {
            bool flag = m_Dirty || !m_ModifiedQuery.IsEmptyIgnoreFilter;
            m_Dirty = false;
            m_TotalCount = m_Hierarchy.Length;
            UpdateSelection();
            base.OnUpdate();
            UpdateViewport(flag);
            if (flag)
            {
                UpdateHierarchy(m_Hierarchy);
            }
        }

        private void UpdateSelection()
        {
            if (m_EditorPanelUISystem.activePanel == null)
            {
                if (!m_SelectedId.isContainer)
                {
                    m_SelectedId = default(ItemId);
                }
                return;
            }
            if (m_ToolSystem.selected != Entity.Null)
            {
                if (m_SelectedId.entity != m_ToolSystem.selected)
                {
                    m_SelectedId = new ItemId
                    {
                        type = ItemType.Object,
                        entity = m_ToolSystem.selected,
                    };
                    RefreshCameraController((CameraMode)m_CameraMode.value);
                }
                return;
            }
            PanelItem panelItem = panelItems.FirstOrDefault(
                (PanelItem p) => p.type == m_SelectedId.type
            );
            if (m_EditorPanelUISystem.activePanel != panelItem?.panel)
            {
                PanelItem panelItem2 = panelItems.FirstOrDefault(
                    (PanelItem p) => p.panel == m_EditorPanelUISystem.activePanel
                );
                m_SelectedId = (
                    (panelItem2 != null) ? new ItemId(panelItem2.type) : default(ItemId)
                );
            }
        }

        private void UpdateViewport(bool force)
        {
            m_NextViewportStartIndex = math.clamp(m_NextViewportStartIndex, 0, m_Hierarchy.Length);
            m_NextViewportEndIndex = math.clamp(m_NextViewportEndIndex, 0, m_Hierarchy.Length);
            if (force || ViewportChanged())
            {
                m_Viewport.startIndex = m_NextViewportStartIndex;
                m_Viewport.items.Clear();
                for (int i = m_NextViewportStartIndex; i < m_NextViewportEndIndex; i++)
                {
                    m_Viewport.items.Add(BuildViewportItem(m_Hierarchy[i]));
                }
                m_ViewportBinding.TriggerUpdate();
            }
        }

        private bool ViewportChanged()
        {
            if (
                m_NextViewportStartIndex != m_Viewport.startIndex
                || m_NextViewportEndIndex != m_Viewport.startIndex + m_Viewport.items.Count
            )
            {
                return true;
            }
            for (int i = 0; i < m_Viewport.items.Count; i++)
            {
                ViewportItem viewportItem = m_Viewport.items[i];
                int num = m_Viewport.startIndex + i;
                if (num >= m_Hierarchy.Length)
                {
                    return true;
                }
                if (!viewportItem.EqualsHierarchy(m_Hierarchy[num]))
                {
                    return true;
                }
            }
            return false;
        }

        private ViewportItem BuildViewportItem(HierarchyItem item)
        {
            ViewportItem result = default(ViewportItem);
            result.id = item.id;
            result.level = item.level;
            result.expandable = item.expandable;
            result.expanded = item.expanded;
            result.name = GetName(item.id);
            result.selectable = item.selectable;
            result.saveable =
                item.id.type == ItemType.Object
                && base.EntityManager.TryGetComponent<PrefabRef>(item.id.entity, out var component)
                && m_PrefabSystem.TryGetPrefab<PrefabBase>(component, out var prefab)
                && !prefab.builtin;
            return result;
        }

        private LocalizedString GetName(ItemId id)
        {
            PrefabRef component2;
            DynamicBuffer<SubMesh> buffer;
            PrefabBase prefab2;
            if (id.type == ItemType.Object)
            {
                if (
                    base.EntityManager.TryGetComponent<PrefabRef>(id.entity, out var component)
                    && m_PrefabSystem.TryGetPrefab<PrefabBase>(component, out var prefab)
                )
                {
                    return LocalizedString.Value(prefab.name);
                }
            }
            else if (
                id.type == ItemType.SubMesh
                && base.EntityManager.TryGetComponent<PrefabRef>(id.entity, out component2)
                && base.EntityManager.TryGetBuffer(
                    component2.m_Prefab,
                    isReadOnly: true,
                    out buffer
                )
                && id.subIndex < buffer.Length
                && m_PrefabSystem.TryGetPrefab<PrefabBase>(
                    buffer[id.subIndex].m_SubMesh,
                    out prefab2
                )
            )
            {
                return LocalizedString.Value(prefab2.name);
            }
            return "Editor." + id.type.ToString().ToUpper();
        }

        private void UpdateHierarchy(NativeList<HierarchyItem> hierarchy)
        {
            hierarchy.Clear();
            foreach (PanelItem panelItem in panelItems)
            {
                HierarchyItem value = new HierarchyItem
                {
                    id = new ItemId(panelItem.type),
                    level = panelItem.level,
                    selectable = true,
                };
                hierarchy.Add(in value);
            }
            ScheduleJobForItemType(
                hierarchy,
                m_ObjectQuery,
                ItemType.Object,
                ItemType.ObjectContainer
            );
            ScheduleJobForItemType(
                hierarchy,
                m_NetworkQuery,
                ItemType.Object,
                ItemType.NetworkContainer
            );
            ScheduleJobForItemType(hierarchy, m_AreaQuery, ItemType.Object, ItemType.AreaContainer);
            ScheduleJobForItemType(
                hierarchy,
                m_OtherQuery,
                ItemType.Object,
                ItemType.OtherContainer
            );
        }

        private void ScheduleJobForItemType(
            NativeList<HierarchyItem> hierarchy,
            EntityQuery query,
            ItemType itemType,
            ItemType containerType
        )
        {
            if (!query.IsEmptyIgnoreFilter)
            {
                ItemId itemId = new ItemId(containerType);
                bool num = m_ExpandedIds.Contains(itemId);
                HierarchyItem value = new HierarchyItem
                {
                    id = itemId,
                    level = 0,
                    expandable = true,
                    expanded = m_ExpandedIds.Contains(itemId),
                    selectable = false,
                };
                hierarchy.Add(in value);
                if (num)
                {
                    //__TypeHandle.__Game_Prefabs_SubMesh_RO_BufferLookup.Update(ref base.CheckedStateRef);
                    //__TypeHandle.__Game_Prefabs_PrefabRef_RO_ComponentTypeHandle.Update(ref base.CheckedStateRef);
                    //__TypeHandle.__Unity_Entities_Entity_TypeHandle.Update(ref base.CheckedStateRef);
                    ObjectHierarchyJob objectHierarchyJob = default(ObjectHierarchyJob);
                    objectHierarchyJob.m_EntityType = InternalCompilerInterface.GetEntityTypeHandle(
                        ref __TypeHandle.__Unity_Entities_Entity_TypeHandle,
                        ref CheckedStateRef
                    );
                    objectHierarchyJob.m_PrefabRefType =
                        InternalCompilerInterface.GetComponentTypeHandle<PrefabRef>(
                            ref this.__TypeHandle.__Game_Prefabs_PrefabRef_RO_ComponentTypeHandle,
                            ref base.CheckedStateRef
                        );
                    ;
                    objectHierarchyJob.m_SubMeshes =
                        InternalCompilerInterface.GetBufferLookup<SubMesh>(
                            ref this.__TypeHandle.__Game_Prefabs_SubMesh_RO_BufferLookup,
                            ref base.CheckedStateRef
                        );
                    objectHierarchyJob.m_ExpandedIds = m_ExpandedIds;
                    objectHierarchyJob.m_Hierarchy = hierarchy;
                    objectHierarchyJob.m_ContainerType = containerType;
                    objectHierarchyJob.m_ItemType = itemType;
                    ObjectHierarchyJob jobData = objectHierarchyJob;
                    base.Dependency = JobChunkExtensions.Schedule(jobData, query, base.Dependency);
                    base.Dependency = EditorHierarchyUISystem_4CF10000_LambdaJob_0_Execute(
                        hierarchy,
                        base.Dependency
                    );
                }
            }
        }

        private void SetViewportRange(int startIndex, int endIndex)
        {
            m_NextViewportStartIndex = startIndex;
            m_NextViewportEndIndex = endIndex;
        }

        private void SetSelectedId(ItemId id)
        {
            m_SelectedId = id;
            switch (id.type)
            {
                case ItemType.Object:
                    m_ToolSystem.selected = id.entity;
                    m_EditorToolUISystem.SelectEntity(id.entity);
                    break;
                case ItemType.SubMesh:
                    m_ToolSystem.selected = id.entity;
                    m_EditorToolUISystem.SelectEntitySubMesh(id.entity, id.subIndex);
                    break;
                default:
                    m_ToolSystem.selected = Entity.Null;
                    m_EditorPanelUISystem.activePanel = panelItems
                        .FirstOrDefault((PanelItem p) => p.type == id.type)
                        ?.panel;
                    break;
            }
            RefreshCameraController((CameraMode)m_CameraMode.value);
        }

        private void SetExpanded(ItemId id, bool expanded)
        {
            m_Dirty = true;
            if (expanded)
            {
                m_ExpandedIds.Add(id);
            }
            else
            {
                m_ExpandedIds.Remove(id);
            }
        }

        private void ToggleCameraMode(CameraMode cameraMode)
        {
            m_CameraMode.Update((int)cameraMode);
            RefreshCameraController(cameraMode);
        }

        private void RefreshCameraController(CameraMode mode)
        {
            if (
                mode == CameraMode.Default
                || (mode == CameraMode.Orbit && m_SelectedId.entity == Entity.Null)
            )
            {
                if (
                    m_CameraUpdateSystem.activeCameraController
                    != m_CameraUpdateSystem.gamePlayController
                )
                {
                    m_CameraUpdateSystem.gamePlayController.TryMatchPosition(
                        m_CameraUpdateSystem.activeCameraController
                    );
                    m_CameraUpdateSystem.activeCameraController =
                        m_CameraUpdateSystem.gamePlayController;
                }
                return;
            }
            switch (mode)
            {
                case CameraMode.Orbit:
                    m_CameraUpdateSystem.orbitCameraController.followedEntity = m_SelectedId.entity;
                    if (
                        m_CameraUpdateSystem.activeCameraController
                        != m_CameraUpdateSystem.orbitCameraController
                    )
                    {
                        m_CameraUpdateSystem.orbitCameraController.TryMatchPosition(
                            m_CameraUpdateSystem.activeCameraController
                        );
                        m_CameraUpdateSystem.activeCameraController =
                            m_CameraUpdateSystem.orbitCameraController;
                    }
                    break;
                case CameraMode.FirstPerson:
                    if (
                        m_CameraUpdateSystem.activeCameraController
                        != m_CameraUpdateSystem.cinematicCameraController
                    )
                    {
                        m_CameraUpdateSystem.cinematicCameraController.TryMatchPosition(
                            m_CameraUpdateSystem.activeCameraController
                        );
                        m_CameraUpdateSystem.activeCameraController =
                            m_CameraUpdateSystem.cinematicCameraController;
                    }
                    break;
            }
        }

        private int GetWidth()
        {
            return (SharedSettings.instance?.editor)?.hierarchyWidth ?? 350;
        }

        private void SetWidth(int width)
        {
            EditorSettings editorSettings = SharedSettings.instance?.editor;
            if (editorSettings != null)
            {
                editorSettings.hierarchyWidth = width;
            }
        }

        private void OnSave(Entity entity)
        {
            PrefabRef componentData = base.EntityManager.GetComponentData<PrefabRef>(entity);
            EditorPrefabUtils.SavePrefab(m_PrefabSystem.GetPrefab<PrefabBase>(componentData));
            PlatformManager.instance.UnlockAchievement(Achievements.IMadeThis);
            onSave?.Invoke(entity);
        }

        private void OnBulldoze(Entity entity)
        {
            onBulldoze?.Invoke(entity);
        }

        private JobHandle EditorHierarchyUISystem_4CF10000_LambdaJob_0_Execute(
            NativeList<HierarchyItem> hierarchy,
            JobHandle __inputDependency
        )
        {
            EditorHierarchyUISystem_4CF10000_LambdaJob_0_Job jobData =
                default(EditorHierarchyUISystem_4CF10000_LambdaJob_0_Job);
            jobData.hierarchy = hierarchy;
            return IJobExtensions.Schedule(jobData, __inputDependency);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void __AssignQueries(ref SystemState state)
        {
            EntityQueryBuilder entityQueryBuilder = new EntityQueryBuilder(Allocator.Temp);
            entityQueryBuilder.Dispose();
        }

        protected override void OnCreateForCompiler()
        {
            base.OnCreateForCompiler();
            __AssignQueries(ref base.CheckedStateRef);
            __TypeHandle.__AssignHandles(ref base.CheckedStateRef);
        }

        [Preserve]
        public PatchedEditorHierarchyUISystem() { }
    }
}
