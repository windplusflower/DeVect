using UnityEngine;

namespace DeVect.Visual;

internal sealed class IceShieldDisplay
{
    private const int HudLayer = 27;
    private const float HudViewportX = 0.31f;
    private const float HudViewportY = 0.92f;
    private static readonly Color ActivePetalColor = new(0.78f, 0.94f, 1f, 0.98f);
    private static readonly Color InactivePetalColor = new(0.42f, 0.56f, 0.68f, 0.24f);
    private static readonly Vector3[] PetalOffsets =
    {
        new Vector3(0f, 0.18f, 0f),
        new Vector3(0.18f, 0f, 0f),
        new Vector3(0f, -0.18f, 0f),
        new Vector3(-0.18f, 0f, 0f)
    };

    private static Sprite? _petalSprite;
    private static Sprite? _coreSprite;

    private GameObject? _root;
    private SpriteRenderer? _coreRenderer;
    private SpriteRenderer[] _petalRenderers = new SpriteRenderer[4];

    public void Tick(int petalCount)
    {
        Camera? hudCamera = GameCameras.instance != null ? GameCameras.instance.hudCamera : null;
        if (hudCamera == null || !hudCamera.gameObject.activeInHierarchy)
        {
            SetVisible(false);
            return;
        }

        EnsureBuilt();
        if (_root == null || _coreRenderer == null)
        {
            return;
        }

        float worldDistance = Mathf.Abs(0f - hudCamera.transform.position.z);
        Vector3 worldPosition = hudCamera.ViewportToWorldPoint(new Vector3(HudViewportX, HudViewportY, worldDistance));
        worldPosition.z = 0f;
        _root.transform.position = worldPosition;
        _root.transform.rotation = Quaternion.identity;
        _root.transform.localScale = Vector3.one;

        bool hasShield = petalCount > 0;
        SetVisible(hasShield);
        if (!hasShield)
        {
            return;
        }

        _coreRenderer.color = new Color(0.84f, 0.96f, 1f, 0.3f + (0.1f * petalCount));
        for (int i = 0; i < _petalRenderers.Length; i++)
        {
            SpriteRenderer renderer = _petalRenderers[i];
            renderer.color = i < petalCount ? ActivePetalColor : InactivePetalColor;
        }
    }

    public void Dispose()
    {
        if (_root != null)
        {
            Object.Destroy(_root);
        }

        _root = null;
        _coreRenderer = null;
        _petalRenderers = new SpriteRenderer[4];
    }

    private void EnsureBuilt()
    {
        if (_root != null)
        {
            return;
        }

        _root = new GameObject("DeVect_IceShieldDisplay");
        ApplyHudLayer(_root);

        GameObject core = new("Core");
        core.transform.SetParent(_root.transform, false);
        ApplyHudLayer(core);
        _coreRenderer = core.AddComponent<SpriteRenderer>();
        _coreRenderer.sprite = CreateCoreSprite();
        _coreRenderer.sortingLayerName = "HUD";
        _coreRenderer.sortingOrder = 18;
        _coreRenderer.transform.localScale = new Vector3(0.16f, 0.16f, 1f);

        for (int i = 0; i < _petalRenderers.Length; i++)
        {
            GameObject petal = new($"Petal_{i}");
            petal.transform.SetParent(_root.transform, false);
            ApplyHudLayer(petal);
            petal.transform.localPosition = PetalOffsets[i];
            petal.transform.localScale = new Vector3(0.22f, 0.22f, 1f);
            petal.transform.localRotation = Quaternion.Euler(0f, 0f, i * -90f);

            SpriteRenderer renderer = petal.AddComponent<SpriteRenderer>();
            renderer.sprite = CreatePetalSprite();
            renderer.sortingLayerName = "HUD";
            renderer.sortingOrder = 19;
            _petalRenderers[i] = renderer;
        }
    }

    private static void ApplyHudLayer(GameObject root)
    {
        root.layer = HudLayer;
    }

    private void SetVisible(bool visible)
    {
        if (_root != null && _root.activeSelf != visible)
        {
            _root.SetActive(visible);
        }
    }

    private static Sprite CreatePetalSprite()
    {
        if (_petalSprite != null)
        {
            return _petalSprite;
        }

        const int size = 48;
        Texture2D texture = new(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            name = "DeVect_IceShieldPetal"
        };

        Vector2 center = new(size * 0.5f, size * 0.38f);
        float radiusX = size * 0.2f;
        float radiusY = size * 0.28f;
        Vector2 tip = new(size * 0.5f, size * 0.92f);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float ellipseX = (x - center.x) / radiusX;
                float ellipseY = (y - center.y) / radiusY;
                float bodyMask = Mathf.Clamp01(1f - ((ellipseX * ellipseX) + (ellipseY * ellipseY)));
                float veinMask = Mathf.Clamp01(1f - (Mathf.Abs(x - center.x) / (size * 0.08f))) * Mathf.Clamp01((y - center.y) / (size * 0.36f));
                float tipDistance = Vector2.Distance(new Vector2(x, y), tip) / (size * 0.22f);
                float tipMask = Mathf.Clamp01(1f - tipDistance);
                float alpha = Mathf.Clamp01((bodyMask * 0.92f) + (tipMask * 0.58f));
                if (alpha <= 0f)
                {
                    texture.SetPixel(x, y, Color.clear);
                    continue;
                }

                float frost = Mathf.Clamp01(bodyMask * 0.75f + veinMask * 0.45f + tipMask * 0.3f);
                texture.SetPixel(x, y, new Color(0.85f + (frost * 0.15f), 0.94f + (frost * 0.06f), 1f, alpha));
            }
        }

        texture.Apply();
        _petalSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.18f), size);
        _petalSprite.name = "DeVect_IceShieldPetalSprite";
        return _petalSprite;
    }

    private static Sprite CreateCoreSprite()
    {
        if (_coreSprite != null)
        {
            return _coreSprite;
        }

        const int size = 32;
        Texture2D texture = new(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            name = "DeVect_IceShieldCore"
        };

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

                float alpha = Mathf.Pow(1f - distance, 1.25f);
                texture.SetPixel(x, y, new Color(0.92f, 0.98f, 1f, alpha));
            }
        }

        texture.Apply();
        _coreSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        _coreSprite.name = "DeVect_IceShieldCoreSprite";
        return _coreSprite;
    }
}
