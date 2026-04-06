using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using DeVect.Combat;
using UnityEngine;

namespace DeVect.Visual;

internal sealed class IceShieldDisplay
{
    private const int DefaultHudLayer = 27;
    private const string DefaultHudSortingLayerName = "HUD";
    private const int DefaultHudSortingOrder = 0;
    private const float DefaultAnchorViewportX = 0.18f;
    private const float DefaultAnchorViewportY = 0.86f;
    private const float LeftHudAnchorViewportOffsetX = 0.11f;
    private const float LeftHudAnchorViewportOffsetY = -0.018f;
    private const float SlotStartWorldOffsetX = -0.92f;
    private const float SlotStartWorldOffsetY = -0.11f;
    private const float HealthMaskWorldOffsetX = -0.5f;
    private const float HealthMaskWorldSpacing = 0.94f;
    private const float HealthMaskWorldOffsetY = -0.31f;
    private const float LeftHudAnchorMinViewportX = -0.02f;
    private const float LeftHudAnchorMaxViewportX = 0.12f;
    private const float LeftHudAnchorMinViewportY = 0.76f;
    private const float LeftHudAnchorMaxViewportY = 1.05f;
    private const float LeftHudMinimumArea = 0.03f;
    private const float RootScale = 1f;
    private const float IconSpacing = 0.19f;
    private const float IconBaseScale = 0.5f;
    private const float GlowBaseScale = 0.74f;
    private const float HaloBaseScale = 0.78f;
    private const float MistBaseScale = 0.8f;
    private const float HighlightBaseScale = 0.66f;
    private const float QuadrantLocalSpreadX = 0f;
    private const float QuadrantLocalSpreadY = 0f;
    private const float PetalPulseAmplitude = 0f;
    private const float PulseAmplitude = 0f;
    private const float BobAmplitude = 0f;
    private const float SwayAmplitude = 0f;
    private const string EmbeddedShieldResourceName = "DeVect.assets.ice_shield_hud.png";

    private static readonly string[] HealthNameKeywords = { "health", "mask", "blue", "joni", "lifeblood", "hp" };
    private static readonly string[] LeftHudNameKeywords = { "soul", "orb", "vessel", "face", "health", "mask", "ui" };
    private static readonly Vector2[] QuadrantPivots =
    {
        new(1f, 0f),
        new(0f, 0f),
        new(1f, 1f),
        new(0f, 1f)
    };
    private static readonly Vector2[] QuadrantDirections =
    {
        new(-1f, 1f),
        new(1f, 1f),
        new(-1f, -1f),
        new(1f, -1f)
    };
    private static readonly Color GlowColor = new(0.48f, 0.78f, 1f, 0.18f);
    private static readonly Color HaloColor = new(0.56f, 0.83f, 1f, 0.12f);
    private static readonly Color MistColor = new(0.8f, 0.96f, 1f, 0.2f);
    private static readonly Color HighlightColor = new(0.94f, 1f, 1f, 0.34f);
    private static readonly Color ShieldColor = new(0.92f, 0.98f, 1f, 0.98f);

    private static Texture2D? _embeddedShieldTexture;
    private static Sprite[]? _quadrantSprites;
    private static Sprite? _glowSprite;
    private static Sprite? _haloSprite;
    private static Sprite? _mistSprite;
    private static Sprite? _highlightSprite;

    private readonly SpriteRenderer[] _glowRenderers = new SpriteRenderer[IceShieldState.MaxShieldLayers];
    private readonly SpriteRenderer?[] _haloRenderers = new SpriteRenderer?[IceShieldState.MaxShieldLayers];
    private readonly SpriteRenderer?[] _mistRenderers = new SpriteRenderer?[IceShieldState.MaxShieldLayers];
    private readonly SpriteRenderer?[] _highlightRenderers = new SpriteRenderer?[IceShieldState.MaxShieldLayers];
    private readonly SpriteRenderer[] _petalRenderers = new SpriteRenderer[IceShieldState.MaxShieldLayers * IceShieldState.PetalsPerShield];
    private readonly Transform[] _slotTransforms = new Transform[IceShieldState.MaxShieldLayers];

    private GameObject? _root;
    private Transform? _rootTransform;
    private HudRenderConfig _currentHudRenderConfig = new(DefaultHudLayer, DefaultHudSortingLayerName, DefaultHudSortingOrder);
    private float _time;

    public void Tick(int petalCount)
    {
        Camera? hudCamera = GameCameras.instance != null ? GameCameras.instance.hudCamera : null;
        if (hudCamera == null || !hudCamera.gameObject.activeInHierarchy)
        {
            SetVisible(false);
            return;
        }

        EnsureBuilt();
        if (_rootTransform == null)
        {
            return;
        }

        _time += Time.deltaTime;
        _rootTransform.position = Vector3.zero;
        _rootTransform.rotation = Quaternion.identity;
        _rootTransform.localScale = new Vector3(RootScale, RootScale, 1f);
        ApplyHudRenderConfig(GetHudRenderConfig(hudCamera));

        bool hasShield = petalCount > 0;
        SetVisible(hasShield);
        if (!hasShield)
        {
            return;
        }

        UpdateIcons(petalCount);
    }

    public void Dispose()
    {
        if (_root != null)
        {
            UnityEngine.Object.Destroy(_root);
        }

        _root = null;
        _rootTransform = null;
        _time = 0f;

        for (int i = 0; i < IceShieldState.MaxShieldLayers; i++)
        {
            _glowRenderers[i] = null!;
            _haloRenderers[i] = null;
            _mistRenderers[i] = null;
            _highlightRenderers[i] = null;
            _slotTransforms[i] = null!;
        }

        for (int i = 0; i < _petalRenderers.Length; i++)
        {
            _petalRenderers[i] = null!;
        }
    }

    private void EnsureBuilt()
    {
        if (_root != null)
        {
            return;
        }

        DestroyDuplicateDisplays();

        _root = new GameObject("DeVect_IceShieldDisplay");
        _rootTransform = _root.transform;
        ApplyHudLayer(_root, _currentHudRenderConfig.HudLayer);
        _rootTransform.position = Vector3.zero;
        _rootTransform.rotation = Quaternion.identity;
        _rootTransform.localScale = new Vector3(RootScale, RootScale, 1f);

        for (int i = 0; i < IceShieldState.MaxShieldLayers; i++)
        {
            GameObject slot = new($"Shield_{i}");
            Transform slotTransform = slot.transform;
            slotTransform.SetParent(_rootTransform, false);
            slotTransform.localPosition = new Vector3(i * IconSpacing, 0f, 0f);
            slotTransform.localRotation = Quaternion.identity;
            slotTransform.localScale = Vector3.one;
            _slotTransforms[i] = slotTransform;

            CreateRendererObject(
                "Glow",
                slotTransform,
                CreateGlowSprite(),
                GlowColor,
                0,
                new Vector3(GlowBaseScale, GlowBaseScale * 0.92f, 1f),
                out SpriteRenderer glowRenderer);
            _glowRenderers[i] = glowRenderer;

            CreateRendererObject(
                "Halo",
                slotTransform,
                CreateHaloSprite(),
                HaloColor,
                1,
                new Vector3(HaloBaseScale, HaloBaseScale * 0.94f, 1f),
                out SpriteRenderer haloRenderer);
            _haloRenderers[i] = haloRenderer;

            CreateRendererObject(
                "Mist",
                slotTransform,
                CreateMistSprite(),
                MistColor,
                2,
                new Vector3(MistBaseScale, MistBaseScale * 0.92f, 1f),
                out SpriteRenderer mistRenderer);
            _mistRenderers[i] = mistRenderer;

            CreateRendererObject(
                "Highlight",
                slotTransform,
                CreateHighlightSprite(),
                HighlightColor,
                3,
                new Vector3(HighlightBaseScale, HighlightBaseScale, 1f),
                out SpriteRenderer highlightRenderer);
            _highlightRenderers[i] = highlightRenderer;

            for (int quadrantIndex = 0; quadrantIndex < IceShieldState.PetalsPerShield; quadrantIndex++)
            {
                CreateRendererObject(
                    $"Petal_{quadrantIndex}",
                    slotTransform,
                    GetQuadrantSprite(quadrantIndex),
                    ShieldColor,
                    2,
                    Vector3.one,
                    out SpriteRenderer petalRenderer);
                petalRenderer.transform.localPosition = GetQuadrantLocalPosition(quadrantIndex);
                _petalRenderers[(i * IceShieldState.PetalsPerShield) + quadrantIndex] = petalRenderer;
            }
        }

        ApplyHudRenderConfig(_currentHudRenderConfig);
    }

