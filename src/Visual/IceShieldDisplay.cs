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
    private const float LeftHudAnchorViewportOffsetY = -0.02f;
    private const int PetalsPerLayer = IceShieldState.PetalsPerShield;
    private const int LayerCount = IceShieldState.MaxShieldLayers;
    private const float BaseScale = 0.2f;
    private const float LayerScaleStep = 0.14f;
    private const float CoreBaseScale = 0.3f;
    private const float MistBaseScale = 1.5f;
    private const float LayerBobAmplitude = 0.01f;
    private const float LayerSwayAmplitude = 0.008f;
    private const float JitterAmplitude = 0.008f;
    private const float JitterFrequency = 1.9f;
    private const string SortingLayer = "HUD";
    private const int CoreSortingOrder = 5;
    private const int LayerSortingBase = 6;
    private const int MistSortingOrder = 4;
    private const float LeftHudAnchorMinViewportX = -0.02f;
    private const float LeftHudAnchorMaxViewportX = 0.12f;
    private const float LeftHudAnchorMinViewportY = 0.76f;
    private const float LeftHudAnchorMaxViewportY = 1.05f;
    private const float LeftHudMinimumArea = 0.03f;
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
    private static readonly Color CoreColor = new(0.88f, 0.97f, 1f, 0.22f);
    private static readonly Color MistColor = new(0.58f, 0.87f, 1f, 0.24f);
    private static readonly Color ActivePetalColor = new(0.98f, 1f, 1f, 0.96f);
    private static readonly Color InactivePetalColor = new(0.64f, 0.8f, 0.95f, 0.12f);
    private static readonly Color AccentColor = new(0.9f, 0.98f, 1f, 0.16f);

    private static Sprite[]? _quadrantSprites;
    private static Texture2D? _embeddedShieldTexture;
    private static Sprite? _mistSprite;
    private static Sprite? _coreSprite;
    private static Sprite? _accentSprite;

    private GameObject? _root;
    private Transform? _rootTransform;
    private SpriteRenderer? _coreRenderer;
    private SpriteRenderer? _mistRenderer;
    private SpriteRenderer? _accentRenderer;
    private readonly SpriteRenderer[] _petalRenderers = new SpriteRenderer[LayerCount * PetalsPerLayer];
    private readonly Transform[] _petalTransforms = new Transform[LayerCount * PetalsPerLayer];
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
        _rootTransform.position = GetHudWorldPosition(hudCamera) + GetAnimatedAnchorOffset();
        _rootTransform.rotation = Quaternion.identity;
        _rootTransform.localScale = new Vector3(BaseScale, BaseScale, 1f);
        ApplyHudRenderConfig(GetHudRenderConfig(hudCamera));

        bool hasShield = petalCount > 0;
        SetVisible(hasShield);
        if (!hasShield)
        {
            return;
        }

        float shieldFill = petalCount / (float)IceShieldState.MaxPetals;
        UpdateCore(shieldFill);
        UpdateMist(shieldFill);
        UpdateAccent(shieldFill);
        UpdatePetals(petalCount, shieldFill);
    }

    public void Dispose()
    {
        if (_root != null)
        {
            UnityEngine.Object.Destroy(_root);
        }

        _root = null;
        _rootTransform = null;
        _coreRenderer = null;
        _mistRenderer = null;
        _accentRenderer = null;
        _time = 0f;
        for (int i = 0; i < _petalRenderers.Length; i++)
        {
            _petalRenderers[i] = null!;
            _petalTransforms[i] = null!;
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
        _rootTransform.localScale = new Vector3(BaseScale, BaseScale, 1f);

        GameObject mist = CreateRendererObject("Mist", _rootTransform, CreateMistSprite(), MistColor, MistSortingOrder, new Vector3(MistBaseScale, MistBaseScale * 0.9f, 1f), out SpriteRenderer mistRenderer);
        mist.transform.localPosition = new Vector3(0f, 0.02f, 0f);
        _mistRenderer = mistRenderer;

        GameObject accent = CreateRendererObject("Accent", _rootTransform, CreateAccentSprite(), AccentColor, CoreSortingOrder + 1, new Vector3(0.72f, 0.8f, 1f), out SpriteRenderer accentRenderer);
        accent.transform.localPosition = new Vector3(0f, 0.01f, 0f);
        _accentRenderer = accentRenderer;

        GameObject core = CreateRendererObject("Core", _rootTransform, CreateCoreSprite(), CoreColor, CoreSortingOrder, new Vector3(CoreBaseScale, CoreBaseScale, 1f), out SpriteRenderer coreRenderer);
        _coreRenderer = coreRenderer;

        for (int layerIndex = 0; layerIndex < LayerCount; layerIndex++)
        {
            for (int petalIndex = 0; petalIndex < PetalsPerLayer; petalIndex++)
            {
                int index = (layerIndex * PetalsPerLayer) + petalIndex;
                GameObject petal = CreateRendererObject(
                    $"Petal_{layerIndex}_{petalIndex}",
                    _rootTransform,
                    GetQuadrantSprite(petalIndex),
                    InactivePetalColor,
                    LayerSortingBase + layerIndex,
                    GetPetalBaseScale(layerIndex),
                    out SpriteRenderer petalRenderer
                );

                Transform petalTransform = petal.transform;
                petalTransform.localPosition = GetQuadrantLocalPosition(layerIndex, petalIndex, 0f);
                petalTransform.localRotation = Quaternion.identity;
                _petalRenderers[index] = petalRenderer;
                _petalTransforms[index] = petalTransform;
            }
        }

        ApplyHudRenderConfig(_currentHudRenderConfig);
    }

    private static GameObject CreateRendererObject(
        string name,
        Transform parent,
        Sprite sprite,
        Color color,
        int sortingOrder,
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
        renderer.sortingLayerName = SortingLayer;
        renderer.sortingOrder = sortingOrder;
        return obj;
    }

    private static void ApplyHudLayer(GameObject obj, int hudLayer)
    {
        obj.layer = hudLayer;
    }

    private void UpdateCore(float shieldFill)
    {
        if (_coreRenderer == null)
        {
            return;
        }

        float pulse = 1f + (Mathf.Sin((_time * 1.45f) + 0.6f) * 0.06f);
        _coreRenderer.transform.localScale = new Vector3(CoreBaseScale, CoreBaseScale, 1f) * pulse;
        _coreRenderer.color = Color.Lerp(
            new Color(0.54f, 0.84f, 1f, 0.08f),
            new Color(0.9f, 0.98f, 1f, 0.22f),
            shieldFill);
    }

    private void UpdateMist(float shieldFill)
    {
        if (_mistRenderer == null)
        {
            return;
        }

        float swirl = Mathf.Sin((_time * 0.92f) + 1.2f);
        float drift = Mathf.Cos((_time * 0.61f) + 0.8f);
        _mistRenderer.transform.localScale = new Vector3(
            MistBaseScale + (shieldFill * 0.3f) + (swirl * 0.08f),
            (MistBaseScale * 0.9f) + (shieldFill * 0.22f) - (swirl * 0.05f),
            1f);
        _mistRenderer.transform.localPosition = new Vector3(swirl * 0.026f, 0.018f + (shieldFill * 0.018f) + (drift * 0.012f), 0f);
        _mistRenderer.transform.localRotation = Quaternion.Euler(0f, 0f, swirl * 2.2f);
        _mistRenderer.color = new Color(0.52f, 0.84f, 1f, 0.11f + (shieldFill * 0.13f));
    }

    private void UpdateAccent(float shieldFill)
    {
        if (_accentRenderer == null)
        {
            return;
        }

        float shimmer = Mathf.Sin((_time * 1.88f) + 2.1f);
        _accentRenderer.transform.localScale = new Vector3(0.66f + (shieldFill * 0.1f), 0.78f + (shieldFill * 0.14f), 1f);
        _accentRenderer.transform.localRotation = Quaternion.Euler(0f, 0f, shimmer * 4f);
        _accentRenderer.color = new Color(0.94f, 0.99f, 1f, 0.04f + (shieldFill * 0.1f));
    }

    private void UpdatePetals(int petalCount, float shieldFill)
    {
        for (int i = 0; i < _petalRenderers.Length; i++)
        {
            SpriteRenderer renderer = _petalRenderers[i];
            Transform transform = _petalTransforms[i];
            if (renderer == null || transform == null)
            {
                continue;
            }

            int layerIndex = i / PetalsPerLayer;
            int quadrantIndex = i % PetalsPerLayer;
            bool active = i < petalCount;
            float layerPhase = (_time * (0.92f + (layerIndex * 0.08f))) + (quadrantIndex * 0.55f);
            Vector3 basePosition = GetQuadrantLocalPosition(layerIndex, quadrantIndex, shieldFill);
            Vector2 quadrantDirection = QuadrantDirections[quadrantIndex];
            Vector3 drift = new(
                (Mathf.Sin(layerPhase * JitterFrequency) * JitterAmplitude * 0.45f) + (quadrantDirection.x * Mathf.Sin(layerPhase * 0.78f) * 0.01f),
                (Mathf.Cos((layerPhase * (JitterFrequency - 0.31f)) + 0.8f) * JitterAmplitude * 0.38f) + (quadrantDirection.y * Mathf.Cos((layerPhase * 0.66f) + 0.4f) * 0.01f),
                0f);
            float sway = Mathf.Sin((_time * 1.08f) + (layerIndex * 0.36f) + quadrantIndex) * 2.4f;
            float pulse = active ? 1f + (Mathf.Sin((_time * 2.1f) + (i * 0.7f)) * 0.025f) : 0.98f;

            transform.localPosition = basePosition + drift;
            transform.localRotation = Quaternion.Euler(0f, 0f, sway);
            transform.localScale = GetPetalBaseScale(layerIndex) * (1f + (shieldFill * 0.06f)) * pulse;
            renderer.enabled = active || layerIndex == 0;

            float layerAlpha = Mathf.Clamp01(1f - (layerIndex * 0.14f));
            Color targetColor = active
                ? Color.Lerp(new Color(0.84f, 0.94f, 1f, 0.66f), ActivePetalColor, shieldFill * 0.8f)
                : InactivePetalColor;
            float alphaWave = active ? 0.94f + (Mathf.Sin((_time * 2.18f) + i) * 0.06f) : 1f;
            renderer.color = new Color(
                targetColor.r,
                targetColor.g,
                targetColor.b,
                targetColor.a * layerAlpha * alphaWave);
        }
    }

    private Vector3 GetAnimatedAnchorOffset()
    {
        return new Vector3(
            Mathf.Sin(_time * 1.25f) * LayerSwayAmplitude,
            Mathf.Sin((_time * 1.75f) + 0.8f) * LayerBobAmplitude,
            0f);
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

        if (_mistRenderer != null)
        {
            _mistRenderer.gameObject.layer = config.HudLayer;
            _mistRenderer.sortingLayerName = config.SortingLayerName;
            _mistRenderer.sortingOrder = config.SortingOrder - 1;
        }

        if (_coreRenderer != null)
        {
            _coreRenderer.gameObject.layer = config.HudLayer;
            _coreRenderer.sortingLayerName = config.SortingLayerName;
            _coreRenderer.sortingOrder = config.SortingOrder;
        }

        if (_accentRenderer != null)
        {
            _accentRenderer.gameObject.layer = config.HudLayer;
            _accentRenderer.sortingLayerName = config.SortingLayerName;
            _accentRenderer.sortingOrder = config.SortingOrder + 1;
        }

        for (int i = 0; i < _petalRenderers.Length; i++)
        {
            SpriteRenderer? renderer = _petalRenderers[i];
            if (renderer == null)
            {
                continue;
            }

            renderer.gameObject.layer = config.HudLayer;
            renderer.sortingLayerName = config.SortingLayerName;
            renderer.sortingOrder = config.SortingOrder + 2 + (i / PetalsPerLayer);
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

        if (renderer.gameObject.name.StartsWith("DeVect_"))
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

        if (renderer.gameObject.name.StartsWith("DeVect_"))
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

    private static Vector3 GetPetalBaseScale(int layerIndex)
    {
        float scale = 0.62f + (layerIndex * LayerScaleStep);
        return new Vector3(scale, scale, 1f);
    }

    private static Vector3 GetQuadrantLocalPosition(int layerIndex, int quadrantIndex, float shieldFill)
    {
        Vector2 direction = QuadrantDirections[quadrantIndex];
        float layerSpread = 0.018f + (layerIndex * 0.026f) + (shieldFill * 0.014f);
        float verticalBias = quadrantIndex < 2 ? 0.012f : -0.012f;
        return new Vector3(
            direction.x * layerSpread,
            (direction.y * layerSpread * 0.92f) + verticalBias,
            0f);
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
        Sprite[] sprites = new Sprite[PetalsPerLayer];

        for (int quadrantIndex = 0; quadrantIndex < sprites.Length; quadrantIndex++)
        {
            Texture2D quadrantTexture = CreateQuadrantTexture(source, backgroundMask, quadrantIndex);
            Sprite sprite = Sprite.Create(
                quadrantTexture,
                new Rect(0f, 0f, quadrantTexture.width, quadrantTexture.height),
                QuadrantPivots[quadrantIndex],
                Mathf.Max(quadrantTexture.width, quadrantTexture.height));
            sprite.name = $"DeVect_IceShieldQuadrant_{quadrantIndex}";
            sprites[quadrantIndex] = sprite;
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
        Queue<int> queue = new();

        void EnqueueIfBackground(int x, int y)
        {
            int index = (y * width) + x;
            if (mask[index] || !IsBackgroundCandidate(pixels[index]))
            {
                return;
            }

            mask[index] = true;
            queue.Enqueue(index);
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

        while (queue.Count > 0)
        {
            int index = queue.Dequeue();
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

    private static Texture2D CreateQuadrantTexture(Texture2D source, bool[] backgroundMask, int quadrantIndex)
    {
        int fullWidth = source.width;
        int fullHeight = source.height;
        int halfWidth = fullWidth / 2;
        int halfHeight = fullHeight / 2;
        int sourceX = (quadrantIndex % 2) * halfWidth;
        int sourceY = quadrantIndex < 2 ? halfHeight : 0;
        Color32[] sourcePixels = source.GetPixels32();
        Color[] remappedPixels = new Color[halfWidth * halfHeight];

        for (int y = 0; y < halfHeight; y++)
        {
            for (int x = 0; x < halfWidth; x++)
            {
                int sourcePixelX = sourceX + x;
                int sourcePixelY = sourceY + y;
                int sourceIndex = (sourcePixelY * fullWidth) + sourcePixelX;
                int targetIndex = (y * halfWidth) + x;
                if (backgroundMask[sourceIndex])
                {
                    remappedPixels[targetIndex] = Color.clear;
                    continue;
                }

                Color sourceColor = sourcePixels[sourceIndex];
                float edgeFactor = GetForegroundEdgeFactor(backgroundMask, sourcePixelX, sourcePixelY, fullWidth, fullHeight);
                Color remappedColor = RemapToIcePalette(sourceColor, edgeFactor);
                float softEdgeFade = Mathf.Clamp01(1f - (Mathf.InverseLerp(0.88f, 1f, GetLuminance(sourceColor)) * edgeFactor * 0.55f));
                remappedColor.a = sourceColor.a * softEdgeFade;
                remappedPixels[targetIndex] = remappedColor;
            }
        }

        Texture2D texture = CreateTexture(halfWidth, halfHeight, $"DeVect_IceShieldQuadrantTexture_{quadrantIndex}");
        texture.SetPixels(remappedPixels);
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

    private static Color RemapToIcePalette(Color sourceColor, float edgeFactor)
    {
        float luminance = GetLuminance(sourceColor);
        float maxChannel = Mathf.Max(sourceColor.r, Mathf.Max(sourceColor.g, sourceColor.b));
        float minChannel = Mathf.Min(sourceColor.r, Mathf.Min(sourceColor.g, sourceColor.b));
        float contrast = maxChannel - minChannel;

        Color deepIce = new(0.12f, 0.3f, 0.66f, 1f);
        Color midIce = new(0.34f, 0.72f, 0.98f, 1f);
        Color frost = new(0.82f, 0.97f, 1f, 1f);
        Color brightFrost = new(0.98f, 1f, 1f, 1f);

        Color baseColor = luminance >= 0.72f
            ? Color.Lerp(frost, brightFrost, Mathf.Pow(Mathf.InverseLerp(0.72f, 1f, luminance), 0.7f))
            : Color.Lerp(deepIce, midIce, Mathf.Pow(Mathf.Clamp01(luminance + (contrast * 0.12f)), 0.82f));

        float bloom = Mathf.Clamp01((edgeFactor * 0.68f) + Mathf.Max(0f, luminance - 0.82f) * 0.55f);
        return Color.Lerp(baseColor, brightFrost, bloom * 0.42f);
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

    private static Sprite CreateMistSprite()
    {
        if (_mistSprite != null)
        {
            return _mistSprite;
        }

        const int width = 144;
        const int height = 110;
        Texture2D texture = CreateTexture(width, height, "DeVect_IceShieldMist");
        Vector2 center = new(width * 0.5f, height * 0.52f);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float nx = (x - center.x) / (width * 0.46f);
                float ny = (y - center.y) / (height * 0.36f);
                float distance = Mathf.Sqrt((nx * nx) + (ny * ny * 1.1f));
                float body = Mathf.Clamp01(1f - distance);
                float ring = Mathf.Clamp01(1f - Mathf.Abs(distance - 0.62f) * 2.4f);
                float upperWisp = Mathf.Clamp01(1f - Vector2.Distance(new Vector2(nx, ny), new Vector2(0f, 0.18f)) * 1.85f);
                float sideWisp = Mathf.Clamp01(1f - Vector2.Distance(new Vector2(Mathf.Abs(nx), ny), new Vector2(0.54f, 0.02f)) * 1.9f);
                float noise =
                    0.58f +
                    (0.18f * Mathf.Sin((x * 0.14f) + (y * 0.09f))) +
                    (0.16f * Mathf.Cos((x * 0.08f) - (y * 0.17f))) +
                    (0.08f * Mathf.Sin((x * 0.29f) - (y * 0.06f)));
                float alpha = Mathf.Clamp01(((body * 0.16f) + (ring * 0.13f) + (upperWisp * 0.12f) + (sideWisp * 0.09f)) * noise);
                Color color = Color.Lerp(new Color(0.46f, 0.78f, 1f, alpha), new Color(0.9f, 0.98f, 1f, alpha), Mathf.Clamp01(body + (upperWisp * 0.4f)));
                texture.SetPixel(x, y, new Color(color.r, color.g, color.b, alpha));
            }
        }

        texture.Apply();
        _mistSprite = Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.48f), width);
        _mistSprite.name = "DeVect_IceShieldMistSprite";
        return _mistSprite;
    }

    private static Sprite CreateCoreSprite()
    {
        if (_coreSprite != null)
        {
            return _coreSprite;
        }

        const int size = 68;
        Texture2D texture = CreateTexture(size, size, "DeVect_IceShieldCore");
        Vector2 center = new(size * 0.5f, size * 0.5f);
        float radius = size * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center) / radius;
                if (distance > 1f)
                {
                    texture.SetPixel(x, y, Color.clear);
                    continue;
                }

                float alpha = Mathf.Pow(1f - distance, 2.4f) * 0.42f;
                Color color = Color.Lerp(new Color(0.34f, 0.74f, 1f, alpha), new Color(0.98f, 1f, 1f, alpha), Mathf.Pow(1f - distance, 3.2f));
                texture.SetPixel(x, y, new Color(color.r, color.g, color.b, alpha));
            }
        }

        texture.Apply();
        _coreSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        _coreSprite.name = "DeVect_IceShieldCoreSprite";
        return _coreSprite;
    }

    private static Sprite CreateAccentSprite()
    {
        if (_accentSprite != null)
        {
            return _accentSprite;
        }

        const int size = 76;
        Texture2D texture = CreateTexture(size, size, "DeVect_IceShieldAccent");
        Vector2 center = new(size * 0.5f, size * 0.5f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Abs(x - center.x) / (size * 0.16f);
                float dy = Mathf.Abs(y - center.y) / (size * 0.16f);
                float vertical = Mathf.Clamp01(1f - dx - (dy * 0.2f));
                float horizontal = Mathf.Clamp01(1f - dy - (dx * 0.2f));
                float diamond = Mathf.Clamp01(1f - ((Mathf.Abs(x - center.x) + Mathf.Abs(y - center.y)) / (size * 0.38f)));
                float alpha = Mathf.Max(vertical * 0.1f, Mathf.Max(horizontal * 0.1f, diamond * 0.08f));
                texture.SetPixel(x, y, new Color(0.94f, 0.99f, 1f, alpha));
            }
        }

        texture.Apply();
        _accentSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        _accentSprite.name = "DeVect_IceShieldAccentSprite";
        return _accentSprite;
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
