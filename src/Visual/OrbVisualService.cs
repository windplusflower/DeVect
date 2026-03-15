using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace DeVect.Visual;

internal sealed class OrbVisualService
{
    private const float DashedRingRadius = 0.225f;
    private const float DashScale = 0.086f;
    private const float DashThicknessFactor = 0.32f;
    private const float LightningScale = 0.325f;
    private const float LightningLifetime = 0.45f;
    private const int DashCount = 14;
    private static readonly Color DashColor = new(1f, 1f, 1f, 0.55f);

    private readonly List<TransientVisual> _transientVisuals = new();
    private static Sprite? _pixelSprite;
    private static Sprite? _circleSprite;
    private static Sprite? _electricAuraSprite;
    private static Sprite? _highlightSprite;
    private static Texture2D? _lightningTexture;
    private static Sprite? _lightningSprite;

    public void BuildDashedRing(Transform parent)
    {
        for (int i = 0; i < DashCount; i++)
        {
            float angle = (Mathf.PI * 2f * i) / DashCount;
            Vector3 localPosition = new(Mathf.Cos(angle) * DashedRingRadius, Mathf.Sin(angle) * DashedRingRadius, 0f);

            GameObject dash = new($"Dash_{i}");
            dash.transform.SetParent(parent, false);
            dash.transform.localPosition = localPosition;
            dash.transform.localScale = new Vector3(DashScale, DashScale * DashThicknessFactor, 1f);
            dash.transform.localRotation = Quaternion.Euler(0f, 0f, (angle * Mathf.Rad2Deg) + 90f);

            SpriteRenderer renderer = dash.AddComponent<SpriteRenderer>();
            renderer.sprite = CreatePixelSprite();
            renderer.color = DashColor;
            renderer.sortingLayerName = "HUD";
            renderer.sortingOrder = 9;
        }
    }

    public SpriteRenderer CreateOrbRenderer(string name, Color color)
    {
        GameObject orb = new(name);
        SpriteRenderer renderer = orb.AddComponent<SpriteRenderer>();
        renderer.sprite = CreateCircleSprite();
        renderer.color = color;
        renderer.sortingLayerName = "HUD";
        renderer.sortingOrder = 10;
        AddOrbMaterialLayers(orb.transform, color);
        return renderer;
    }

    public void SpawnLightningVisual(Vector3 worldPosition)
    {
        GameObject lightning = new("DeVect_LightningVisual");
        lightning.transform.position = worldPosition;
        lightning.transform.rotation = Quaternion.identity;
        lightning.transform.localScale = new Vector3(LightningScale, LightningScale, 1f);

        SpriteRenderer renderer = lightning.AddComponent<SpriteRenderer>();
        renderer.sprite = CreateLightningSprite();
        renderer.color = new Color(1f, 0.95f, 0.35f, 1f);
        renderer.sortingLayerName = "HUD";
        renderer.sortingOrder = 12;

        _transientVisuals.Add(new TransientVisual(lightning, renderer, LightningLifetime, Vector3.up * 1.4f, renderer.color, lightning.transform.localScale));
    }

    public void TrackTransientVisual(TransientVisual visual)
    {
        _transientVisuals.Add(visual);
    }

    public void TickTransientVisuals(float deltaTime)
    {
        for (int i = _transientVisuals.Count - 1; i >= 0; i--)
        {
            TransientVisual transient = _transientVisuals[i];
            transient.LifetimeRemaining -= deltaTime;
            if (transient.Root == null || transient.LifetimeRemaining <= 0f)
            {
                if (transient.Root != null)
                {
                    Object.Destroy(transient.Root);
                }

                _transientVisuals.RemoveAt(i);
                continue;
            }

            float alpha = Mathf.Clamp01(transient.LifetimeRemaining / transient.InitialLifetime);
            if (transient.UseArcMotion)
            {
                float progress = 1f - alpha;
                Vector3 position;
                if (transient.ArcRadius > 0f)
                {
                    float delta = Mathf.Repeat((transient.EndAngleDeg - transient.StartAngleDeg) + 180f, 360f) - 180f;
                    float angleDeg = transient.StartAngleDeg + (delta * progress);
                    float radians = angleDeg * Mathf.Deg2Rad;
                    position = transient.ArcCenter + new Vector3(
                        Mathf.Cos(radians) * transient.ArcRadius,
                        Mathf.Sin(radians) * transient.ArcRadius,
                        0f);
                }
                else
                {
                    position = Vector3.Lerp(transient.StartPosition, transient.EndPosition, progress);
                    position.y += 4f * progress * (1f - progress) * transient.ArcHeight;
                }

                transient.Root.transform.position = position;
            }
            else
            {
                transient.Root.transform.position += transient.Velocity * deltaTime;
            }

            transient.Renderer.color = new Color(transient.BaseColor.r, transient.BaseColor.g, transient.BaseColor.b, alpha);
            transient.Root.transform.localScale = transient.BaseScale * (0.85f + (0.15f * alpha));
        }
    }