    private void UpdateIcons(int petalCount)
    {
        Vector3[] slotPositions = GetSlotWorldPositions();
        Vector3 sharedOffset = GetAnimatedAnchorOffset();

        for (int i = 0; i < IceShieldState.MaxShieldLayers; i++)
        {
            Transform slotTransform = _slotTransforms[i];
            SpriteRenderer glowRenderer = _glowRenderers[i];
            SpriteRenderer? haloRenderer = _haloRenderers[i];
            SpriteRenderer? mistRenderer = _mistRenderers[i];
            SpriteRenderer? highlightRenderer = _highlightRenderers[i];
            if (slotTransform == null || glowRenderer == null || haloRenderer == null || mistRenderer == null || highlightRenderer == null)
            {
                continue;
            }

            int slotPetalCount = Mathf.Clamp(petalCount - (i * IceShieldState.PetalsPerShield), 0, IceShieldState.PetalsPerShield);
            float fill = slotPetalCount / (float)IceShieldState.PetalsPerShield;
            bool active = slotPetalCount > 0;
            glowRenderer.enabled = active;
            haloRenderer.enabled = active;
            mistRenderer.enabled = active;
            highlightRenderer.enabled = active;
            if (!active)
            {
                for (int quadrantIndex = 0; quadrantIndex < IceShieldState.PetalsPerShield; quadrantIndex++)
                {
                    SpriteRenderer petalRenderer = _petalRenderers[(i * IceShieldState.PetalsPerShield) + quadrantIndex];
                    if (petalRenderer != null)
                    {
                        petalRenderer.enabled = false;
                    }
                }
                continue;
            }

            float phase = (_time * 1.7f) + (i * 0.55f);
            float pulse = 1f + (Mathf.Sin(phase) * PulseAmplitude);
            float bob = Mathf.Sin((_time * 2.05f) + (i * 0.4f)) * BobAmplitude;
            float shimmer = 0.98f + (Mathf.Sin((_time * 2.5f) + i) * 0.05f);
            float haloPhase = (_time * 1.08f) + (i * 0.31f);
            float haloScalePulse = 1f + (Mathf.Sin(haloPhase) * 0.018f);
            float mistPhase = (_time * 1.38f) + (i * 0.73f);
            float mistScalePulse = 1f + (Mathf.Sin(mistPhase) * 0.06f);
            float mistAlphaPulse = 0.98f + (Mathf.Sin((_time * 1.74f) + (i * 0.61f)) * 0.05f);
            float highlightPhase = (_time * 1.48f) + (i * 0.42f);
            float highlightPulse = 0.98f + (Mathf.Sin(highlightPhase) * 0.11f);

            Vector3 basePosition = slotPositions[i];
            slotTransform.position = new Vector3(basePosition.x + sharedOffset.x, basePosition.y + sharedOffset.y + bob, 0f);
            slotTransform.rotation = Quaternion.identity;

            for (int quadrantIndex = 0; quadrantIndex < IceShieldState.PetalsPerShield; quadrantIndex++)
            {
                SpriteRenderer petalRenderer = _petalRenderers[(i * IceShieldState.PetalsPerShield) + quadrantIndex];
                if (petalRenderer == null)
                {
                    continue;
                }

                bool petalActive = quadrantIndex < slotPetalCount;
                petalRenderer.enabled = petalActive;
                if (!petalActive)
                {
                    continue;
                }

                float petalPulse = 1f + (Mathf.Sin((_time * 2.35f) + (i * 0.5f) + quadrantIndex) * PetalPulseAmplitude);
                float shieldScale = IconBaseScale * pulse * petalPulse;
                petalRenderer.transform.localPosition = GetQuadrantLocalPosition(quadrantIndex);
                petalRenderer.transform.localScale = new Vector3(shieldScale, shieldScale, 1f);
                petalRenderer.color = new Color(
                    ShieldColor.r,
                    ShieldColor.g,
                    ShieldColor.b,
                    (0.78f + (fill * 0.16f)) * shimmer);
            }

            float glowScale = GlowBaseScale;
            glowRenderer.transform.localScale = new Vector3(glowScale, glowScale * 0.92f, 1f);
            glowRenderer.transform.localPosition = Vector3.zero;
            glowRenderer.color = new Color(
                GlowColor.r,
                GlowColor.g,
                GlowColor.b,
                0.02f + (fill * 0.045f));

            float haloScale = HaloBaseScale * (0.96f + (fill * 0.04f)) * haloScalePulse;
            haloRenderer.transform.localScale = new Vector3(haloScale, haloScale * 0.96f, 1f);
            haloRenderer.transform.localPosition = Vector3.zero;
            haloRenderer.color = new Color(
                HaloColor.r,
                HaloColor.g,
                HaloColor.b,
                (0.024f + (fill * 0.02f)) * shimmer);

            float mistScale = MistBaseScale * (1.06f + (fill * 0.12f)) * mistScalePulse;
            mistRenderer.transform.localScale = new Vector3(mistScale, mistScale * 0.94f, 1f);
            mistRenderer.transform.localPosition = new Vector3(0f, 0.014f, 0f);
            mistRenderer.color = new Color(
                MistColor.r,
                MistColor.g,
                MistColor.b,
                (0.1f + (fill * 0.06f)) * shimmer * mistAlphaPulse);

            float highlightScale = HighlightBaseScale * (0.94f + (fill * 0.06f));
            highlightRenderer.transform.localScale = new Vector3(highlightScale, highlightScale, 1f);
            highlightRenderer.transform.localPosition = new Vector3(-0.02f, 0.046f, 0f);
            highlightRenderer.color = new Color(
                HighlightColor.r,
                HighlightColor.g,
                HighlightColor.b,
                (0.4f + (fill * 0.16f)) * highlightPulse);
        }
    }

    private Vector3 GetAnimatedAnchorOffset()
    {
        return new Vector3(
            Mathf.Sin(_time * 1.15f) * SwayAmplitude,
            Mathf.Sin((_time * 1.8f) + 0.8f) * BobAmplitude,
            0f);
    }

    private Vector3[] GetSlotWorldPositions()
    {
        Camera? hudCamera = GameCameras.instance != null ? GameCameras.instance.hudCamera : null;
        Vector3 anchor = hudCamera != null ? GetHudWorldPosition(hudCamera) : Vector3.zero;
        Vector3[] fallbackPositions = new Vector3[IceShieldState.MaxShieldLayers];
        for (int i = 0; i < fallbackPositions.Length; i++)
        {
            fallbackPositions[i] = new Vector3(
                anchor.x + SlotStartWorldOffsetX + (i * HealthMaskWorldSpacing),
                anchor.y + SlotStartWorldOffsetY,
                0f);
        }

        return fallbackPositions;
    }

    private static GameObject CreateRendererObject(
        string name,
        Transform parent,
        Sprite sprite,
        Color color,
        int sortingOrderOffset,
        Vector3 localScale,
        out SpriteRenderer renderer)
    {
        GameObject obj = new(name);
        Transform transform = obj.transform;
        transform.SetParent(parent, false);
        transform.localScale = localScale;

        renderer = obj.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = color;
        renderer.sortingLayerName = DefaultHudSortingLayerName;
        renderer.sortingOrder = DefaultHudSortingOrder + sortingOrderOffset;
        return obj;
    }

