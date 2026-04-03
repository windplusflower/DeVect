using DeVect.Combat;
using UnityEngine;

namespace DeVect.Visual;

internal sealed class IceShieldDisplay
{
    private const int DefaultHudLayer = 27;
    private const string DefaultHudSortingLayerName = "HUD";
    private const int DefaultHudSortingOrder = 0;
    private const float HealthHudStartViewportX = 0.124f;
    private const float HealthHudUnitViewportSpacing = 0.0295f;
    private const float HudViewportY = 0.92f;
    private const float MaskAnchorWorldOffsetX = 0.22f;
    private const float MaskAnchorWorldOffsetY = -0.01f;
    private const int PetalsPerLayer = IceShieldState.PetalsPerShield;
    private const int LayerCount = IceShieldState.MaxShieldLayers;
    private const float BaseScale = 0.2f;
    private const float LayerScaleStep = 0.18f;
    private const float PetalRadius = 0.32f;
    private const float PetalRadiusStep = 0.08f;
    private const float CoreBaseScale = 0.48f;
    private const float MistBaseScale = 1.36f;
    private const float LayerBobAmplitude = 0.01f;
    private const float LayerSwayAmplitude = 0.008f;
    private const float JitterAmplitude = 0.02f;
    private const float JitterFrequency = 2.4f;
    private const string SortingLayer = "HUD";
    private const int CoreSortingOrder = 5;
    private const int LayerSortingBase = 6;
    private const int MistSortingOrder = 4;

    private static readonly string[] HealthNameKeywords = { "health", "mask", "blue", "joni", "lifeblood", "hp" };
    private static readonly Color CoreColor = new(0.72f, 0.94f, 1f, 0.5f);
    private static readonly Color MistColor = new(0.5f, 0.83f, 1f, 0.16f);
    private static readonly Color ActivePetalColor = new(0.67f, 0.9f, 1f, 0.42f);
    private static readonly Color InactivePetalColor = new(0.48f, 0.7f, 0.88f, 0.1f);
    private static readonly Color AccentColor = new(0.9f, 0.98f, 1f, 0.22f);

    private static Sprite? _petalSprite;
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
            Object.Destroy(_root);
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

        GameObject mist = CreateRendererObject("Mist", _rootTransform, CreateMistSprite(), MistColor, MistSortingOrder, new Vector3(MistBaseScale, MistBaseScale * 0.88f, 1f), out SpriteRenderer mistRenderer);
        mist.transform.localPosition = new Vector3(0f, 0.02f, 0f);
        _mistRenderer = mistRenderer;

        GameObject accent = CreateRendererObject("Accent", _rootTransform, CreateAccentSprite(), AccentColor, CoreSortingOrder + 1, new Vector3(0.8f, 1.06f, 1f), out SpriteRenderer accentRenderer);
        accent.transform.localPosition = new Vector3(0f, 0.04f, 0f);
        _accentRenderer = accentRenderer;

        GameObject core = CreateRendererObject("Core", _rootTransform, CreateCoreSprite(), CoreColor, CoreSortingOrder, new Vector3(CoreBaseScale, CoreBaseScale, 1f), out SpriteRenderer coreRenderer);
        _coreRenderer = coreRenderer;