    public void DisposeTransientVisuals()
    {
        for (int i = _transientVisuals.Count - 1; i >= 0; i--)
        {
            if (_transientVisuals[i].Root != null)
            {
                Object.Destroy(_transientVisuals[i].Root);
            }
        }

        _transientVisuals.Clear();
    }

    private static Sprite CreatePixelSprite()
    {
        if (_pixelSprite != null)
        {
            return _pixelSprite;
        }

        Texture2D texture = new(1, 1, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            name = "DeVect_Pixel"
        };
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();

        _pixelSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        _pixelSprite.name = "DeVect_PixelSprite";
        return _pixelSprite;
    }

    private static Sprite CreateCircleSprite()
    {
        if (_circleSprite != null)
        {
            return _circleSprite;
        }

        const int size = 64;
        Texture2D texture = new(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            name = "DeVect_Circle"
        };

        float radius = (size - 1) * 0.5f;
        Vector2 center = new(radius, radius);
        Vector2 lightDirection = new(-0.55f, 0.82f);
        lightDirection.Normalize();
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 point = new(x, y);
                Vector2 offset = (point - center) / radius;
                float distance = offset.magnitude;
                if (distance > 1f)
                {
                    texture.SetPixel(x, y, Color.clear);
                    continue;
                }

                float edgeFade = Mathf.Clamp01((1f - distance) / 0.08f);
                float sphereMask = Mathf.Clamp01(1f - (distance * distance));
                float directional = Mathf.Clamp01(Vector2.Dot(offset.normalized, lightDirection));
                float brightness = 0.38f + (sphereMask * 0.42f) + (directional * 0.24f);
                float rim = Mathf.Pow(Mathf.Clamp01(1f - distance), 0.45f);
                brightness = Mathf.Clamp01(brightness + (rim * 0.08f));
                texture.SetPixel(x, y, new Color(brightness, brightness, brightness, edgeFade));
            }
        }

        texture.Apply();
        _circleSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        _circleSprite.name = "DeVect_CircleSprite";
        return _circleSprite;
    }

    private static void AddOrbMaterialLayers(Transform parent, Color baseColor)
    {
        CreateLayerRenderer(
            parent,
            "ElectricAura",
            CreateElectricAuraSprite(),
            GetElectricAuraColor(baseColor),
            8,
            Vector3.zero,
            new Vector3(1.48f, 1.48f, 1f));

        CreateLayerRenderer(
            parent,
            "InnerGlow",
            CreateCircleSprite(),
            GetInnerGlowColor(baseColor),
            11,
            new Vector3(-0.015f, 0.012f, 0f),
            new Vector3(0.74f, 0.74f, 1f));

        CreateLayerRenderer(
            parent,
            "Highlight",
            CreateHighlightSprite(),
            GetHighlightColor(baseColor),
            12,
            new Vector3(-0.12f, 0.1f, 0f),
            new Vector3(0.56f, 0.56f, 1f));
    }

    private static SpriteRenderer CreateLayerRenderer(Transform parent, string name, Sprite sprite, Color color, int sortingOrder, Vector3 localPosition, Vector3 localScale)
    {
        GameObject layer = new(name);
        layer.transform.SetParent(parent, false);
        layer.transform.localPosition = localPosition;
        layer.transform.localScale = localScale;

        SpriteRenderer renderer = layer.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = color;
        renderer.sortingLayerName = "HUD";
        renderer.sortingOrder = sortingOrder;
        return renderer;
    }

    private static Color GetElectricAuraColor(Color baseColor)
    {
        Color cyan = new(0.5f, 0.95f, 1f, 1f);
        Color mixed = Color.Lerp(baseColor, cyan, 0.38f);
        mixed.a = 0.5f;
        return mixed;
    }

    private static Color GetInnerGlowColor(Color baseColor)
    {
        Color glow = Color.Lerp(baseColor, Color.white, 0.42f);
        glow.a = 0.28f;
        return glow;
    }

    private static Color GetHighlightColor(Color baseColor)
    {
        Color highlight = Color.Lerp(baseColor, Color.white, 0.78f);
        highlight.a = 0.82f;
        return highlight;
    }

    private static Sprite CreateElectricAuraSprite()
    {
        if (_electricAuraSprite != null)
        {
            return _electricAuraSprite;
        }

        const int size = 64;
        Texture2D texture = new(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            name = "DeVect_ElectricAura"
        };

        float radius = (size - 1) * 0.5f;
        Vector2 center = new(radius, radius);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 point = new(x, y);
                Vector2 offset = (point - center) / radius;
                float distance = offset.magnitude;
                if (distance > 1f)
                {
                    texture.SetPixel(x, y, Color.clear);
                    continue;
                }

                float angle = Mathf.Atan2(offset.y, offset.x);
                float jaggedEdge = 0.67f
                    + (0.08f * Mathf.Sin((angle * 6f) + 0.35f))
                    + (0.04f * Mathf.Sin((angle * 11f) - 0.6f));
                float shell = 1f - Mathf.Clamp01(Mathf.Abs(distance - jaggedEdge) / 0.16f);
                float innerFade = Mathf.Clamp01((distance - 0.38f) / 0.4f);
                float alpha = shell * innerFade;
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply();
        _electricAuraSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        _electricAuraSprite.name = "DeVect_ElectricAuraSprite";
        return _electricAuraSprite;
    }

    private static Sprite CreateHighlightSprite()
    {
        if (_highlightSprite != null)
        {
            return _highlightSprite;
        }

        const int size = 64;
        Texture2D texture = new(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            name = "DeVect_Highlight"
        };

        Vector2 center = new(size * 0.34f, size * 0.68f);
        float radiusX = size * 0.24f;
        float radiusY = size * 0.18f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float normalizedX = (x - center.x) / radiusX;
                float normalizedY = (y - center.y) / radiusY;
                float distance = Mathf.Sqrt((normalizedX * normalizedX) + (normalizedY * normalizedY));
                float alpha = distance >= 1f ? 0f : Mathf.Pow(1f - distance, 1.7f);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply();
        _highlightSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        _highlightSprite.name = "DeVect_HighlightSprite";
        return _highlightSprite;
    }

    private static Sprite CreateLightningSprite()
    {
        if (_lightningSprite != null)
        {
            return _lightningSprite;
        }

        Texture2D texture = LoadLightningTexture();
        _lightningSprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0f), 32f);
        _lightningSprite.name = "DeVect_LightningSprite";
        return _lightningSprite;
    }

    private static string GetLightningAssetPath()
    {
        string assemblyDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
            ?? throw new IOException("Unable to resolve mod assembly directory.");
        return Path.Combine(assemblyDirectory, "assets", "闪电.png");
    }

    private static Texture2D LoadLightningTexture()
    {
        if (_lightningTexture != null)
        {
            return _lightningTexture;
        }

        string assetPath = GetLightningAssetPath();
        if (!File.Exists(assetPath))
        {
            throw new FileNotFoundException($"Lightning icon asset not found: {assetPath}", assetPath);
        }

        byte[] imageBytes = File.ReadAllBytes(assetPath);
        Texture2D texture = new(2, 2, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            name = "DeVect_Lightning"
        };

        if (!ImageConversion.LoadImage(texture, imageBytes, false))
        {
            Object.Destroy(texture);
            throw new IOException($"Failed to decode lightning icon asset: {assetPath}");
        }

        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;
        _lightningTexture = texture;
        return _lightningTexture;
    }
}