    private static void ApplyHudLayer(GameObject obj, int hudLayer)
    {
        obj.layer = hudLayer;
    }

    private static Vector3 GetHudWorldPosition(Camera hudCamera)
    {
        if (TryGetLeftHudAnchorWorldPosition(hudCamera, out Vector3 anchorWorldPosition))
        {
            return anchorWorldPosition;
        }

        return GetDefaultHudWorldPosition(hudCamera);
    }

    private static Vector3 GetDefaultHudWorldPosition(Camera hudCamera)
    {
        float worldDistance = Mathf.Abs(hudCamera.transform.position.z);
        Vector3 worldPosition = hudCamera.ViewportToWorldPoint(new Vector3(DefaultAnchorViewportX, DefaultAnchorViewportY, worldDistance));
        worldPosition.z = 0f;
        return worldPosition;
    }

    private static bool TryGetLeftHudAnchorWorldPosition(Camera hudCamera, out Vector3 worldPosition)
    {
        worldPosition = default;

        Transform hudRoot = hudCamera.transform.parent != null ? hudCamera.transform.parent : hudCamera.transform;
        if (!TryGetLeftHudAnchorRenderer(hudCamera, hudRoot, requireNameKeyword: true, out Renderer? anchorRenderer) &&
            !TryGetLeftHudAnchorRenderer(hudCamera, hudRoot, requireNameKeyword: false, out anchorRenderer))
        {
            return false;
        }

        if (anchorRenderer == null)
        {
            return false;
        }

        Bounds anchorBounds = anchorRenderer.bounds;
        Vector3 anchorViewport = hudCamera.WorldToViewportPoint(anchorBounds.center);
        float worldDistance = Mathf.Abs(hudCamera.transform.position.z);
        worldPosition = hudCamera.ViewportToWorldPoint(new Vector3(
            Mathf.Clamp01(anchorViewport.x + LeftHudAnchorViewportOffsetX),
            Mathf.Clamp01(anchorViewport.y + LeftHudAnchorViewportOffsetY),
            worldDistance));
        worldPosition.z = 0f;
        return true;
    }

    private static bool TryGetHealthMaskWorldPositions(Camera hudCamera, out Vector3[] worldPositions)
    {
        worldPositions = Array.Empty<Vector3>();

        Transform hudRoot = hudCamera.transform.parent != null ? hudCamera.transform.parent : hudCamera.transform;
        if (!TryGetFirstHealthMaskRenderer(hudCamera, hudRoot, requireHealthKeyword: true, out Renderer? firstMaskRenderer) &&
            !TryGetFirstHealthMaskRenderer(hudCamera, hudRoot, requireHealthKeyword: false, out firstMaskRenderer))
        {
            return false;
        }

        if (firstMaskRenderer == null)
        {
            return false;
        }

        float worldDistance = Mathf.Abs(hudCamera.transform.position.z);
        int count = IceShieldState.MaxShieldLayers;
        worldPositions = new Vector3[IceShieldState.MaxShieldLayers];

        Vector3 firstViewport = hudCamera.WorldToViewportPoint(firstMaskRenderer.bounds.center);
        Vector3 firstWorldPosition = hudCamera.ViewportToWorldPoint(new Vector3(firstViewport.x, firstViewport.y, worldDistance));
        firstWorldPosition.z = 0f;

        for (int i = 0; i < count; i++)
        {
            worldPositions[i] = new Vector3(
                firstWorldPosition.x + HealthMaskWorldOffsetX + (i * HealthMaskWorldSpacing),
                firstWorldPosition.y + HealthMaskWorldOffsetY,
                0f);
        }

        return true;
    }

    private HudRenderConfig GetHudRenderConfig(Camera hudCamera)
    {
        Transform hudRoot = hudCamera.transform.parent != null ? hudCamera.transform.parent : hudCamera.transform;
        return TryGetHealthHudRenderConfig(hudCamera, hudRoot, out HudRenderConfig config)
            ? config
            : new HudRenderConfig(DefaultHudLayer, DefaultHudSortingLayerName, DefaultHudSortingOrder);
    }

    private void ApplyHudRenderConfig(HudRenderConfig config)
    {
        _currentHudRenderConfig = config;

        if (_root != null)
        {
            _root.layer = config.HudLayer;
        }

        for (int i = 0; i < IceShieldState.MaxShieldLayers; i++)
        {
            SpriteRenderer glowRenderer = _glowRenderers[i];
            if (glowRenderer != null)
            {
                glowRenderer.gameObject.layer = config.HudLayer;
                glowRenderer.sortingLayerName = config.SortingLayerName;
                glowRenderer.sortingOrder = config.SortingOrder + 1;
            }

            SpriteRenderer? haloRenderer = _haloRenderers[i];
            if (haloRenderer != null)
            {
                haloRenderer.gameObject.layer = config.HudLayer;
                haloRenderer.sortingLayerName = config.SortingLayerName;
                haloRenderer.sortingOrder = config.SortingOrder + 2;
            }

            SpriteRenderer? mistRenderer = _mistRenderers[i];
            if (mistRenderer != null)
            {
                mistRenderer.gameObject.layer = config.HudLayer;
                mistRenderer.sortingLayerName = config.SortingLayerName;
                mistRenderer.sortingOrder = config.SortingOrder + 3;
            }

            SpriteRenderer? highlightRenderer = _highlightRenderers[i];
            if (highlightRenderer != null)
            {
                highlightRenderer.gameObject.layer = config.HudLayer;
                highlightRenderer.sortingLayerName = config.SortingLayerName;
                highlightRenderer.sortingOrder = config.SortingOrder + 5;
            }

            for (int quadrantIndex = 0; quadrantIndex < IceShieldState.PetalsPerShield; quadrantIndex++)
            {
                SpriteRenderer petalRenderer = _petalRenderers[(i * IceShieldState.PetalsPerShield) + quadrantIndex];
                if (petalRenderer == null)
                {
                    continue;
                }

                petalRenderer.gameObject.layer = config.HudLayer;
                petalRenderer.sortingLayerName = config.SortingLayerName;
                petalRenderer.sortingOrder = config.SortingOrder + 4;
            }
        }
    }

    private static bool TryGetHealthHudRenderConfig(Camera hudCamera, Transform root, out HudRenderConfig config)
    {
        config = new HudRenderConfig(DefaultHudLayer, DefaultHudSortingLayerName, DefaultHudSortingOrder);
        if (!TryGetRightmostHudRenderer(hudCamera, root, requireHealthKeyword: true, out Renderer? bestRenderer) &&
            !TryGetRightmostHudRenderer(hudCamera, root, requireHealthKeyword: false, out bestRenderer))
        {
            return false;
        }

        return TryCreateHudRenderConfig(bestRenderer, out config);
    }

    private static bool TryGetLeftHudAnchorRenderer(Camera hudCamera, Transform root, bool requireNameKeyword, out Renderer? bestRenderer)
    {
        bestRenderer = null;
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        bool found = false;
        float bestScore = float.NegativeInfinity;

        foreach (Renderer renderer in renderers)
        {
            if (!IsEligibleLeftHudAnchorRenderer(hudCamera, renderer, requireNameKeyword))
            {
                continue;
            }

            Bounds bounds = renderer.bounds;
            Vector3 viewport = hudCamera.WorldToViewportPoint(bounds.center);
            float area = bounds.size.x * bounds.size.y;
            float score =
                (area * 10f) -
                Mathf.Abs(viewport.x - 0.05f) -
                (Mathf.Abs(viewport.y - 0.88f) * 0.5f);
            if (!found || score > bestScore)
            {
                bestRenderer = renderer;
                bestScore = score;
                found = true;
            }
        }

        return found;
    }

    private static Vector3 GetQuadrantLocalPosition(int quadrantIndex)
    {
        Vector2 direction = QuadrantDirections[quadrantIndex];
        return new Vector3(direction.x * QuadrantLocalSpreadX, direction.y * QuadrantLocalSpreadY, 0f);
    }

