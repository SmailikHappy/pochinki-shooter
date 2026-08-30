using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Draws the generated Pixel visuals in a small number of instanced batches.
/// Pixel GameObjects, ownership components and capture colliders remain intact;
/// only their individual MeshRenderers are replaced while this component is active.
/// </summary>
[DisallowMultipleComponent]
public sealed class GameSurfaceRenderer : MonoBehaviour
{
    // RenderMeshInstanced is commonly limited to 511 matrices when Unity also
    // supplies worldToObject. 500 is safe for the URP/WebGL shader variants too.
    private const int MaxInstancesPerBatch = 500;

    private sealed class VisualInstance
    {
        public Transform Transform;
        public MeshRenderer Renderer;
        public Mesh Mesh;
        public int Layer;
        public ShadowCastingMode ShadowCastingMode;
        public bool ReceiveShadows;
        public LightProbeUsage LightProbeUsage;
        public ReflectionProbeUsage ReflectionProbeUsage;
        public uint RenderingLayerMask;
        public MotionVectorGenerationMode MotionVectorMode;
    }

    private readonly struct DrawKey : IEquatable<DrawKey>
    {
        public readonly Mesh Mesh;
        public readonly Material SourceMaterial;
        public readonly int Layer;
        public readonly ShadowCastingMode ShadowCastingMode;
        public readonly bool ReceiveShadows;
        public readonly LightProbeUsage LightProbeUsage;
        public readonly ReflectionProbeUsage ReflectionProbeUsage;
        public readonly uint RenderingLayerMask;
        public readonly MotionVectorGenerationMode MotionVectorMode;

        public DrawKey(VisualInstance visual, Material sourceMaterial)
        {
            Mesh = visual.Mesh;
            SourceMaterial = sourceMaterial;
            Layer = visual.Layer;
            ShadowCastingMode = visual.ShadowCastingMode;
            ReceiveShadows = visual.ReceiveShadows;
            LightProbeUsage = visual.LightProbeUsage;
            ReflectionProbeUsage = visual.ReflectionProbeUsage;
            RenderingLayerMask = visual.RenderingLayerMask;
            MotionVectorMode = visual.MotionVectorMode;
        }

        public bool Equals(DrawKey other)
        {
            return Mesh == other.Mesh &&
                SourceMaterial == other.SourceMaterial &&
                Layer == other.Layer &&
                ShadowCastingMode == other.ShadowCastingMode &&
                ReceiveShadows == other.ReceiveShadows &&
                LightProbeUsage == other.LightProbeUsage &&
                ReflectionProbeUsage == other.ReflectionProbeUsage &&
                RenderingLayerMask == other.RenderingLayerMask &&
                MotionVectorMode == other.MotionVectorMode;
        }

        public override bool Equals(object obj)
        {
            return obj is DrawKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Mesh != null ? Mesh.GetHashCode() : 0;
                hash = (hash * 397) ^ (SourceMaterial != null ? SourceMaterial.GetHashCode() : 0);
                hash = (hash * 397) ^ Layer;
                hash = (hash * 397) ^ (int)ShadowCastingMode;
                hash = (hash * 397) ^ (ReceiveShadows ? 1 : 0);
                hash = (hash * 397) ^ (int)LightProbeUsage;
                hash = (hash * 397) ^ (int)ReflectionProbeUsage;
                hash = (hash * 397) ^ RenderingLayerMask.GetHashCode();
                hash = (hash * 397) ^ (int)MotionVectorMode;
                return hash;
            }
        }
    }

    private sealed class DrawBatch
    {
        public readonly DrawKey Key;
        public readonly Material RuntimeMaterial;
        public readonly List<Matrix4x4> Matrices = new(MaxInstancesPerBatch);

        public DrawBatch(DrawKey key, Material runtimeMaterial)
        {
            Key = key;
            RuntimeMaterial = runtimeMaterial;
        }
    }

    private readonly struct RendererState
    {
        public readonly MeshRenderer Renderer;
        public readonly bool WasEnabled;

        public RendererState(MeshRenderer renderer)
        {
            Renderer = renderer;
            WasEnabled = renderer != null && renderer.enabled;
        }
    }

    private readonly List<VisualInstance> visuals = new();
    private readonly List<RendererState> rendererStates = new();
    private readonly List<PlayerOwnable> subscribedOwnables = new();
    private readonly Dictionary<DrawKey, DrawBatch> drawBatches = new();
    private readonly Dictionary<Material, Material> runtimeMaterials = new();

    private bool instancedRenderingActive;
    private bool headlessRenderingDisabled;
    private bool groupsDirty;
    private bool hasWorldBounds;
    private Bounds worldBounds;

    public bool IsInstancedRenderingActive => instancedRenderingActive;

    /// <summary>
    /// Replaces the individual root and outline MeshRenderers for the supplied
    /// logical pixels. Returns false when the platform or prefab cannot use the
    /// instanced path; in that case all original renderers stay enabled.
    /// </summary>
    public bool Rebuild(IReadOnlyList<Pixel> pixels)
    {
        Clear();

        if (pixels == null || pixels.Count == 0)
            return false;

        if (!CollectVisuals(pixels, out string validationError))
        {
            FallbackToOriginalRenderers(validationError);
            return false;
        }

        if (IsHeadlessBuild())
        {
            DisableOriginalRenderers();
            headlessRenderingDisabled = true;
            return true;
        }

        if (!SystemInfo.supportsInstancing)
        {
            FallbackToOriginalRenderers(
                "GPU instancing is unavailable on this graphics device; using individual Pixel renderers.");
            return false;
        }

        if (!RebuildDrawBatches(out string batchError))
        {
            FallbackToOriginalRenderers(batchError);
            return false;
        }

        SubscribeToOwnershipChanges(pixels);
        DisableOriginalRenderers();
        instancedRenderingActive = true;
        groupsDirty = false;
        transform.hasChanged = false;
        return true;
    }

    /// <summary>
    /// Restores individual renderers and releases runtime materials/subscriptions.
    /// Safe to call repeatedly and immediately before destroying PixelParent.
    /// </summary>
    public void Clear()
    {
        UnsubscribeFromOwnershipChanges();
        RestoreOriginalRenderers();
        DestroyRuntimeMaterials();

        visuals.Clear();
        rendererStates.Clear();
        drawBatches.Clear();

        instancedRenderingActive = false;
        headlessRenderingDisabled = false;
        groupsDirty = false;
        hasWorldBounds = false;
    }

    private void LateUpdate()
    {
        if (!instancedRenderingActive)
            return;

        if (transform.hasChanged)
        {
            groupsDirty = true;
            transform.hasChanged = false;
        }

        if (groupsDirty && !RebuildDrawBatches(out string batchError))
        {
            FallbackToOriginalRenderers(batchError);
            return;
        }

        if (!hasWorldBounds)
            return;

        try
        {
            foreach (DrawBatch batch in drawBatches.Values)
            {
                int matrixCount = batch.Matrices.Count;
                if (matrixCount == 0)
                    continue;

                ShadowCastingMode shadowCastingMode = batch.Key.ShadowCastingMode;
                bool receiveShadows = batch.Key.ReceiveShadows;
                MotionVectorGenerationMode motionVectorMode = batch.Key.MotionVectorMode;
#if UNITY_WEBGL && !UNITY_EDITOR && !DEVELOPMENT_BUILD
                bool optimizeWebRelease = true;
#else
                bool optimizeWebRelease = false;
#endif
                if (optimizeWebRelease)
                {
                    shadowCastingMode = ShadowCastingMode.Off;
                    receiveShadows = false;
                    motionVectorMode = MotionVectorGenerationMode.ForceNoMotion;
                }

                var renderParams = new RenderParams(batch.RuntimeMaterial)
                {
                    worldBounds = worldBounds,
                    layer = batch.Key.Layer,
                    shadowCastingMode = shadowCastingMode,
                    receiveShadows = receiveShadows,
                    motionVectorMode = motionVectorMode,
                    lightProbeUsage = batch.Key.LightProbeUsage,
                    reflectionProbeUsage = batch.Key.ReflectionProbeUsage,
                    renderingLayerMask = batch.Key.RenderingLayerMask,
                };

                for (int start = 0; start < matrixCount; start += MaxInstancesPerBatch)
                {
                    int count = Mathf.Min(MaxInstancesPerBatch, matrixCount - start);
                    Graphics.RenderMeshInstanced(
                        renderParams,
                        batch.Key.Mesh,
                        0,
                        batch.Matrices,
                        count,
                        start);
                }
            }
        }
        catch (InvalidOperationException exception)
        {
            FallbackToOriginalRenderers(
                $"Instanced Pixel rendering failed ({exception.Message}); restored individual renderers.");
        }
    }

    private bool CollectVisuals(IReadOnlyList<Pixel> pixels, out string error)
    {
        for (int pixelIndex = 0; pixelIndex < pixels.Count; pixelIndex++)
        {
            Pixel pixel = pixels[pixelIndex];
            if (pixel == null)
                continue;

            MeshRenderer[] pixelRenderers = pixel.GetComponentsInChildren<MeshRenderer>(true);
            if (pixelRenderers.Length == 0)
            {
                error = $"Pixel {pixelIndex} has no MeshRenderer.";
                return false;
            }

            foreach (MeshRenderer pixelRenderer in pixelRenderers)
            {
                if (pixelRenderer == null || !pixelRenderer.enabled ||
                    !pixelRenderer.gameObject.activeInHierarchy)
                {
                    continue;
                }

                MeshFilter meshFilter = pixelRenderer.GetComponent<MeshFilter>();
                Mesh mesh = meshFilter != null ? meshFilter.sharedMesh : null;
                Material[] materials = pixelRenderer.sharedMaterials;

                // The authored Pixel and PixelOutline each use one cube submesh.
                // Falling back is safer than silently dropping additional submeshes.
                if (mesh == null || mesh.subMeshCount != 1 ||
                    materials.Length != 1 || materials[0] == null)
                {
                    error = $"Pixel renderer '{pixelRenderer.name}' is not a single-mesh/single-material visual.";
                    return false;
                }

                rendererStates.Add(new RendererState(pixelRenderer));
                visuals.Add(new VisualInstance
                {
                    Transform = pixelRenderer.transform,
                    Renderer = pixelRenderer,
                    Mesh = mesh,
                    Layer = pixelRenderer.gameObject.layer,
                    ShadowCastingMode = pixelRenderer.shadowCastingMode,
                    ReceiveShadows = pixelRenderer.receiveShadows,
                    LightProbeUsage = pixelRenderer.lightProbeUsage,
                    ReflectionProbeUsage = pixelRenderer.reflectionProbeUsage,
                    RenderingLayerMask = pixelRenderer.renderingLayerMask,
                    MotionVectorMode = pixelRenderer.motionVectorGenerationMode,
                });
            }
        }

        if (visuals.Count == 0)
        {
            error = "No enabled Pixel visuals were found.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private bool RebuildDrawBatches(out string error)
    {
        foreach (DrawBatch batch in drawBatches.Values)
            batch.Matrices.Clear();

        hasWorldBounds = false;

        foreach (VisualInstance visual in visuals)
        {
            if (visual.Renderer == null || visual.Transform == null || visual.Mesh == null)
            {
                error = "A cached Pixel visual was destroyed before the grid was rebuilt.";
                return false;
            }

            Material sourceMaterial = visual.Renderer.sharedMaterial;
            if (sourceMaterial == null)
            {
                error = $"Pixel renderer '{visual.Renderer.name}' lost its material.";
                return false;
            }

            var key = new DrawKey(visual, sourceMaterial);
            if (!drawBatches.TryGetValue(key, out DrawBatch batch))
            {
                Material runtimeMaterial = GetOrCreateRuntimeMaterial(sourceMaterial);
                if (runtimeMaterial == null || !runtimeMaterial.enableInstancing)
                {
                    error = $"Material '{sourceMaterial.name}' does not support GPU instancing.";
                    return false;
                }

                batch = new DrawBatch(key, runtimeMaterial);
                drawBatches.Add(key, batch);
            }

            batch.Matrices.Add(visual.Transform.localToWorldMatrix);

            Bounds rendererBounds = visual.Renderer.bounds;
            if (!hasWorldBounds)
            {
                worldBounds = rendererBounds;
                hasWorldBounds = true;
            }
            else
            {
                worldBounds.Encapsulate(rendererBounds);
            }
        }

        groupsDirty = false;
        error = string.Empty;
        return true;
    }

    private Material GetOrCreateRuntimeMaterial(Material sourceMaterial)
    {
        if (runtimeMaterials.TryGetValue(sourceMaterial, out Material runtimeMaterial) &&
            runtimeMaterial != null)
        {
            return runtimeMaterial;
        }

        runtimeMaterial = new Material(sourceMaterial)
        {
            name = $"{sourceMaterial.name} (Game Surface Instanced)",
            hideFlags = HideFlags.DontSave,
            enableInstancing = true,
        };
        runtimeMaterials[sourceMaterial] = runtimeMaterial;
        return runtimeMaterial;
    }

    private void SubscribeToOwnershipChanges(IReadOnlyList<Pixel> pixels)
    {
        foreach (Pixel pixel in pixels)
        {
            if (pixel == null)
                continue;

            PlayerOwnable ownable = pixel.GetComponent<PlayerOwnable>();
            if (ownable == null)
                continue;

            ownable.OnOwnerChanged += HandleOwnerChanged;
            subscribedOwnables.Add(ownable);
        }
    }

    private void UnsubscribeFromOwnershipChanges()
    {
        foreach (PlayerOwnable ownable in subscribedOwnables)
        {
            if (ownable != null)
                ownable.OnOwnerChanged -= HandleOwnerChanged;
        }

        subscribedOwnables.Clear();
    }

    private void HandleOwnerChanged(Player newOwner)
    {
        // PlayerOwnable updates its MeshRenderer immediately after invoking the
        // event. Deferring the regroup to LateUpdate observes the final material.
        groupsDirty = true;
    }

    private void DisableOriginalRenderers()
    {
        foreach (RendererState state in rendererStates)
        {
            if (state.Renderer != null)
                state.Renderer.enabled = false;
        }
    }

    private void RestoreOriginalRenderers()
    {
        foreach (RendererState state in rendererStates)
        {
            if (state.Renderer != null)
                state.Renderer.enabled = state.WasEnabled;
        }
    }

    private void DestroyRuntimeMaterials()
    {
        foreach (Material runtimeMaterial in runtimeMaterials.Values)
        {
            if (runtimeMaterial == null)
                continue;

            if (Application.isPlaying)
                Destroy(runtimeMaterial);
            else
                DestroyImmediate(runtimeMaterial);
        }

        runtimeMaterials.Clear();
    }

    private void FallbackToOriginalRenderers(string reason)
    {
        bool hadManagedRendering = instancedRenderingActive || headlessRenderingDisabled;
        UnsubscribeFromOwnershipChanges();
        RestoreOriginalRenderers();
        DestroyRuntimeMaterials();

        visuals.Clear();
        rendererStates.Clear();
        drawBatches.Clear();
        instancedRenderingActive = false;
        headlessRenderingDisabled = false;
        groupsDirty = false;
        hasWorldBounds = false;

        if (!string.IsNullOrWhiteSpace(reason))
        {
            Debug.LogWarning($"[GameSurfaceRenderer] {reason}", this);
        }
        else if (hadManagedRendering)
        {
            Debug.LogWarning(
                "[GameSurfaceRenderer] Instanced rendering stopped; restored individual Pixel renderers.",
                this);
        }
    }

    private static bool IsHeadlessBuild()
    {
#if UNITY_SERVER
        return true;
#else
        return SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null;
#endif
    }

    private void OnDisable()
    {
        Clear();
    }

    private void OnDestroy()
    {
        Clear();
    }
}
