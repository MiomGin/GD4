using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class PostProcessFeature :
    ScriptableRendererFeature
{
    // ============================================================
    // Mask Group Constants
    // ============================================================

    // 0 = Background
    //
    // 1 = Default merged scene group
    //
    // 2+ = Explicit MaskGroup
    public const uint DefaultMaskGroupID =
        1u;


    // RGB stores Group ID.
    public const uint MaxMaskGroupID =
        0xFFFFFFu;


    // ============================================================
    // Shader Property IDs
    // ============================================================

    private static readonly int ObjectInfoTextureID =
        Shader.PropertyToID(
            "_ObjectInfoTexture"
        );


    private static readonly int ObjectSortingTextureID =
        Shader.PropertyToID(
            "_ObjectSortingTexture"
        );


    private static readonly int ObjectInfoTexelSizeID =
        Shader.PropertyToID(
            "_ObjectInfoTexture_TexelSize"
        );


    private static readonly int UseObjectInfoBufferID =
        Shader.PropertyToID(
            "_UseObjectInfoBuffer"
        );


    private static readonly int MaskGroupDataID =
        Shader.PropertyToID(
            "_MaskGroupData"
        );


    private static readonly int MaskSortingDataID =
        Shader.PropertyToID(
            "_MaskSortingData"
        );


    private static readonly int AlphaClipThresholdID =
        Shader.PropertyToID(
            "_AlphaClipThreshold"
        );


    // ============================================================
    // Effect Entry
    // ============================================================

    [Serializable]
    public class EffectEntry
    {
        [Tooltip(
            "Fullscreen post-process material."
        )]
        public Material material;


        [Tooltip(
            "Allow this material to access the ObjectInfo " +
            "and ObjectSorting buffers."
        )]
        public bool useObjectInfoBuffer =
            false;


        [Min(0)]
        public int passIndex =
            0;
    }


    // ============================================================
    // Settings
    // ============================================================

    [Serializable]
    public class Settings
    {
        [Header("Object Mask")]

        [Tooltip(
            "Only Renderers on these GameObject Layers " +
            "participate in the mask system."
        )]
        public LayerMask objectLayerMask =
            ~0;


        [Tooltip(
            "Leave empty to automatically use " +
            "Hidden/PostProcess/ObjectInfoSorting."
        )]
        public Material objectInfoMaterial;


        [Range(0.001f, 1.0f)]
        public float alphaClipThreshold =
            0.01f;


        [Header("Renderer Cache")]

        [Min(0)]
        [Tooltip(
            "How often the global Renderer list is refreshed.\n\n" +
            "0 = only refresh when MaskGroup hierarchy changes.\n" +
            "1 = every frame.\n" +
            "30 = every 30 frames."
        )]
        public int rendererCacheRefreshInterval =
            30;


        [Header("Post Process Stack")]

        [Tooltip(
            "Effects execute from top to bottom."
        )]
        public EffectEntry[] effects =
            Array.Empty<EffectEntry>();


        [Header("Render Timing")]

        public RenderPassEvent passEvent =
            RenderPassEvent
                .AfterRenderingPostProcessing;


        [Header("Camera")]

        public bool runInSceneView =
            true;


        public bool runOnOverlayCameras =
            false;
    }


    public Settings settings =
        new Settings();


    // ============================================================
    // Shared Resources
    // ============================================================

    private class SharedResources
    {
        public RTHandle objectInfoRT;

        public RTHandle objectSortingRT;
    }


    private SharedResources sharedResources;


    private RendererMetadataCache rendererMetadataCache;

    private ObjectInfoPass objectInfoPass;

    private PostProcessStackPass postProcessPass;


    private Material runtimeObjectInfoMaterial;


    // ============================================================
    // Renderer Metadata Cache
    //
    // The actual rendering still uses URP's culling and sorting.
    //
    // This cache only supplies per-renderer metadata:
    //
    // Group ID
    // Group Flags
    // Sorting Layer
    // Sorting Order
    // ============================================================

    private class RendererMetadataCache
    {
        private class RendererEntry
        {
            public Renderer renderer;

            public MaskGroup maskGroup;


            public uint lastGroupID =
                uint.MaxValue;


            public byte lastFlags =
                byte.MaxValue;


            public int lastSortingLayerValue =
                int.MinValue;


            public int lastSortingOrder =
                int.MinValue;
        }


        private readonly List<RendererEntry>
            entries =
                new List<RendererEntry>();


        private readonly MaterialPropertyBlock
            propertyBlock =
                new MaterialPropertyBlock();


        private int cachedLayerMask =
            int.MinValue;


        private int cachedMaskGroupVersion =
            -1;


        private int lastRefreshFrame =
            -1;


        private bool initialized;


        // ========================================================
        // Update
        // ========================================================

        public void Update(
            Settings settings)
        {
            RefreshIfNeeded(
                settings
            );


            UpdateRendererProperties();
        }


        // ========================================================
        // Cache Refresh
        // ========================================================

        private void RefreshIfNeeded(
            Settings settings)
        {
            int currentLayerMask =
                settings
                    .objectLayerMask
                    .value;


            bool layerMaskChanged =
                currentLayerMask !=
                cachedLayerMask;


            bool maskGroupChanged =
                cachedMaskGroupVersion !=
                MaskGroup.GlobalVersion;


            bool timedRefresh =
                false;


            int interval =
                settings
                    .rendererCacheRefreshInterval;


            if (interval > 0 &&
                lastRefreshFrame >= 0)
            {
                timedRefresh =
                    Time.frameCount -
                    lastRefreshFrame >=
                    interval;
            }


            if (!initialized ||
                layerMaskChanged ||
                maskGroupChanged ||
                timedRefresh)
            {
                Refresh(
                    currentLayerMask
                );
            }
        }


        private void Refresh(
            int layerMask)
        {
            entries.Clear();


            Renderer[] renderers =
                UnityEngine.Object
                    .FindObjectsByType<Renderer>(
                        FindObjectsInactive.Include,
                        FindObjectsSortMode.None
                    );


            for (
                int i = 0;
                i < renderers.Length;
                i++)
            {
                Renderer renderer =
                    renderers[i];


                if (renderer == null)
                {
                    continue;
                }


                GameObject gameObject =
                    renderer.gameObject;


                // Ignore prefab assets / invalid scene objects.
                if (!gameObject
                        .scene
                        .IsValid())
                {
                    continue;
                }


                int layerBit =
                    1 <<
                    gameObject.layer;


                if ((layerMask &
                     layerBit) == 0)
                {
                    continue;
                }


                MaskGroup maskGroup =
                    MaskGroup
                        .ResolveNearestActive(
                            renderer.transform
                        );


                entries.Add(
                    new RendererEntry
                    {
                        renderer =
                            renderer,

                        maskGroup =
                            maskGroup
                    }
                );
            }


            cachedLayerMask =
                layerMask;


            cachedMaskGroupVersion =
                MaskGroup.GlobalVersion;


            lastRefreshFrame =
                Time.frameCount;


            initialized =
                true;
        }


        // ========================================================
        // Update Renderer Metadata
        // ========================================================

        private void UpdateRendererProperties()
        {
            for (
                int i =
                    entries.Count - 1;

                i >= 0;

                i--)
            {
                RendererEntry entry =
                    entries[i];


                Renderer renderer =
                    entry.renderer;


                if (renderer == null)
                {
                    entries.RemoveAt(
                        i
                    );

                    continue;
                }


                // ------------------------------------------------
                // Mask Group
                // ------------------------------------------------

                uint groupID;

                byte flags;


                MaskGroup group =
                    entry.maskGroup;


                if (group != null &&
                    group.isActiveAndEnabled)
                {
                    groupID =
                        group.GroupID;


                    flags =
                        group.GroupFlags;
                }
                else
                {
                    groupID =
                        DefaultMaskGroupID;


                    flags =
                        0;
                }


                // ------------------------------------------------
                // Sorting
                //
                // Convert SortingLayer ID into its actual
                // comparison value.
                // ------------------------------------------------

                int sortingLayerValue =
                    SortingLayer
                        .GetLayerValueFromID(
                            renderer
                                .sortingLayerID
                        );


                int sortingOrder =
                    renderer
                        .sortingOrder;


                // ------------------------------------------------
                // Avoid updating the MaterialPropertyBlock
                // when nothing changed.
                // ------------------------------------------------

                bool changed =
                    entry.lastGroupID !=
                        groupID ||

                    entry.lastFlags !=
                        flags ||

                    entry
                        .lastSortingLayerValue !=
                        sortingLayerValue ||

                    entry.lastSortingOrder !=
                        sortingOrder;


                if (!changed)
                {
                    continue;
                }


                entry.lastGroupID =
                    groupID;


                entry.lastFlags =
                    flags;


                entry.lastSortingLayerValue =
                    sortingLayerValue;


                entry.lastSortingOrder =
                    sortingOrder;


                propertyBlock.Clear();


                // Preserve values written by other systems.
                renderer.GetPropertyBlock(
                    propertyBlock
                );


                propertyBlock.SetVector(
                    MaskGroupDataID,

                    EncodeMaskGroupData(
                        groupID,
                        flags
                    )
                );


                propertyBlock.SetVector(
                    MaskSortingDataID,

                    new Vector4(
                        sortingLayerValue,
                        sortingOrder,
                        0.0f,
                        0.0f
                    )
                );


                renderer.SetPropertyBlock(
                    propertyBlock
                );
            }
        }
    }


    // ============================================================
    // Object Info Pass
    //
    // MRT:
    //
    // SV_Target0
    //     ObjectInfo
    //
    // SV_Target1
    //     SortingInfo
    // ============================================================

    private class ObjectInfoPass :
        ScriptableRenderPass
    {
        private readonly Settings settings;

        private readonly SharedResources resources;

        private readonly RendererMetadataCache
            metadataCache;


        private Material objectInfoMaterial;


        private readonly RTHandle[] mrtTargets =
            new RTHandle[2];


        private static readonly ShaderTagId[]
            ShaderTagIDs =
        {
            new ShaderTagId(
                "Universal2D"
            ),

            new ShaderTagId(
                "UniversalForward"
            ),

            new ShaderTagId(
                "UniversalForwardOnly"
            ),

            new ShaderTagId(
                "SRPDefaultUnlit"
            )
        };


        public ObjectInfoPass(
            Settings settings,
            SharedResources resources,
            RendererMetadataCache metadataCache,
            Material material)
        {
            this.settings =
                settings;


            this.resources =
                resources;


            this.metadataCache =
                metadataCache;


            objectInfoMaterial =
                material;
        }


        public void SetMaterial(
            Material material)
        {
            objectInfoMaterial =
                material;
        }


        // ========================================================
        // Setup MRT
        // ========================================================

        public override void OnCameraSetup(
            CommandBuffer cmd,
            ref RenderingData renderingData)
        {
            if (objectInfoMaterial == null)
            {
                return;
            }


            RenderTextureDescriptor baseDescriptor =
                renderingData
                    .cameraData
                    .cameraTargetDescriptor;


            baseDescriptor.depthBufferBits =
                0;


            baseDescriptor.msaaSamples =
                1;


            // ====================================================
            // Object Info
            //
            // RGB:
            // 24-bit Group ID
            //
            // A:
            // 8-bit Group Flags
            // ====================================================

            RenderTextureDescriptor
                objectInfoDescriptor =
                    baseDescriptor;


            objectInfoDescriptor.graphicsFormat =
                GraphicsFormat
                    .R8G8B8A8_UNorm;


            RenderingUtils
                .ReAllocateIfNeeded(
                    ref resources.objectInfoRT,
                    objectInfoDescriptor,
                    FilterMode.Point,
                    TextureWrapMode.Clamp,
                    name:
                        "_ObjectInfoTexture"
                );


            // ====================================================
            // Sorting Info
            //
            // R:
            // SortingLayer final value
            //
            // G:
            // SortingOrder
            //
            // 32-bit floats preserve integer sorting values.
            // ====================================================

            RenderTextureDescriptor
                sortingDescriptor =
                    baseDescriptor;


            sortingDescriptor.graphicsFormat =
                GraphicsFormat
                    .R32G32_SFloat;


            RenderingUtils
                .ReAllocateIfNeeded(
                    ref resources.objectSortingRT,
                    sortingDescriptor,
                    FilterMode.Point,
                    TextureWrapMode.Clamp,
                    name:
                        "_ObjectSortingTexture"
                );


            mrtTargets[0] =
                resources.objectInfoRT;


            mrtTargets[1] =
                resources.objectSortingRT;


            ConfigureTarget(
                mrtTargets
            );


            ConfigureClear(
                ClearFlag.Color,
                Color.clear
            );
        }


        // ========================================================
        // Execute
        // ========================================================

        public override void Execute(
            ScriptableRenderContext context,
            ref RenderingData renderingData)
        {
            if (objectInfoMaterial == null ||
                resources.objectInfoRT == null ||
                resources.objectSortingRT == null)
            {
                return;
            }


            // Update all per-renderer group and sorting data.
            metadataCache.Update(
                settings
            );


            // ----------------------------------------------------
            // Fallback values.
            //
            // Renderers not yet present in the metadata cache
            // temporarily fall back to Default Group / sorting 0.
            // ----------------------------------------------------

            objectInfoMaterial.SetVector(
                MaskGroupDataID,

                EncodeMaskGroupData(
                    DefaultMaskGroupID,
                    0
                )
            );


            objectInfoMaterial.SetVector(
                MaskSortingDataID,

                Vector4.zero
            );


            objectInfoMaterial.SetFloat(
                AlphaClipThresholdID,
                settings.alphaClipThreshold
            );


            // ----------------------------------------------------
            // URP still performs its own camera culling and
            // transparent sorting here.
            // ----------------------------------------------------

            DrawingSettings drawingSettings =
                CreateDrawingSettings(
                    ShaderTagIDs[0],
                    ref renderingData,
                    SortingCriteria
                        .CommonTransparent
                );


            for (
                int i = 1;
                i < ShaderTagIDs.Length;
                i++)
            {
                drawingSettings
                    .SetShaderPassName(
                        i,
                        ShaderTagIDs[i]
                    );
            }


            drawingSettings.overrideMaterial =
                objectInfoMaterial;


            drawingSettings
                .overrideMaterialPassIndex =
                0;


            FilteringSettings filteringSettings =
                new FilteringSettings(
                    RenderQueueRange.all,
                    settings.objectLayerMask
                );


            context.DrawRenderers(
                renderingData.cullResults,
                ref drawingSettings,
                ref filteringSettings
            );
        }
    }


    // ============================================================
    // Post Process Stack Pass
    // ============================================================

    private class PostProcessStackPass :
        ScriptableRenderPass
    {
        private readonly Settings settings;

        private readonly SharedResources resources;


        private RTHandle tempA;

        private RTHandle tempB;


        private bool objectInfoEnabled;


        public PostProcessStackPass(
            Settings settings,
            SharedResources resources)
        {
            this.settings =
                settings;


            this.resources =
                resources;


            ConfigureInput(
                ScriptableRenderPassInput.Color
            );
        }


        public void SetObjectInfoEnabled(
            bool enabled)
        {
            objectInfoEnabled =
                enabled;
        }


        // ========================================================
        // Setup
        // ========================================================

        public override void OnCameraSetup(
            CommandBuffer cmd,
            ref RenderingData renderingData)
        {
            if (!HasValidEffects())
            {
                return;
            }


            RenderTextureDescriptor descriptor =
                renderingData
                    .cameraData
                    .cameraTargetDescriptor;


            descriptor.depthBufferBits =
                0;


            descriptor.msaaSamples =
                1;


            RenderingUtils
                .ReAllocateIfNeeded(
                    ref tempA,
                    descriptor,
                    FilterMode.Bilinear,
                    TextureWrapMode.Clamp,
                    name:
                        "_PostProcessTempA"
                );


            RenderingUtils
                .ReAllocateIfNeeded(
                    ref tempB,
                    descriptor,
                    FilterMode.Bilinear,
                    TextureWrapMode.Clamp,
                    name:
                        "_PostProcessTempB"
                );
        }


        // ========================================================
        // Execute
        // ========================================================

        public override void Execute(
            ScriptableRenderContext context,
            ref RenderingData renderingData)
        {
            if (!HasValidEffects())
            {
                return;
            }


            RTHandle cameraColor =
                renderingData
                    .cameraData
                    .renderer
                    .cameraColorTargetHandle;


            if (cameraColor == null)
            {
                return;
            }


            CommandBuffer cmd =
                CommandBufferPool.Get(
                    "Post Process Stack"
                );


            bool hasObjectInfo =
                objectInfoEnabled &&

                resources.objectInfoRT !=
                    null &&

                resources.objectSortingRT !=
                    null &&

                resources.objectInfoRT.rt !=
                    null &&

                resources.objectSortingRT.rt !=
                    null;


            // ====================================================
            // Bind Object Buffers
            // ====================================================

            if (hasObjectInfo)
            {
                cmd.SetGlobalTexture(
                    ObjectInfoTextureID,
                    resources
                        .objectInfoRT
                        .nameID
                );


                cmd.SetGlobalTexture(
                    ObjectSortingTextureID,
                    resources
                        .objectSortingRT
                        .nameID
                );


                int width =
                    resources
                        .objectInfoRT
                        .rt
                        .width;


                int height =
                    resources
                        .objectInfoRT
                        .rt
                        .height;


                cmd.SetGlobalVector(
                    ObjectInfoTexelSizeID,

                    new Vector4(
                        1.0f / width,
                        1.0f / height,
                        width,
                        height
                    )
                );
            }


            // ====================================================
            // Sequential Material Stack
            // ====================================================

            RTHandle source =
                cameraColor;


            RTHandle destination =
                tempA;


            for (
                int i = 0;
                i < settings.effects.Length;
                i++)
            {
                EffectEntry effect =
                    settings.effects[i];


                if (effect == null ||
                    effect.material == null)
                {
                    continue;
                }


                bool useObjectInfo =
                    effect
                        .useObjectInfoBuffer &&

                    hasObjectInfo;


                cmd.SetGlobalFloat(
                    UseObjectInfoBufferID,

                    useObjectInfo
                        ? 1.0f
                        : 0.0f
                );


                int passIndex =
                    Mathf.Clamp(
                        effect.passIndex,

                        0,

                        Mathf.Max(
                            0,

                            effect
                                .material
                                .passCount -
                            1
                        )
                    );


                Blitter.BlitCameraTexture(
                    cmd,
                    source,
                    destination,
                    effect.material,
                    passIndex
                );


                source =
                    destination;


                destination =
                    ReferenceEquals(
                        destination,
                        tempA
                    )
                        ? tempB
                        : tempA;
            }


            // ====================================================
            // Final Output
            // ====================================================

            if (!ReferenceEquals(
                    source,
                    cameraColor))
            {
                Blitter.BlitCameraTexture(
                    cmd,
                    source,
                    cameraColor
                );
            }


            cmd.SetGlobalFloat(
                UseObjectInfoBufferID,
                0.0f
            );


            context.ExecuteCommandBuffer(
                cmd
            );


            CommandBufferPool.Release(
                cmd
            );
        }


        // ========================================================
        // Validation
        // ========================================================

        private bool HasValidEffects()
        {
            if (settings.effects == null)
            {
                return false;
            }


            foreach (
                EffectEntry effect
                in settings.effects)
            {
                if (effect != null &&
                    effect.material != null)
                {
                    return true;
                }
            }


            return false;
        }


        // ========================================================
        // Dispose
        // ========================================================

        public void Dispose()
        {
            tempA?.Release();

            tempB?.Release();


            tempA =
                null;


            tempB =
                null;
        }
    }


    // ============================================================
    // Create
    // ============================================================

    public override void Create()
    {
        if (runtimeObjectInfoMaterial !=
            null)
        {
            CoreUtils.Destroy(
                runtimeObjectInfoMaterial
            );


            runtimeObjectInfoMaterial =
                null;
        }


        sharedResources ??=
            new SharedResources();


        rendererMetadataCache =
            new RendererMetadataCache();


        Material material =
            settings.objectInfoMaterial;


        if (material == null)
        {
            Shader shader =
                Shader.Find(
                    "Hidden/PostProcess/ObjectInfoSorting"
                );


            if (shader != null)
            {
                runtimeObjectInfoMaterial =
                    CoreUtils
                        .CreateEngineMaterial(
                            shader
                        );


                material =
                    runtimeObjectInfoMaterial;
            }
            else
            {
                Debug.LogError(
                    "PostProcessFeature: Cannot find shader " +
                    "'Hidden/PostProcess/ObjectInfoSorting'."
                );
            }
        }


        objectInfoPass =
            new ObjectInfoPass(
                settings,
                sharedResources,
                rendererMetadataCache,
                material
            );


        postProcessPass =
            new PostProcessStackPass(
                settings,
                sharedResources
            );


        UpdatePassEvents();
    }


    // ============================================================
    // Add Render Passes
    // ============================================================

    public override void AddRenderPasses(
        ScriptableRenderer renderer,
        ref RenderingData renderingData)
    {
        if (renderingData
                .cameraData
                .isPreviewCamera)
        {
            return;
        }


        if (!settings.runInSceneView &&
            renderingData
                .cameraData
                .isSceneViewCamera)
        {
            return;
        }


        if (!settings
                .runOnOverlayCameras &&

            renderingData
                .cameraData
                .renderType ==
            CameraRenderType.Overlay)
        {
            return;
        }


        if (!HasAnyValidEffect())
        {
            return;
        }


        UpdatePassEvents();


        Material material =
            settings.objectInfoMaterial != null

                ? settings.objectInfoMaterial

                : runtimeObjectInfoMaterial;


        objectInfoPass.SetMaterial(
            material
        );


        bool canGenerateObjectInfo =
            NeedsObjectInfoBuffer() &&
            material != null;


        postProcessPass
            .SetObjectInfoEnabled(
                canGenerateObjectInfo
            );


        if (canGenerateObjectInfo)
        {
            renderer.EnqueuePass(
                objectInfoPass
            );
        }


        renderer.EnqueuePass(
            postProcessPass
        );
    }


    // ============================================================
    // Helpers
    // ============================================================

    private void UpdatePassEvents()
    {
        int objectInfoEvent =
            Mathf.Max(
                (int)
                    RenderPassEvent
                        .BeforeRendering,

                (int)
                    settings
                        .passEvent -
                1
            );


        objectInfoPass.renderPassEvent =
            (RenderPassEvent)
                objectInfoEvent;


        postProcessPass.renderPassEvent =
            settings.passEvent;
    }


    private bool HasAnyValidEffect()
    {
        if (settings.effects == null)
        {
            return false;
        }


        foreach (
            EffectEntry effect
            in settings.effects)
        {
            if (effect != null &&
                effect.material != null)
            {
                return true;
            }
        }


        return false;
    }


    private bool NeedsObjectInfoBuffer()
    {
        if (settings.effects == null)
        {
            return false;
        }


        foreach (
            EffectEntry effect
            in settings.effects)
        {
            if (effect != null &&
                effect.material != null &&
                effect.useObjectInfoBuffer)
            {
                return true;
            }
        }


        return false;
    }


    // ============================================================
    // Mask Group Encoding
    //
    // RGB:
    // 24-bit Group ID
    //
    // A:
    // 8-bit Flags
    // ============================================================

    public static Vector4 EncodeMaskGroupData(
        uint groupID,
        byte flags)
    {
        groupID &=
            MaxMaskGroupID;


        uint r =
            groupID &
            0xFFu;


        uint g =
            (groupID >> 8) &
            0xFFu;


        uint b =
            (groupID >> 16) &
            0xFFu;


        return new Vector4(
            r / 255.0f,
            g / 255.0f,
            b / 255.0f,
            flags / 255.0f
        );
    }


    // ============================================================
    // Dispose
    // ============================================================

    protected override void Dispose(
        bool disposing)
    {
        postProcessPass?.Dispose();


        if (sharedResources != null)
        {
            sharedResources
                .objectInfoRT
                ?.Release();


            sharedResources
                .objectSortingRT
                ?.Release();


            sharedResources.objectInfoRT =
                null;


            sharedResources.objectSortingRT =
                null;
        }


        if (runtimeObjectInfoMaterial !=
            null)
        {
            CoreUtils.Destroy(
                runtimeObjectInfoMaterial
            );


            runtimeObjectInfoMaterial =
                null;
        }
    }
}