    private static bool TryCreateHudRenderConfig(Renderer? renderer, out HudRenderConfig config)
    {
        if (renderer == null)
        {
            config = new HudRenderConfig(DefaultHudLayer, DefaultHudSortingLayerName, DefaultHudSortingOrder);
            return false;
        }

        string sortingLayerName = renderer.sortingLayerID != 0 ? UnityEngine.SortingLayer.IDToName(renderer.sortingLayerID) : renderer.sortingLayerName;
        if (string.IsNullOrEmpty(sortingLayerName))
        {
            sortingLayerName = DefaultHudSortingLayerName;
        }

        config = new HudRenderConfig(renderer.gameObject.layer, sortingLayerName, renderer.sortingOrder);
        return true;
    }

    private static bool TryGetRightmostHudRenderer(Camera hudCamera, Transform root, bool requireHealthKeyword, out Renderer? bestRenderer)
    {
        bestRenderer = null;
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        bool found = false;
        float bestRightEdge = float.NegativeInfinity;

        foreach (Renderer renderer in renderers)
        {
            if (!IsEligibleHudRenderer(hudCamera, renderer, requireHealthKeyword))
            {
                continue;
            }

            Bounds bounds = renderer.bounds;
            if (!found || bounds.max.x > bestRightEdge)
            {
                bestRenderer = renderer;
                bestRightEdge = bounds.max.x;
                found = true;
            }
        }

        return found;
    }

    private static bool TryGetFirstHealthMaskRenderer(Camera hudCamera, Transform root, bool requireHealthKeyword, out Renderer? bestRenderer)
    {
        bestRenderer = null;
        Renderer[] candidates = root.GetComponentsInChildren<Renderer>(true);
        bool found = false;
        float bestViewportX = float.PositiveInfinity;
        float bestArea = float.NegativeInfinity;

        for (int i = 0; i < candidates.Length; i++)
        {
            Renderer renderer = candidates[i];
            if (!IsEligibleHudRenderer(hudCamera, renderer, requireHealthKeyword))
            {
                continue;
            }

            float viewportX = hudCamera.WorldToViewportPoint(renderer.bounds.center).x;
            float area = renderer.bounds.size.x * renderer.bounds.size.y;
            if (!found || viewportX < bestViewportX - 0.0001f || (Mathf.Abs(viewportX - bestViewportX) <= 0.0001f && area > bestArea))
            {
                bestRenderer = renderer;
                bestViewportX = viewportX;
                bestArea = area;
                found = true;
            }
        }

        return found;
    }

    private static bool IsEligibleHudRenderer(Camera hudCamera, Renderer renderer, bool requireHealthKeyword)
    {
        if (renderer == null ||
            !renderer.gameObject.activeInHierarchy ||
            renderer.gameObject.layer != DefaultHudLayer)
        {
            return false;
        }

        string sortingLayerName = renderer.sortingLayerID != 0 ? UnityEngine.SortingLayer.IDToName(renderer.sortingLayerID) : renderer.sortingLayerName;
        if (sortingLayerName != DefaultHudSortingLayerName)
        {
            return false;
        }

        if (renderer is SpriteRenderer spriteRenderer && spriteRenderer.sprite == null)
        {
            return false;
        }

        if (renderer.gameObject.name.StartsWith("DeVect_", StringComparison.Ordinal))
        {
            return false;
        }

        string objectNameLower = renderer.gameObject.name.ToLowerInvariant();
        bool matchesHealthKeyword = false;
        for (int i = 0; i < HealthNameKeywords.Length; i++)
        {
            if (objectNameLower.Contains(HealthNameKeywords[i]))
            {
                matchesHealthKeyword = true;
                break;
            }
        }

        if (requireHealthKeyword && !matchesHealthKeyword)
        {
            return false;
        }

        Bounds bounds = renderer.bounds;
        Vector3 viewport = hudCamera.WorldToViewportPoint(bounds.center);
        if (viewport.z <= 0f || viewport.y < 0.78f || viewport.y > 1.05f || viewport.x < -0.05f || viewport.x > 0.45f)
        {
            return false;
        }

        Vector3 size = bounds.size;
        return size.x > 0f && size.y > 0f && size.x <= 2.5f && size.y <= 2.5f;
    }

    private static bool IsEligibleLeftHudAnchorRenderer(Camera hudCamera, Renderer renderer, bool requireNameKeyword)
    {
        if (renderer == null ||
            !renderer.gameObject.activeInHierarchy ||
            renderer.gameObject.layer != DefaultHudLayer)
        {
            return false;
        }

        string sortingLayerName = renderer.sortingLayerID != 0 ? UnityEngine.SortingLayer.IDToName(renderer.sortingLayerID) : renderer.sortingLayerName;
        if (sortingLayerName != DefaultHudSortingLayerName)
        {
            return false;
        }

        if (renderer is SpriteRenderer spriteRenderer && spriteRenderer.sprite == null)
        {
            return false;
        }

        if (renderer.gameObject.name.StartsWith("DeVect_", StringComparison.Ordinal))
        {
            return false;
        }

        Bounds bounds = renderer.bounds;
        Vector3 viewport = hudCamera.WorldToViewportPoint(bounds.center);
        if (viewport.z <= 0f ||
            viewport.x < LeftHudAnchorMinViewportX ||
            viewport.x > LeftHudAnchorMaxViewportX ||
            viewport.y < LeftHudAnchorMinViewportY ||
            viewport.y > LeftHudAnchorMaxViewportY)
        {
            return false;
        }

        float area = bounds.size.x * bounds.size.y;
        if (area < LeftHudMinimumArea)
        {
            return false;
        }

        if (!requireNameKeyword)
        {
            return true;
        }

        string objectNameLower = renderer.gameObject.name.ToLowerInvariant();
        for (int i = 0; i < LeftHudNameKeywords.Length; i++)
        {
            if (objectNameLower.Contains(LeftHudNameKeywords[i]))
            {
                return true;
            }
        }

        return false;
    }

    private void DestroyDuplicateDisplays()
    {
        GameObject[] displays = UnityEngine.Object.FindObjectsOfType<GameObject>();
        for (int i = 0; i < displays.Length; i++)
        {
            GameObject display = displays[i];
            if (display == null || display == _root || display.name != "DeVect_IceShieldDisplay")
            {
                continue;
            }

            UnityEngine.Object.Destroy(display);
        }
    }

    private void SetVisible(bool visible)
    {
        if (_root != null && _root.activeSelf != visible)
        {
            _root.SetActive(visible);
        }
    }

    private readonly struct HudRenderConfig
    {
        public HudRenderConfig(int hudLayer, string sortingLayerName, int sortingOrder)
        {
            HudLayer = hudLayer;
            SortingLayerName = sortingLayerName;
            SortingOrder = sortingOrder;
        }

        public int HudLayer { get; }

        public string SortingLayerName { get; }

        public int SortingOrder { get; }
    }

    private static Sprite GetQuadrantSprite(int quadrantIndex)
    {
        if (_quadrantSprites == null)
        {
            _quadrantSprites = CreateQuadrantSprites();
        }

        return _quadrantSprites[quadrantIndex];
    }

    private static Sprite[] CreateQuadrantSprites()
    {
        Texture2D source = LoadEmbeddedShieldTexture();
        bool[] backgroundMask = BuildBackgroundMask(source);
        Sprite[] sprites = new Sprite[IceShieldState.PetalsPerShield];

        for (int quadrantIndex = 0; quadrantIndex < sprites.Length; quadrantIndex++)
        {
            Texture2D quadrantTexture = CreateQuadrantTexture(source, backgroundMask, quadrantIndex);
            sprites[quadrantIndex] = Sprite.Create(
                quadrantTexture,
                new Rect(0f, 0f, quadrantTexture.width, quadrantTexture.height),
                QuadrantPivots[quadrantIndex],
                Mathf.Max(quadrantTexture.width, quadrantTexture.height));
            sprites[quadrantIndex].name = $"DeVect_IceShieldQuadrant_{quadrantIndex}";
        }

        return sprites;
    }