        for (int layerIndex = 0; layerIndex < LayerCount; layerIndex++)
        {
            for (int petalIndex = 0; petalIndex < PetalsPerLayer; petalIndex++)
            {
                int index = (layerIndex * PetalsPerLayer) + petalIndex;
                float baseAngle = 45f + (petalIndex * (360f / PetalsPerLayer));
                GameObject petal = CreateRendererObject(
                    $"Petal_{layerIndex}_{petalIndex}",
                    _rootTransform,
                    CreatePetalSprite(),
                    InactivePetalColor,
                    LayerSortingBase + layerIndex,
                    GetPetalBaseScale(layerIndex),
                    out SpriteRenderer petalRenderer
                );

                Transform petalTransform = petal.transform;
                petalTransform.localRotation = Quaternion.Euler(0f, 0f, baseAngle);
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

        float pulse = 1f + (Mathf.Sin((_time * 1.7f) + 0.6f) * 0.08f);
        _coreRenderer.transform.localScale = new Vector3(CoreBaseScale, CoreBaseScale, 1f) * pulse;
        _coreRenderer.color = Color.Lerp(
            new Color(0.56f, 0.84f, 1f, 0.26f),
            new Color(0.78f, 0.96f, 1f, 0.54f),
            shieldFill);
    }

    private void UpdateMist(float shieldFill)
    {
        if (_mistRenderer == null)
        {
            return;
        }

        float swirl = Mathf.Sin((_time * 0.95f) + 1.2f);
        _mistRenderer.transform.localScale = new Vector3(
            MistBaseScale + (shieldFill * 0.28f) + (swirl * 0.06f),
            (MistBaseScale * 0.88f) + (shieldFill * 0.18f) - (swirl * 0.04f),
            1f);
        _mistRenderer.transform.localPosition = new Vector3(swirl * 0.02f, 0.01f + (shieldFill * 0.02f), 0f);
        _mistRenderer.color = new Color(0.44f, 0.8f, 1f, 0.1f + (shieldFill * 0.08f));
    }

    private void UpdateAccent(float shieldFill)
    {
        if (_accentRenderer == null)
        {
            return;
        }

        float shimmer = Mathf.Sin((_time * 2.15f) + 2.1f);
        _accentRenderer.transform.localScale = new Vector3(0.76f + (shieldFill * 0.14f), 1f + (shieldFill * 0.18f), 1f);
        _accentRenderer.transform.localRotation = Quaternion.Euler(0f, 0f, shimmer * 7f);
        _accentRenderer.color = new Color(0.92f, 0.99f, 1f, 0.08f + (shieldFill * 0.12f));
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
            int directionIndex = i % PetalsPerLayer;
            bool active = i < petalCount;
            float layerFactor = 1f + (layerIndex * 0.08f);
            float baseAngle = 45f + (directionIndex * (360f / PetalsPerLayer));
            float angleOffset = Mathf.Sin((_time * (1.2f + (layerIndex * 0.17f))) + (directionIndex * 0.9f)) * 6f;
            float radius = PetalRadius + (layerIndex * PetalRadiusStep) + (Mathf.Sin((_time * 1.4f) + i) * 0.012f);
            Vector3 outward = Quaternion.Euler(0f, 0f, baseAngle) * Vector3.up;
            Vector3 layerOffset = outward * radius;
            layerOffset.y *= 0.92f;
            layerOffset += new Vector3(
                Mathf.Sin((_time * JitterFrequency) + i) * JitterAmplitude,
                Mathf.Cos((_time * (JitterFrequency - 0.35f)) + i) * (JitterAmplitude * 0.65f),
                0f);

            transform.localPosition = layerOffset;
            transform.localRotation = Quaternion.Euler(0f, 0f, baseAngle + angleOffset);
            transform.localScale = GetPetalBaseScale(layerIndex) * (1f + (shieldFill * 0.12f));
            renderer.enabled = active || layerIndex == 0;

            Color targetColor = active
                ? Color.Lerp(ActivePetalColor, new Color(0.84f, 0.97f, 1f, 0.5f), shieldFill * 0.8f)
                : InactivePetalColor;
            float alphaWave = active ? 0.92f + (Mathf.Sin((_time * 2.3f) + i) * 0.08f) : 1f;
            renderer.color = new Color(
                targetColor.r,
                targetColor.g,
                targetColor.b,
                targetColor.a * (1f - (layerIndex * 0.08f)) * alphaWave * layerFactor);
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
        if (TryGetMaskAnchorWorldPosition(hudCamera, out Vector3 anchorWorldPosition))
        {
            return anchorWorldPosition;
        }

        PlayerData? playerData = PlayerData.instance;
        int maxHealth = Mathf.Max(5, playerData?.maxHealth ?? 5);
        int blueHealth = Mathf.Max(0, playerData?.healthBlue ?? 0);
        float viewportX = HealthHudStartViewportX + ((maxHealth + blueHealth) * HealthHudUnitViewportSpacing);
        float worldDistance = Mathf.Abs(hudCamera.transform.position.z);
        Vector3 worldPosition = hudCamera.ViewportToWorldPoint(new Vector3(viewportX, HudViewportY, worldDistance));
        worldPosition.z = 0f;
        return worldPosition;
    }

    private static bool TryGetMaskAnchorWorldPosition(Camera hudCamera, out Vector3 worldPosition)
    {
        worldPosition = default;

        Transform hudRoot = hudCamera.transform.parent != null ? hudCamera.transform.parent : hudCamera.transform;
        if (!TryGetRightmostHudRenderer(hudCamera, hudRoot, requireHealthKeyword: true, out Renderer? maskRenderer) &&
            !TryGetRightmostHudRenderer(hudCamera, hudRoot, requireHealthKeyword: false, out maskRenderer))
        {
            return false;
        }

        if (maskRenderer == null)
        {
            return false;
        }

        Bounds maskBounds = maskRenderer.bounds;
        worldPosition = new Vector3(
            maskBounds.max.x + MaskAnchorWorldOffsetX,
            maskBounds.center.y + MaskAnchorWorldOffsetY,
            0f);
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

    private void DestroyDuplicateDisplays()
    {
        GameObject[] displays = Object.FindObjectsOfType<GameObject>();
        for (int i = 0; i < displays.Length; i++)
        {
            GameObject display = displays[i];
            if (display == null || display == _root || display.name != "DeVect_IceShieldDisplay")
            {
                continue;
            }

            Object.Destroy(display);
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
        float scale = 0.7f + (layerIndex * LayerScaleStep);
        return new Vector3(scale, scale * 1.18f, 1f);
    }

    private static Sprite CreatePetalSprite()
    {
        if (_petalSprite != null)
        {
            return _petalSprite;
        }

        const int width = 56;
        const int height = 80;
        Texture2D texture = CreateTexture(width, height, "DeVect_IceShieldLotusPetal");
        Vector2 center = new(width * 0.5f, height * 0.42f);
        float maxRadiusX = width * 0.26f;
        float maxRadiusY = height * 0.42f;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float normalizedX = (x - center.x) / maxRadiusX;
                float normalizedY = (y - center.y) / maxRadiusY;
                float ellipse = (normalizedX * normalizedX) + (normalizedY * normalizedY);
                float taper = Mathf.Clamp01(1f - Mathf.Abs(normalizedX) * 0.68f);
                float sharpenedTip = Mathf.Clamp01((y / (float)height) * 1.2f);
                if (ellipse > 1f || y < 4)
                {
                    texture.SetPixel(x, y, Color.clear);
                    continue;
                }

                float body = Mathf.Pow(1f - ellipse, 1.3f) * taper;
                float ridge = Mathf.Clamp01(1f - Mathf.Abs(normalizedX) * 2.1f) * sharpenedTip;
                float alpha = Mathf.Clamp01((body * 0.9f) + (ridge * 0.24f));
                Color color = Color.Lerp(new Color(0.34f, 0.7f, 0.95f, alpha), new Color(0.88f, 0.98f, 1f, alpha), ridge * 0.75f);
                texture.SetPixel(x, y, new Color(color.r, color.g, color.b, alpha));
            }
        }

        texture.Apply();
        _petalSprite = Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.12f), width);
        _petalSprite.name = "DeVect_IceShieldLotusPetalSprite";
        return _petalSprite;
    }

    private static Sprite CreateMistSprite()
    {
        if (_mistSprite != null)
        {
            return _mistSprite;
        }

        const int width = 96;
        const int height = 72;
        Texture2D texture = CreateTexture(width, height, "DeVect_IceShieldMist");
        Vector2 center = new(width * 0.5f, height * 0.52f);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float nx = (x - center.x) / (width * 0.42f);
                float ny = (y - center.y) / (height * 0.3f);
                float distance = Mathf.Sqrt((nx * nx) + (ny * ny));
                float cloud = Mathf.Clamp01(1f - distance);
                float noise = 0.65f + (0.2f * Mathf.Sin((x * 0.22f) + (y * 0.18f))) + (0.15f * Mathf.Cos((x * 0.11f) - (y * 0.2f)));
                float alpha = Mathf.Clamp01(cloud * noise * 0.34f);
                texture.SetPixel(x, y, new Color(0.64f, 0.9f, 1f, alpha));
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

        const int size = 56;
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

                float alpha = Mathf.Pow(1f - distance, 1.8f) * 0.72f;
                Color color = Color.Lerp(new Color(0.26f, 0.66f, 1f, alpha), new Color(0.94f, 0.99f, 1f, alpha), Mathf.Pow(1f - distance, 2.8f));
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

        const int width = 48;
        const int height = 72;
        Texture2D texture = CreateTexture(width, height, "DeVect_IceShieldAccent");
        Vector2 center = new(width * 0.5f, height * 0.5f);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float nx = Mathf.Abs((x - center.x) / (width * 0.22f));
                float ny = Mathf.Abs((y - center.y) / (height * 0.42f));
                float line = Mathf.Clamp01(1f - nx - (ny * 0.38f));
                float side = Mathf.Clamp01(1f - Mathf.Abs(nx - (0.48f - (ny * 0.28f))) * 3.8f);
                float alpha = Mathf.Max(line * 0.16f, side * 0.1f);
                texture.SetPixel(x, y, new Color(0.88f, 0.98f, 1f, alpha));
            }
        }

        texture.Apply();
        _accentSprite = Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.36f), width);
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