    private static Texture2D LoadEmbeddedShieldTexture()
    {
        if (_embeddedShieldTexture != null)
        {
            return _embeddedShieldTexture;
        }

        Assembly assembly = typeof(IceShieldDisplay).Assembly;
        Stream? resourceStream = assembly.GetManifestResourceStream(EmbeddedShieldResourceName);
        if (resourceStream == null)
        {
            string[] resourceNames = assembly.GetManifestResourceNames();
            for (int i = 0; i < resourceNames.Length; i++)
            {
                string resourceName = resourceNames[i];
                if (resourceName.EndsWith("ice_shield_hud.png", StringComparison.OrdinalIgnoreCase))
                {
                    resourceStream = assembly.GetManifestResourceStream(resourceName);
                    if (resourceStream != null)
                    {
                        break;
                    }
                }
            }
        }

        if (resourceStream == null)
        {
            _embeddedShieldTexture = CreateFallbackShieldTexture();
            return _embeddedShieldTexture;
        }

        using (resourceStream)
        using (MemoryStream buffer = new())
        {
            resourceStream.CopyTo(buffer);
            Texture2D texture = CreateTexture(2, 2, "DeVect_IceShieldEmbeddedSource");
            if (!ImageConversion.LoadImage(texture, buffer.ToArray(), false))
            {
                UnityEngine.Object.Destroy(texture);
                _embeddedShieldTexture = CreateFallbackShieldTexture();
                return _embeddedShieldTexture;
            }

            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            _embeddedShieldTexture = texture;
            return _embeddedShieldTexture;
        }
    }

    private static bool[] BuildBackgroundMask(Texture2D source)
    {
        int width = source.width;
        int height = source.height;
        Color32[] pixels = source.GetPixels32();
        bool[] mask = new bool[pixels.Length];
        int[] queue = new int[pixels.Length];
        int queueStart = 0;
        int queueEnd = 0;

        void EnqueueIfBackground(int x, int y)
        {
            int index = (y * width) + x;
            if (mask[index] || !IsBackgroundCandidate(pixels[index]))
            {
                return;
            }

            mask[index] = true;
            queue[queueEnd++] = index;
        }

        for (int x = 0; x < width; x++)
        {
            EnqueueIfBackground(x, 0);
            EnqueueIfBackground(x, height - 1);
        }

        for (int y = 1; y < height - 1; y++)
        {
            EnqueueIfBackground(0, y);
            EnqueueIfBackground(width - 1, y);
        }

        while (queueStart < queueEnd)
        {
            int index = queue[queueStart++];
            int x = index % width;
            int y = index / width;

            if (x > 0)
            {
                EnqueueIfBackground(x - 1, y);
            }

            if (x < width - 1)
            {
                EnqueueIfBackground(x + 1, y);
            }

            if (y > 0)
            {
                EnqueueIfBackground(x, y - 1);
            }

            if (y < height - 1)
            {
                EnqueueIfBackground(x, y + 1);
            }
        }

        return mask;
    }

    private static bool IsBackgroundCandidate(Color32 color)
    {
        float alpha = color.a / 255f;
        if (alpha <= 0.01f)
        {
            return true;
        }

        float red = color.r / 255f;
        float green = color.g / 255f;
        float blue = color.b / 255f;
        float max = Mathf.Max(red, Mathf.Max(green, blue));
        float min = Mathf.Min(red, Mathf.Min(green, blue));
        float luminance = GetLuminance(new Color(red, green, blue, alpha));
        return alpha >= 0.99f && luminance >= 0.93f && (max - min) <= 0.09f;
    }

    private static Texture2D CreateShieldTexture(Texture2D source, bool[] backgroundMask)
    {
        int width = source.width;
        int height = source.height;
        Color32[] sourcePixels = source.GetPixels32();
        Color[] remappedPixels = new Color[width * height];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int index = (y * width) + x;
                if (backgroundMask[index])
                {
                    remappedPixels[index] = Color.clear;
                    continue;
                }

                Color sourceColor = sourcePixels[index];
                float edgeFactor = GetForegroundEdgeFactor(backgroundMask, x, y, width, height);
                float featherFactor = GetForegroundFeatherFactor(backgroundMask, x, y, width, height);
                float normalizedX = (((x + 0.5f) / width) - 0.5f) * 2f;
                float normalizedY = (((y + 0.5f) / height) - 0.5f) * 2f;
                float shellFactor = GetOpaqueShellFactor(normalizedX, normalizedY, edgeFactor, featherFactor);
                float distortionLikeFactor = GetDistortionLikeFactor(normalizedX, normalizedY, sourceColor);
                float facetLightingFactor = GetFacetLightingFactor(normalizedX, normalizedY, edgeFactor, featherFactor, sourceColor);
                float facetHighlightFactor = GetFacetHighlightFactor(normalizedX, normalizedY, edgeFactor, sourceColor);
                float edgeRimFactor = GetEdgeRimFactor(normalizedX, normalizedY, edgeFactor, featherFactor);
                Color remappedColor = RemapToIcePalette(sourceColor, edgeFactor, shellFactor, facetLightingFactor, facetHighlightFactor, edgeRimFactor, distortionLikeFactor);
                float hollowFactor = GetCenterHollowFactor(sourceColor, x, y, width, height);
                float shadowPlaneFactor = Mathf.Clamp01((1f - facetLightingFactor) * (0.72f + (shellFactor * 0.14f)));
                float shellOpacity = Mathf.Lerp(0.68f, 1f, shellFactor);
                shellOpacity *= Mathf.Lerp(1.08f, 0.92f, facetLightingFactor);
                shellOpacity *= Mathf.Lerp(0.98f, 1.08f, edgeRimFactor);
                shellOpacity *= Mathf.Lerp(0.96f, 1.12f, shadowPlaneFactor);
                float hollowAlpha = Mathf.Lerp(1f, 0.62f, hollowFactor);
                float bodyAlphaFloor = Mathf.Lerp(0.22f, 0.56f, shellFactor);
                bodyAlphaFloor = Mathf.Lerp(bodyAlphaFloor, 0.66f, shadowPlaneFactor * 0.38f);
                bodyAlphaFloor *= Mathf.Lerp(1f, 0.7f, hollowFactor);
                float finalAlpha = Mathf.Max(Mathf.Clamp01(shellOpacity) * hollowAlpha, bodyAlphaFloor);
                finalAlpha = Mathf.Clamp01(finalAlpha + (facetHighlightFactor * 0.035f) + (edgeRimFactor * 0.028f));
                remappedColor.a = sourceColor.a * finalAlpha;
                remappedPixels[index] = remappedColor;
            }
        }

        Texture2D texture = CreateTexture(width, height, "DeVect_IceShieldTexture");
        texture.SetPixels(remappedPixels);
        texture.Apply();
        return texture;
    }

    private static Texture2D CreateQuadrantTexture(Texture2D source, bool[] backgroundMask, int quadrantIndex)
    {
        Texture2D shieldTexture = CreateShieldTexture(source, backgroundMask);
        int fullWidth = shieldTexture.width;
        int fullHeight = shieldTexture.height;
        int halfWidth = fullWidth / 2;
        int halfHeight = fullHeight / 2;
        int sourceX = (quadrantIndex % 2) * halfWidth;
        int sourceY = quadrantIndex < 2 ? halfHeight : 0;
        Color[] pixels = shieldTexture.GetPixels(sourceX, sourceY, halfWidth, halfHeight);
        Texture2D texture = CreateTexture(halfWidth, halfHeight, $"DeVect_IceShieldQuadrantTexture_{quadrantIndex}");
        texture.SetPixels(pixels);
        texture.Apply();
        return texture;
    }

    private static float GetForegroundEdgeFactor(bool[] backgroundMask, int x, int y, int width, int height)
    {
        int backgroundSamples = 0;
        int sampleCount = 0;

        for (int offsetY = -1; offsetY <= 1; offsetY++)
        {
            for (int offsetX = -1; offsetX <= 1; offsetX++)
            {
                if (offsetX == 0 && offsetY == 0)
                {
                    continue;
                }

                int sampleX = x + offsetX;
                int sampleY = y + offsetY;
                sampleCount++;
                if (sampleX < 0 || sampleX >= width || sampleY < 0 || sampleY >= height)
                {
                    backgroundSamples++;
                    continue;
                }

                if (backgroundMask[(sampleY * width) + sampleX])
                {
                    backgroundSamples++;
                }
            }
        }

        if (sampleCount == 0)
        {
            return 0f;
        }

        return backgroundSamples / (float)sampleCount;
    }

    private static float GetForegroundFeatherFactor(bool[] backgroundMask, int x, int y, int width, int height)
    {
        float weightedBackground = 0f;
        float totalWeight = 0f;

        for (int offsetY = -2; offsetY <= 2; offsetY++)
        {
            for (int offsetX = -2; offsetX <= 2; offsetX++)
            {
                if (offsetX == 0 && offsetY == 0)
                {
                    continue;
                }

                float distance = Mathf.Sqrt((offsetX * offsetX) + (offsetY * offsetY));
                if (distance > 2.35f)
                {
                    continue;
                }

                float weight = 1f / (distance + 0.35f);
                int sampleX = x + offsetX;
                int sampleY = y + offsetY;
                totalWeight += weight;

                if (sampleX < 0 || sampleX >= width || sampleY < 0 || sampleY >= height || backgroundMask[(sampleY * width) + sampleX])
                {
                    weightedBackground += weight;
                }
            }
        }

        if (totalWeight <= 0f)
        {
            return 0f;
        }

        return weightedBackground / totalWeight;
    }

    private static float GetCenterHollowFactor(Color sourceColor, int x, int y, int width, int height)
    {
        float centeredX = ((x + 0.5f) / width) - 0.5f;
        float centeredY = ((y + 0.5f) / height) - 0.5f;
        float normalizedX = centeredX / 0.31f;
        float normalizedY = centeredY / 0.31f;
        float distance = Mathf.Sqrt((normalizedX * normalizedX) + (normalizedY * normalizedY));
        float centerWeight = 1f - Mathf.SmoothStep(0.1f, 0.74f, distance);
        float luminance = GetLuminance(sourceColor);
        float brightnessWeight = Mathf.SmoothStep(0.72f, 0.99f, luminance);
        float maxChannel = Mathf.Max(sourceColor.r, Mathf.Max(sourceColor.g, sourceColor.b));
        float minChannel = Mathf.Min(sourceColor.r, Mathf.Min(sourceColor.g, sourceColor.b));
        float whiteness = 1f - Mathf.InverseLerp(0.03f, 0.2f, maxChannel - minChannel);
        float shellRetention = Mathf.SmoothStep(0.12f, 0.48f, distance);
        float hollowFactor = centerWeight * brightnessWeight * whiteness * 0.72f;
        float softCore = distance <= 0.13f ? 0.54f : 0f;
        float focusedHollow = Mathf.Max(softCore, Mathf.Pow(hollowFactor, 1.08f));
        return Mathf.Clamp01(focusedHollow * Mathf.Lerp(1f, 0.82f, shellRetention));
    }

    private static float GetOpaqueShellFactor(float normalizedX, float normalizedY, float edgeFactor, float featherFactor)
    {
        float radialDistance = Mathf.Sqrt((normalizedX * normalizedX) + (normalizedY * normalizedY));
        float shellBand = Mathf.SmoothStep(0.12f, 0.94f, radialDistance);
        float edgePresence = Mathf.SmoothStep(0.01f, 0.84f, Mathf.Max(edgeFactor, featherFactor * 0.98f));
        float interiorMass = 1f - Mathf.SmoothStep(0.16f, 0.74f, radialDistance);
        float topBias = Mathf.Clamp01(((-normalizedX * 0.32f) + (normalizedY * 0.92f) + 0.74f) * 0.54f);
        float lowerDepth = Mathf.Clamp01(((normalizedX * 0.18f) - (normalizedY * 0.94f) + 0.8f) * 0.44f);
        return Mathf.Clamp01((shellBand * 0.36f) + (edgePresence * 0.34f) + (interiorMass * 0.12f) + (topBias * 0.12f) + (lowerDepth * 0.06f));
    }

    private static float GetDistortionLikeFactor(float normalizedX, float normalizedY, Color sourceColor)
    {
        float waveA = 0.5f + (0.5f * Mathf.Sin((normalizedX * 7.6f) - (normalizedY * 5.1f) + 0.3f));
        float waveB = 0.5f + (0.5f * Mathf.Cos((normalizedX * 4.2f) + (normalizedY * 8.4f) - 0.6f));
        float luminance = GetLuminance(sourceColor);
        return Mathf.Clamp01(((waveA * 0.58f) + (waveB * 0.42f)) * Mathf.Lerp(0.8f, 1.04f, luminance));
    }

    private static float GetFacetLightingFactor(float normalizedX, float normalizedY, float edgeFactor, float featherFactor, Color sourceColor)
    {
        float radialDistance = Mathf.Sqrt((normalizedX * normalizedX) + (normalizedY * normalizedY));
        float primaryFacet = Mathf.Clamp01(((-normalizedX * 1.02f) + (normalizedY * 1.18f) + 0.62f) * 0.54f);
        float secondaryFacet = Mathf.Clamp01(((-normalizedX * 0.18f) + (normalizedY * 0.82f) + 0.38f) * 0.56f);
        float sideFacet = Mathf.Clamp01(((normalizedX * 0.92f) + (normalizedY * 0.16f) + 0.48f) * 0.44f);
        float lowerShadow = Mathf.Clamp01(((normalizedX * 0.18f) - (normalizedY * 1.28f) + 0.8f) * 0.58f);
        float deepShell = Mathf.Clamp01(((normalizedX * 0.74f) - (normalizedY * 0.64f) + 0.22f) * 0.64f);
        float seamFacet = Mathf.SmoothStep(0.22f, 0.92f, Mathf.Clamp01(1f - Mathf.Abs((normalizedY * 0.96f) - (normalizedX * 0.58f) + 0.02f) * 2.8f));
        float crestFacet = Mathf.SmoothStep(0.3f, 0.96f, Mathf.Clamp01(1f - Mathf.Abs((normalizedY * 1.22f) + (normalizedX * 0.16f) + 0.08f) * 3.6f));
        float edgeShadow = Mathf.SmoothStep(0.08f, 0.9f, Mathf.Max(edgeFactor, featherFactor * 0.92f));
        float interiorMask = 1f - Mathf.SmoothStep(0.74f, 1.04f, radialDistance);
        float rawLighting =
            (primaryFacet * 0.48f) +
            (secondaryFacet * 0.14f) +
            (sideFacet * 0.08f) +
            (seamFacet * 0.14f) +
            (crestFacet * 0.1f) +
            (GetLuminance(sourceColor) * 0.06f) -
            (lowerShadow * 0.22f) -
            (deepShell * 0.2f) -
            (edgeShadow * 0.1f);
        float compressedLighting = Mathf.Pow(Mathf.Clamp01((rawLighting * interiorMask) + (primaryFacet * 0.08f)), 1.18f);
        float steppedLighting = Mathf.Floor(Mathf.Clamp01(compressedLighting) * 4f) / 4f;
        float facetContrast = Mathf.Lerp(0.92f, 1.12f, Mathf.Max(seamFacet, crestFacet * 0.84f));
        return Mathf.Clamp01(Mathf.Lerp(compressedLighting, steppedLighting, 0.72f) * facetContrast);
    }

    private static float GetFacetHighlightFactor(float normalizedX, float normalizedY, float edgeFactor, Color sourceColor)
    {
        float radialDistance = Mathf.Sqrt((normalizedX * normalizedX) + (normalizedY * normalizedY));
        float primarySlash = Mathf.Clamp01(1f - Mathf.Abs((normalizedY * 1.38f) + (normalizedX * 1.06f) + 0.04f) * 16.6f);
        float crossSlash = Mathf.Clamp01(1f - Mathf.Abs((normalizedY * 0.96f) - (normalizedX * 1.34f) + 0.1f) * 20.4f);
        float crestShard = Mathf.Clamp01(1f - Mathf.Abs(Mathf.Sqrt(((normalizedX + 0.12f) * (normalizedX + 0.12f) * 1.4f) + ((normalizedY + 0.24f) * (normalizedY + 0.24f) * 1.78f)) - 0.38f) * 20.8f);
        float upperSpark = Mathf.Clamp01(1f - Mathf.Abs((normalizedY * 1.74f) + (normalizedX * 0.08f) + 0.78f) * 19.4f);
        float interiorMask = 1f - Mathf.SmoothStep(0.62f, 0.96f, radialDistance);
        float lightFacingMask = Mathf.Clamp01(((-normalizedX * 0.96f) + (normalizedY * 1.28f) + 0.32f) * 0.62f);
        float sourceBrightness = Mathf.SmoothStep(0.42f, 0.96f, GetLuminance(sourceColor));
        float edgeAssist = Mathf.SmoothStep(0.16f, 0.88f, edgeFactor);
        float glint = Mathf.Max(primarySlash * 0.76f, Mathf.Max(crossSlash * 0.46f, crestShard * 0.62f));
        glint = Mathf.Max(glint, upperSpark * 0.54f);
        glint += crossSlash * 0.08f;
        glint *= interiorMask * Mathf.Lerp(0.94f, 1.12f, sourceBrightness);
        glint *= Mathf.Lerp(0.96f, 1.1f, edgeAssist);
        glint *= Mathf.Lerp(0.78f, 1.12f, lightFacingMask);
        return Mathf.Clamp01(Mathf.Pow(glint, 1.52f));
    }

    private static float GetEdgeRimFactor(float normalizedX, float normalizedY, float edgeFactor, float featherFactor)
    {
        float radialDistance = Mathf.Sqrt((normalizedX * normalizedX) + (normalizedY * normalizedY));
        float angle = Mathf.Atan2(normalizedY, normalizedX);
        float edgeProximity = Mathf.SmoothStep(0.08f, 0.86f, Mathf.Max(edgeFactor, featherFactor * 0.96f));
        float rimVariation = 0.5f + (0.5f * Mathf.Sin((angle * 5.8f) + (radialDistance * 7.2f)));
        rimVariation = Mathf.Lerp(rimVariation, 0.5f + (0.5f * Mathf.Cos((angle * 9.6f) - 0.4f)), 0.28f);
        float lightFacingRim = Mathf.Clamp01(((-normalizedX * 0.82f) + (normalizedY * 1.08f) + 0.34f) * 0.58f);
        float lowerCatch = Mathf.Clamp01(((normalizedX * 0.12f) - (normalizedY * 1.2f) + 0.82f) * 0.4f);
        float radialMask = Mathf.SmoothStep(0.42f, 1.04f, radialDistance);
        float rim = edgeProximity * radialMask * (0.56f + (lightFacingRim * 0.34f) + (lowerCatch * 0.1f));
        rim *= Mathf.Lerp(0.9f, 1.08f, rimVariation);
        return Mathf.Clamp01(rim);
    }

    private static Color RemapToIcePalette(Color sourceColor, float edgeFactor, float shellFactor, float facetLightingFactor, float facetHighlightFactor, float edgeRimFactor, float distortionLikeFactor)
    {
        float luminance = GetLuminance(sourceColor);
        float maxChannel = Mathf.Max(sourceColor.r, Mathf.Max(sourceColor.g, sourceColor.b));
        float minChannel = Mathf.Min(sourceColor.r, Mathf.Min(sourceColor.g, sourceColor.b));
        float contrast = maxChannel - minChannel;

        Color abyssIce = new(0.05f, 0.11f, 0.29f, 1f);
        Color deepIce = new(0.08f, 0.2f, 0.48f, 1f);
        Color bodyIce = new(0.18f, 0.48f, 0.78f, 1f);
        Color crystalIce = new(0.42f, 0.76f, 0.95f, 1f);
        Color frostIce = new(0.88f, 0.97f, 1f, 1f);
        Color brightFrost = new(1f, 1f, 1f, 1f);

        float baseMix = Mathf.Clamp01((luminance * 0.36f) + (contrast * 0.14f) + (shellFactor * 0.18f) + (distortionLikeFactor * 0.04f));
        Color baseColor = Color.Lerp(abyssIce, deepIce, Mathf.SmoothStep(0.02f, 0.4f, baseMix));

        float bodyMask = Mathf.SmoothStep(0.38f, 0.82f, facetLightingFactor + (shellFactor * 0.06f) + (luminance * 0.08f));
        baseColor = Color.Lerp(baseColor, bodyIce, bodyMask * 0.88f);

        float crystalMask = Mathf.SmoothStep(0.68f, 0.94f, facetLightingFactor + (edgeRimFactor * 0.08f) + (facetHighlightFactor * 0.12f));
        baseColor = Color.Lerp(baseColor, crystalIce, crystalMask * 0.52f);

        float frostMask = Mathf.SmoothStep(0.82f, 0.995f, facetLightingFactor + (facetHighlightFactor * 0.42f) + (edgeRimFactor * 0.08f));
        baseColor = Color.Lerp(baseColor, frostIce, frostMask * 0.34f);

        float rimBlend = Mathf.SmoothStep(0.62f, 0.98f, edgeRimFactor) * Mathf.SmoothStep(0.52f, 0.94f, facetLightingFactor + 0.12f);
        baseColor = Color.Lerp(baseColor, brightFrost, (rimBlend * 0.2f) + (facetHighlightFactor * 0.24f));

        float shadowBlend = Mathf.Clamp01(((1f - facetLightingFactor) * 0.72f) + ((1f - luminance) * 0.08f) + (Mathf.SmoothStep(0.08f, 0.68f, shellFactor) * 0.08f) - (facetHighlightFactor * 0.18f));
        baseColor = Color.Lerp(baseColor, abyssIce, shadowBlend * 0.38f);

        float coolDepth = Mathf.SmoothStep(0.14f, 0.9f, edgeFactor) * (1f - rimBlend) * 0.18f;
        baseColor = Color.Lerp(baseColor, deepIce, coolDepth);
        baseColor.a = 1f;
        return baseColor;
    }

    private static float GetLuminance(Color color)
    {
        return (color.r * 0.2126f) + (color.g * 0.7152f) + (color.b * 0.0722f);
    }

    private static Texture2D CreateFallbackShieldTexture()
    {
        const int size = 128;
        Texture2D texture = CreateTexture(size, size, "DeVect_IceShieldFallbackSource");
        Vector2 center = new(size * 0.5f, size * 0.5f);
        float innerRadius = size * 0.18f;
        float outerRadius = size * 0.36f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 delta = new(x - center.x, y - center.y);
                float angle = Mathf.Atan2(delta.y, delta.x);
                float petals = 0.26f + (0.12f * Mathf.Cos(angle * 4f));
                float radius = delta.magnitude / size;
                bool insideFlower = radius <= outerRadius / size + petals;
                bool insideStar = Mathf.Abs(delta.x) <= innerRadius || Mathf.Abs(delta.y) <= innerRadius;

                if (!insideFlower)
                {
                    texture.SetPixel(x, y, Color.clear);
                    continue;
                }

                Color color = insideStar
                    ? new Color(0.95f, 0.99f, 1f, 1f)
                    : new Color(0.2f, 0.52f, 0.92f, 1f);
                texture.SetPixel(x, y, color);
            }
        }

        texture.Apply();
        return texture;
    }

    private static Sprite CreateGlowSprite()
    {
        if (_glowSprite != null)
        {
            return _glowSprite;
        }

        const int width = 128;
        const int height = 110;
        Texture2D texture = CreateTexture(width, height, "DeVect_IceShieldGlow");
        Vector2 center = new(width * 0.5f, height * 0.5f);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float nx = (x - center.x) / (width * 0.44f);
                float ny = (y - center.y) / (height * 0.34f);
                float distance = Mathf.Sqrt((nx * nx) + (ny * ny));
                float ring = Mathf.Clamp01(1f - Mathf.Abs(distance - 0.72f) * 2.6f);
                float core = Mathf.Clamp01(1f - distance * 1.35f);
                float alpha = (ring * 0.22f) + (core * 0.08f);
                texture.SetPixel(x, y, new Color(0.7f, 0.92f, 1f, alpha));
            }
        }

        texture.Apply();
        _glowSprite = Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), width);
        _glowSprite.name = "DeVect_IceShieldGlowSprite";
        return _glowSprite;
    }

    private static Sprite CreateHaloSprite()
    {
        if (_haloSprite != null)
        {
            return _haloSprite;
        }

        const int width = 136;
        const int height = 120;
        Texture2D texture = CreateTexture(width, height, "DeVect_IceShieldHalo");
        Vector2 center = new(width * 0.5f, height * 0.5f);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float nx = (x - center.x) / (width * 0.45f);
                float ny = (y - center.y) / (height * 0.39f);
                float distance = Mathf.Sqrt((nx * nx) + (ny * ny));
                float outerHalo = Mathf.Clamp01(1f - Mathf.Abs(distance - 0.8f) * 3.2f);
                float softHaze = Mathf.Clamp01(1f - Mathf.Abs(distance - 0.68f) * 4.1f);
                float alpha = (outerHalo * 0.1f) + (softHaze * 0.03f);
                texture.SetPixel(x, y, new Color(0.72f, 0.93f, 1f, alpha));
            }
        }

        texture.Apply();
        _haloSprite = Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), width);
        _haloSprite.name = "DeVect_IceShieldHaloSprite";
        return _haloSprite;
    }

    private static Sprite CreateMistSprite()
    {
        if (_mistSprite != null)
        {
            return _mistSprite;
        }

        const int width = 156;
        const int height = 138;
        Texture2D texture = CreateTexture(width, height, "DeVect_IceShieldMist");
        Vector2 center = new(width * 0.5f, height * 0.5f);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float nx = (x - center.x) / (width * 0.45f);
                float ny = (y - center.y) / (height * 0.39f);
                float distance = Mathf.Sqrt((nx * nx) + (ny * ny));
                float angle = Mathf.Atan2(ny, nx);
                float boundaryRadius =
                    0.68f +
                    (Mathf.Sin((angle * 3.2f) + 0.18f) * 0.026f) +
                    (Mathf.Sin((angle * 7.4f) - 0.42f) * 0.03f) +
                    (Mathf.Cos((angle * 10.8f) + 0.94f) * 0.018f);
                float innerRadius = boundaryRadius - (0.06f + (Mathf.Sin((angle * 5.4f) + (distance * 5.8f)) * 0.01f));
                float edgeHuggingBand = Mathf.Clamp01(1f - Mathf.Abs(distance - boundaryRadius) * 18.6f);
                float outerWispBandA = Mathf.Clamp01(1f - Mathf.Abs(distance - (boundaryRadius + 0.056f + (Mathf.Sin((angle * 4.8f) - 0.2f) * 0.018f))) * 10.4f);
                float outerWispBandB = Mathf.Clamp01(1f - Mathf.Abs(distance - (boundaryRadius + 0.104f + (Mathf.Cos((angle * 6.2f) + 0.36f) * 0.024f))) * 8.8f);
                float outerSprayBand = Mathf.Clamp01(1f - Mathf.Abs(distance - (boundaryRadius + 0.15f + (Mathf.Sin((angle * 9.2f) + 0.44f) * 0.022f))) * 7.2f);
                float interiorSuppression = Mathf.SmoothStep(innerRadius - 0.012f, innerRadius + 0.05f, distance);
                float outerFade = 1f - Mathf.SmoothStep(boundaryRadius + 0.15f, boundaryRadius + 0.28f, distance);
                float wispNoise = 0.5f + (0.5f * Mathf.Sin((angle * 8.6f) - (distance * 13.4f) + 0.24f));
                float secondaryNoise = 0.5f + (0.5f * Mathf.Cos((angle * 12.8f) + (distance * 9.4f) - 0.68f));
                float upperDrift = Mathf.Clamp01((ny + 0.18f) * 0.88f);
                float lightFacingBias = Mathf.Clamp01(((-nx * 0.48f) + (ny * 1.08f) + 0.26f) * 0.7f);
                float alpha = interiorSuppression * outerFade * (
                    (edgeHuggingBand * (0.048f + (wispNoise * 0.026f))) +
                    (outerWispBandA * (0.072f + (upperDrift * 0.058f) + (lightFacingBias * 0.022f))) +
                    (outerWispBandB * (0.05f + (upperDrift * 0.05f) + (secondaryNoise * 0.024f))) +
                    (outerSprayBand * (0.028f + (upperDrift * 0.038f) + (wispNoise * 0.018f))));
                texture.SetPixel(x, y, new Color(0.8f, 0.97f, 1f, alpha));
            }
        }

        texture.Apply();
        _mistSprite = Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), width);
        _mistSprite.name = "DeVect_IceShieldMistSprite";
        return _mistSprite;
    }

    private static Sprite CreateHighlightSprite()
    {
        if (_highlightSprite != null)
        {
            return _highlightSprite;
        }

        const int width = 120;
        const int height = 112;
        Texture2D texture = CreateTexture(width, height, "DeVect_IceShieldHighlight");
        Vector2 center = new(width * 0.5f, height * 0.5f);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float nx = (x - center.x) / (width * 0.5f);
                float ny = (y - center.y) / (height * 0.5f);
                float slashCore = Mathf.Clamp01(1f - Mathf.Abs((ny * 1.48f) + (nx * 1.02f) + 0.02f) * 18.4f);
                float slashSheen = Mathf.Clamp01(1f - Mathf.Abs((ny * 1.48f) + (nx * 1.02f) + 0.02f) * 10.8f);
                float crossCore = Mathf.Clamp01(1f - Mathf.Abs((ny * 0.98f) - (nx * 1.34f) + 0.11f) * 21.8f);
                float crestCore = Mathf.Clamp01(1f - Mathf.Abs(Mathf.Sqrt(((nx + 0.1f) * (nx + 0.1f) * 1.28f) + ((ny - 0.06f) * (ny - 0.06f) * 1.64f)) - 0.24f) * 18.4f);
                float tipShard = Mathf.Clamp01(1f - Mathf.Abs((ny * 1.8f) + (nx * 0.08f) + 0.84f) * 20f);
                float pointSpark = Mathf.Clamp01(1f - Mathf.Sqrt(((nx + 0.02f) * (nx + 0.02f) * 24f) + ((ny + 0.54f) * (ny + 0.54f) * 38f)));
                float alpha =
                    (slashCore * 0.54f) +
                    (slashSheen * 0.14f) +
                    (crossCore * 0.24f) +
                    (crestCore * 0.34f) +
                    (tipShard * 0.24f) +
                    (pointSpark * 0.42f);
                alpha = Mathf.Clamp01(Mathf.Pow(alpha, 1.14f));
                texture.SetPixel(x, y, new Color(0.94f, 1f, 1f, alpha));
            }
        }

        texture.Apply();
        _highlightSprite = Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), width);
        _highlightSprite.name = "DeVect_IceShieldHighlightSprite";
        return _highlightSprite;
    }

    private static Texture2D CreateTexture(int width, int height, string name)
    {
        return new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            name = name
        };
    }
}
