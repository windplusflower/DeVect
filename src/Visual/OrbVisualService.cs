using System.Collections.Generic;
using DeVect.Orbs.Definitions;
using UnityEngine;

namespace DeVect.Visual;

internal sealed class OrbVisualService
{
    private const float DashedRingRadius = 0.225f;
    private const float DashScale = 0.086f;
    private const float DashThicknessFactor = 0.32f;
    private const float LightningScale = 0.42f;
    private const float LightningLifetime = 0.46f;
    private const float VoidImpactBloomLifetime = 0.24f;
    private const float VoidImpactRiftLifetime = 0.42f;
    private const float VoidImpactWispLifetime = 0.34f;
    private const float GlassShardLifetime = 0.46f;
    private const float RefractionRingLifetime = 0.38f;
    private const float GlassFlashLifetime = 0.24f;
    private const int DashCount = 14;
    private static readonly Color DashColor = new(1f, 1f, 1f, 0.55f);

    private readonly List<TransientVisual> _transientVisuals = new();
    private static Sprite? _pixelSprite;
    private static Sprite? _circleSprite;
    private static Sprite? _glassOrbSprite;
    private static Sprite? _electricAuraSprite;
    private static Sprite? _highlightSprite;
    private static Sprite? _glassShardSprite;
    private static Sprite? _refractionRingSprite;
    private static Sprite? _glassFlashSprite;
    private static Texture2D? _lightningTexture;
    private static Sprite? _lightningSprite;
    private static Sprite? _voidOrbSprite;
    private static Sprite? _voidRippleSprite;
    private static Sprite? _voidDropletSprite;

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

    public SpriteRenderer CreateOrbRenderer(string name, OrbTypeId typeId, Color color)
    {
        GameObject orb = new(name);
        SpriteRenderer renderer = orb.AddComponent<SpriteRenderer>();
        renderer.sprite = typeId switch
        {
            OrbTypeId.White => CreateGlassOrbSprite(),
            OrbTypeId.Black => CreateVoidOrbSprite(),
            _ => CreateCircleSprite()
        };
        renderer.color = color;
        renderer.sortingLayerName = "HUD";
        renderer.sortingOrder = 10;
        if (typeId == OrbTypeId.White)
        {
            AddWhiteOrbMaterialLayers(orb.transform, color);
        }
        else if (typeId == OrbTypeId.Black)
        {
            AddBlackOrbMaterialLayers(orb.transform, color);
        }
        else
        {
            AddOrbMaterialLayers(orb.transform, color);
        }

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
        renderer.color = Color.white;
        renderer.sortingLayerName = "HUD";
        renderer.sortingOrder = 12;

        _transientVisuals.Add(new TransientVisual(lightning, renderer, LightningLifetime, Vector3.up * 1.65f, renderer.color, lightning.transform.localScale));
    }

    public void SpawnGlassShatterVisual(Vector3 worldPosition)
    {
        GameObject flash = new("DeVect_GlassImpactFlash");
        flash.transform.position = worldPosition;
        flash.transform.rotation = Quaternion.identity;
        flash.transform.localScale = new Vector3(0.68f, 0.68f, 1f);

        SpriteRenderer flashRenderer = flash.AddComponent<SpriteRenderer>();
        flashRenderer.sprite = CreateGlassFlashSprite();
        flashRenderer.color = new Color(1f, 1f, 1f, 1f);
        flashRenderer.sortingLayerName = "HUD";
        flashRenderer.sortingOrder = 14;
        _transientVisuals.Add(new TransientVisual(flash, flashRenderer, GlassFlashLifetime, Vector3.zero, flashRenderer.color, new Vector3(1.16f, 1.16f, 1f)));

        GameObject ring = new("DeVect_GlassRefractionRing");
        ring.transform.position = worldPosition;
        ring.transform.rotation = Quaternion.identity;
        ring.transform.localScale = new Vector3(0.44f, 0.44f, 1f);

        SpriteRenderer ringRenderer = ring.AddComponent<SpriteRenderer>();
        ringRenderer.sprite = CreateRefractionRingSprite();
        ringRenderer.color = new Color(0.84f, 0.98f, 1f, 0.98f);
        ringRenderer.sortingLayerName = "HUD";
        ringRenderer.sortingOrder = 13;
        _transientVisuals.Add(new TransientVisual(ring, ringRenderer, RefractionRingLifetime, Vector3.zero, ringRenderer.color, new Vector3(1.1f, 1.1f, 1f)));

        Vector3[] velocities =
        {
            new Vector3(-4.2f, 2.9f, 0f),
            new Vector3(-3f, 3.7f, 0f),
            new Vector3(-1.35f, 3.3f, 0f),
            new Vector3(1.45f, 3.45f, 0f),
            new Vector3(3.2f, 2.95f, 0f),
            new Vector3(4.25f, 2f, 0f),
            new Vector3(-3.2f, 1.1f, 0f),
            new Vector3(3.3f, 1.2f, 0f),
            new Vector3(-1.95f, -0.3f, 0f),
            new Vector3(2.05f, -0.22f, 0f)
        };

        for (int i = 0; i < velocities.Length; i++)
        {
            GameObject shard = new($"DeVect_GlassShard_{i}");
            shard.transform.position = worldPosition;
            shard.transform.rotation = Quaternion.Euler(0f, 0f, i * 37f);
            shard.transform.localScale = new Vector3(0.19f, 0.19f, 1f);

            SpriteRenderer shardRenderer = shard.AddComponent<SpriteRenderer>();
            shardRenderer.sprite = CreateGlassShardSprite();
            shardRenderer.color = i % 2 == 0
                ? new Color(1f, 1f, 1f, 1f)
                : new Color(0.8f, 0.96f, 1f, 0.98f);
            shardRenderer.sortingLayerName = "HUD";
            shardRenderer.sortingOrder = 15;
            _transientVisuals.Add(new TransientVisual(shard, shardRenderer, GlassShardLifetime, velocities[i], shardRenderer.color, shard.transform.localScale));
        }
    }

    public void SpawnVoidImpactVisual(Vector3 worldPosition)
    {
        GameObject bloom = new("DeVect_VoidImpactBloom");
        bloom.transform.position = worldPosition;
        bloom.transform.rotation = Quaternion.identity;
        bloom.transform.localScale = new Vector3(0.34f, 0.34f, 1f);

        SpriteRenderer bloomRenderer = bloom.AddComponent<SpriteRenderer>();
        bloomRenderer.sprite = CreateVoidRippleSprite();
        bloomRenderer.color = new Color(0.9f, 0.78f, 1f, 0.98f);
        bloomRenderer.sortingLayerName = "HUD";
        bloomRenderer.sortingOrder = 15;
        _transientVisuals.Add(new TransientVisual(bloom, bloomRenderer, VoidImpactBloomLifetime, Vector3.zero, bloomRenderer.color, new Vector3(0.96f, 0.96f, 1f)));

        GameObject rift = new("DeVect_VoidImpactRift");
        rift.transform.position = worldPosition;
        rift.transform.rotation = Quaternion.identity;
        rift.transform.localScale = new Vector3(0.42f, 1.12f, 1f);

        SpriteRenderer riftRenderer = rift.AddComponent<SpriteRenderer>();
        riftRenderer.sprite = CreateVoidDropletSprite();
        riftRenderer.color = new Color(0.2f, 0.04f, 0.24f, 1f);
        riftRenderer.sortingLayerName = "HUD";
        riftRenderer.sortingOrder = 16;
        _transientVisuals.Add(new TransientVisual(rift, riftRenderer, VoidImpactRiftLifetime, Vector3.zero, riftRenderer.color, new Vector3(0.68f, 1.56f, 1f)));

        Vector3[] velocities =
        {
            new Vector3(-1.45f, 2.35f, 0f),
            new Vector3(1.5f, 2.5f, 0f),
            new Vector3(-2.05f, 0.82f, 0f),
            new Vector3(2.1f, 0.9f, 0f),
            new Vector3(-0.95f, 0.18f, 0f),
            new Vector3(1.02f, 0.24f, 0f)
        };

        for (int i = 0; i < velocities.Length; i++)
        {
            GameObject wisp = new($"DeVect_VoidWisp_{i}");
            wisp.transform.position = worldPosition + new Vector3((i - 2.5f) * 0.03f, 0f, 0f);
            wisp.transform.rotation = Quaternion.Euler(0f, 0f, i % 2 == 0 ? -22f : 22f);
            wisp.transform.localScale = new Vector3(0.2f, 0.54f, 1f);

            SpriteRenderer wispRenderer = wisp.AddComponent<SpriteRenderer>();
            wispRenderer.sprite = CreateVoidDropletSprite();
            wispRenderer.color = i % 2 == 0
                ? new Color(0.42f, 0.2f, 0.55f, 0.92f)
                : new Color(0.78f, 0.58f, 0.96f, 0.88f);
            wispRenderer.sortingLayerName = "HUD";
            wispRenderer.sortingOrder = 17;
            _transientVisuals.Add(new TransientVisual(wisp, wispRenderer, VoidImpactWispLifetime, velocities[i], wispRenderer.color, new Vector3(0.28f, 0.68f, 1f)));
        }
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

    private static Sprite CreateGlassOrbSprite()
    {
        if (_glassOrbSprite != null)
        {
            return _glassOrbSprite;
        }

        const int size = 64;
        Texture2D texture = new(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            name = "DeVect_GlassOrb"
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

                float edgeFade = Mathf.Clamp01((1f - distance) / 0.08f);
                float sphereMask = Mathf.Clamp01(1f - (distance * distance));
                float diagonalShard = Mathf.Abs((offset.x * 0.82f) + (offset.y * 0.57f));
                float crossShard = Mathf.Abs((offset.x * -0.68f) + (offset.y * 0.74f));
                float crack = Mathf.Clamp01(1f - (Mathf.Min(diagonalShard, crossShard) * 8f));
                float rim = Mathf.Pow(Mathf.Clamp01(1f - distance), 0.55f);
                float brightness = 0.42f + (sphereMask * 0.26f) + (rim * 0.18f) + (crack * 0.12f);
                texture.SetPixel(x, y, new Color(brightness, brightness, brightness, edgeFade * 0.92f));
            }
        }

        texture.Apply();
        _glassOrbSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        _glassOrbSprite.name = "DeVect_GlassOrbSprite";
        return _glassOrbSprite;
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

    private static void AddWhiteOrbMaterialLayers(Transform parent, Color baseColor)
    {
        CreateLayerRenderer(
            parent,
            "GlassInnerGlow",
            CreateCircleSprite(),
            new Color(baseColor.r, baseColor.g, baseColor.b, 0.22f),
            11,
            new Vector3(0.01f, -0.01f, 0f),
            new Vector3(0.76f, 0.76f, 1f));

        CreateLayerRenderer(
            parent,
            "GlassFacet",
            CreateGlassShardSprite(),
            new Color(1f, 1f, 1f, 0.42f),
            12,
            new Vector3(-0.03f, 0.02f, 0f),
            new Vector3(0.55f, 0.55f, 1f));

        CreateLayerRenderer(
            parent,
            "GlassHighlight",
            CreateHighlightSprite(),
            new Color(1f, 1f, 1f, 0.9f),
            13,
            new Vector3(-0.11f, 0.11f, 0f),
            new Vector3(0.52f, 0.52f, 1f));
    }

    private static void AddBlackOrbMaterialLayers(Transform parent, Color baseColor)
    {
        CreateLayerRenderer(
            parent,
            "VoidHalo",
            CreateVoidRippleSprite(),
            new Color(0.09f, 0.02f, 0.12f, 0.56f),
            8,
            Vector3.zero,
            new Vector3(1.4f, 1.4f, 1f));

        CreateLayerRenderer(
            parent,
            "VoidCore",
            CreateCircleSprite(),
            new Color(baseColor.r * 1.1f, baseColor.g * 0.82f, baseColor.b * 1.18f, 0.34f),
            11,
            new Vector3(0.015f, -0.015f, 0f),
            new Vector3(0.7f, 0.7f, 1f));

        CreateLayerRenderer(
            parent,
            "VoidGloss",
            CreateHighlightSprite(),
            new Color(0.76f, 0.62f, 0.92f, 0.52f),
            12,
            new Vector3(-0.08f, 0.09f, 0f),
            new Vector3(0.46f, 0.46f, 1f));

        CreateLayerRenderer(
            parent,
            "VoidDroplet",
            CreateVoidDropletSprite(),
            new Color(0.14f, 0.05f, 0.18f, 0.72f),
            13,
            new Vector3(0.11f, -0.1f, 0f),
            new Vector3(0.22f, 0.22f, 1f));
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

    private static Sprite CreateGlassShardSprite()
    {
        if (_glassShardSprite != null)
        {
            return _glassShardSprite;
        }

        const int size = 32;
        Texture2D texture = new(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            name = "DeVect_GlassShard"
        };

        Vector2[] vertices =
        {
            new(size * 0.18f, size * 0.18f),
            new(size * 0.8f, size * 0.34f),
            new(size * 0.62f, size * 0.84f),
            new(size * 0.28f, size * 0.68f)
        };

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 point = new(x + 0.5f, y + 0.5f);
                bool inside = IsPointInPolygon(point, vertices);
                if (!inside)
                {
                    texture.SetPixel(x, y, Color.clear);
                    continue;
                }

                float highlight = Mathf.Clamp01((x / (float)size) * 0.55f + (y / (float)size) * 0.45f);
                texture.SetPixel(x, y, new Color(0.88f + (highlight * 0.12f), 0.95f + (highlight * 0.05f), 1f, 0.92f));
            }
        }

        texture.Apply();
        _glassShardSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        _glassShardSprite.name = "DeVect_GlassShardSprite";
        return _glassShardSprite;
    }

    private static Sprite CreateRefractionRingSprite()
    {
        if (_refractionRingSprite != null)
        {
            return _refractionRingSprite;
        }

        const int size = 48;
        Texture2D texture = new(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            name = "DeVect_RefractionRing"
        };

        float radius = (size - 1) * 0.5f;
        Vector2 center = new(radius, radius);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = ((new Vector2(x, y) - center).magnitude) / radius;
                float alpha = 1f - Mathf.Clamp01(Mathf.Abs(distance - 0.72f) / 0.12f);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha * 0.9f));
            }
        }

        texture.Apply();
        _refractionRingSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        _refractionRingSprite.name = "DeVect_RefractionRingSprite";
        return _refractionRingSprite;
    }

    private static Sprite CreateGlassFlashSprite()
    {
        if (_glassFlashSprite != null)
        {
            return _glassFlashSprite;
        }

        const int size = 48;
        Texture2D texture = new(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            name = "DeVect_GlassFlash"
        };

        Vector2 center = new(size * 0.5f, size * 0.5f);
        float radius = size * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 offset = new Vector2(x, y) - center;
                float distance = offset.magnitude / radius;
                float radial = Mathf.Clamp01(1f - distance);
                float starA = Mathf.Clamp01(1f - (Mathf.Abs(offset.x + offset.y) / (size * 0.24f)));
                float starB = Mathf.Clamp01(1f - (Mathf.Abs(offset.x - offset.y) / (size * 0.24f)));
                float alpha = Mathf.Clamp01((radial * 0.75f) + (Mathf.Max(starA, starB) * 0.75f));
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply();
        _glassFlashSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        _glassFlashSprite.name = "DeVect_GlassFlashSprite";
        return _glassFlashSprite;
    }

    private static bool IsPointInPolygon(Vector2 point, Vector2[] vertices)
    {
        bool inside = false;
        for (int i = 0, j = vertices.Length - 1; i < vertices.Length; j = i++)
        {
            bool intersects = ((vertices[i].y > point.y) != (vertices[j].y > point.y))
                && (point.x < ((vertices[j].x - vertices[i].x) * (point.y - vertices[i].y) / ((vertices[j].y - vertices[i].y) + Mathf.Epsilon)) + vertices[i].x);
            if (intersects)
            {
                inside = !inside;
            }
        }

        return inside;
    }

    private static Sprite CreateLightningSprite()
    {
        if (_lightningSprite != null)
        {
            return _lightningSprite;
        }

        Texture2D texture = CreateLightningTexture();
        _lightningSprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0f), 48f);
        _lightningSprite.name = "DeVect_LightningSprite";
        return _lightningSprite;
    }

    private static Sprite CreateVoidOrbSprite()
    {
        if (_voidOrbSprite != null)
        {
            return _voidOrbSprite;
        }

        const int size = 64;
        Texture2D texture = new(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            name = "DeVect_VoidOrb"
        };

        float radius = (size - 1) * 0.5f;
        Vector2 center = new(radius, radius);
        Vector2 flowDirection = new(-0.45f, 0.89f);
        flowDirection.Normalize();
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
                float directional = Mathf.Clamp01(Vector2.Dot(offset.normalized, flowDirection));
                float swirl = 0.5f + (0.5f * Mathf.Sin((offset.x * 8.5f) - (offset.y * 6.2f) + (distance * 7.5f)));
                float ooze = Mathf.Clamp01(1f - Mathf.Abs(offset.x * 0.72f - offset.y * 0.46f) * 1.75f);
                float brightness = 0.06f + (sphereMask * 0.16f) + (directional * 0.1f) + (swirl * 0.09f) + (ooze * 0.08f);
                float r = brightness * 0.78f;
                float g = brightness * 0.43f;
                float b = brightness * 1.18f;
                texture.SetPixel(x, y, new Color(r, g, b, edgeFade * 0.98f));
            }
        }

        texture.Apply();
        _voidOrbSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        _voidOrbSprite.name = "DeVect_VoidOrbSprite";
        return _voidOrbSprite;
    }

    private static Sprite CreateVoidRippleSprite()
    {
        if (_voidRippleSprite != null)
        {
            return _voidRippleSprite;
        }

        const int size = 48;
        Texture2D texture = new(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            name = "DeVect_VoidRipple"
        };

        float radius = (size - 1) * 0.5f;
        Vector2 center = new(radius, radius);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 offset = (new Vector2(x, y) - center) / radius;
                float distance = offset.magnitude;
                if (distance > 1f)
                {
                    texture.SetPixel(x, y, Color.clear);
                    continue;
                }

                float uneven = 0.72f + (0.07f * Mathf.Sin(Mathf.Atan2(offset.y, offset.x) * 5f));
                float shell = 1f - Mathf.Clamp01(Mathf.Abs(distance - uneven) / 0.16f);
                float haze = Mathf.Clamp01(1f - distance) * 0.25f;
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(shell + haze)));
            }
        }

        texture.Apply();
        _voidRippleSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        _voidRippleSprite.name = "DeVect_VoidRippleSprite";
        return _voidRippleSprite;
    }

    private static Sprite CreateVoidDropletSprite()
    {
        if (_voidDropletSprite != null)
        {
            return _voidDropletSprite;
        }

        const int size = 32;
        Texture2D texture = new(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            name = "DeVect_VoidDroplet"
        };

        Vector2 center = new(size * 0.46f, size * 0.54f);
        float radiusX = size * 0.24f;
        float radiusY = size * 0.32f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float normalizedX = (x - center.x) / radiusX;
                float normalizedY = (y - center.y) / radiusY;
                float distance = (normalizedX * normalizedX) + (normalizedY * normalizedY);
                if (distance > 1f)
                {
                    texture.SetPixel(x, y, Color.clear);
                    continue;
                }

                float tail = Mathf.Clamp01(1f - Mathf.Abs((x - (size * 0.5f)) / (size * 0.18f))) * Mathf.Clamp01((y - (size * 0.68f)) / (size * 0.16f));
                float alpha = Mathf.Clamp01(Mathf.Pow(1f - distance, 1.3f) + (tail * 0.75f));
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply();
        _voidDropletSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.72f), size);
        _voidDropletSprite.name = "DeVect_VoidDropletSprite";
        return _voidDropletSprite;
    }

    private static Texture2D CreateLightningTexture()
    {
        if (_lightningTexture != null)
        {
            return _lightningTexture;
        }

        const int size = 128;
        Texture2D texture = new(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            name = "DeVect_Lightning"
        };

        Color clear = new(0f, 0f, 0f, 0f);
        Color[] clearPixels = new Color[size * size];
        for (int i = 0; i < clearPixels.Length; i++)
        {
            clearPixels[i] = clear;
        }

        texture.SetPixels(clearPixels);

        Vector2 start = new(size * 0.48f, size * 0.05f);
        Vector2 end = new(size * 0.57f, size * 0.95f);
        Vector2[] mainPath = BuildLightningPath(17, start, end, size * 0.15f, 9);
        Color glowColor = new(0.16f, 0.8f, 1f, 0.28f);
        Color coreColor = new(0.94f, 0.99f, 1f, 0.98f);
        DrawLightningBolt(texture, mainPath, coreColor, glowColor, 1, 7);

        DrawLightningBranch(texture, mainPath[3], 152f, size * 0.2f, 23);
        DrawLightningBranch(texture, mainPath[4], 138f, size * 0.22f, 31);
        DrawLightningBranch(texture, mainPath[5], 28f, size * 0.19f, 47);
        DrawLightningBranch(texture, mainPath[6], 201f, size * 0.15f, 63);
        DrawLightningBranch(texture, mainPath[7], 16f, size * 0.13f, 79);

        Vector2 midFlash = mainPath[5];
        StampSoftPixel(texture, Mathf.RoundToInt(midFlash.x), Mathf.RoundToInt(midFlash.y), new Color(0.5f, 0.9f, 1f, 0.18f), 10);
        StampSoftPixel(texture, Mathf.RoundToInt(midFlash.x), Mathf.RoundToInt(midFlash.y), new Color(1f, 1f, 1f, 0.34f), 4);

        Vector2 impact = end;
        StampSoftPixel(texture, Mathf.RoundToInt(impact.x), Mathf.RoundToInt(impact.y), new Color(0.46f, 0.88f, 1f, 0.26f), 14);
        StampSoftPixel(texture, Mathf.RoundToInt(impact.x), Mathf.RoundToInt(impact.y), new Color(0.82f, 0.97f, 1f, 0.36f), 8);
        StampSoftPixel(texture, Mathf.RoundToInt(impact.x), Mathf.RoundToInt(impact.y), new Color(1f, 1f, 1f, 0.66f), 4);

        texture.Apply();

        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;
        _lightningTexture = texture;
        return _lightningTexture;
    }

    private static void DrawLightningBolt(Texture2D texture, Vector2[] points, Color coreColor, Color glowColor, int coreRadius, int glowRadius)
    {
        for (int i = 0; i < points.Length - 1; i++)
        {
            Vector2 from = points[i];
            Vector2 to = points[i + 1];
            float distance = Vector2.Distance(from, to);
            int steps = Mathf.Max(2, Mathf.CeilToInt(distance * 1.6f));
            for (int step = 0; step <= steps; step++)
            {
                float t = step / (float)steps;
                Vector2 point = Vector2.Lerp(from, to, t);
                int pixelX = Mathf.RoundToInt(point.x);
                int pixelY = Mathf.RoundToInt(point.y);
                StampSoftPixel(texture, pixelX, pixelY, glowColor, glowRadius);
                StampSoftPixel(texture, pixelX, pixelY, coreColor, coreRadius);
            }
        }
    }

    private static Vector2[] BuildLightningPath(int seed, Vector2 start, Vector2 end, float horizontalJitter, int segmentCount)
    {
        Vector2[] points = new Vector2[segmentCount + 1];
        Vector2 direction = end - start;
        Vector2 normal = new(-direction.y, direction.x);
        normal.Normalize();

        for (int i = 0; i <= segmentCount; i++)
        {
            float t = i / (float)segmentCount;
            Vector2 point = Vector2.Lerp(start, end, t);
            if (i != 0 && i != segmentCount)
            {
                float waveA = Mathf.Sin((seed * 0.83f) + (i * 1.71f));
                float waveB = Mathf.Sin((seed * 1.37f) - (i * 2.29f));
                float combined = Mathf.Clamp(waveA * 0.68f + waveB * 0.32f, -1f, 1f);
                float taper = 1f - Mathf.Abs((t - 0.5f) * 1.1f);
                point += normal * (combined * horizontalJitter * taper);
            }

            points[i] = point;
        }

        return points;
    }

    private static void StampSoftPixel(Texture2D texture, int x, int y, Color color, int radius)
    {
        int width = texture.width;
        int height = texture.height;
        for (int offsetY = -radius; offsetY <= radius; offsetY++)
        {
            for (int offsetX = -radius; offsetX <= radius; offsetX++)
            {
                int pixelX = x + offsetX;
                int pixelY = y + offsetY;
                if (pixelX < 0 || pixelX >= width || pixelY < 0 || pixelY >= height)
                {
                    continue;
                }

                float distance = Mathf.Sqrt((offsetX * offsetX) + (offsetY * offsetY));
                if (distance > radius)
                {
                    continue;
                }

                float falloff = radius <= 0 ? 1f : Mathf.Pow(1f - (distance / radius), 1.85f);
                if (falloff <= 0f)
                {
                    continue;
                }

                Color source = new(color.r, color.g, color.b, color.a * falloff);
                Color destination = texture.GetPixel(pixelX, pixelY);
                float outAlpha = source.a + (destination.a * (1f - source.a));
                if (outAlpha <= 0f)
                {
                    continue;
                }

                Color blended = new(
                    ((source.r * source.a) + (destination.r * destination.a * (1f - source.a))) / outAlpha,
                    ((source.g * source.a) + (destination.g * destination.a * (1f - source.a))) / outAlpha,
                    ((source.b * source.a) + (destination.b * destination.a * (1f - source.a))) / outAlpha,
                    outAlpha);
                texture.SetPixel(pixelX, pixelY, blended);
            }
        }
    }

    private static void DrawLightningBranch(Texture2D texture, Vector2 origin, float angleDeg, float length, int seed)
    {
        float radians = angleDeg * Mathf.Deg2Rad;
        Vector2 direction = new(Mathf.Cos(radians), Mathf.Sin(radians));
        Vector2 end = origin + (direction * length);
        Vector2[] branchPath = BuildLightningPath(seed, origin, end, length * 0.16f, 5);
        DrawLightningBolt(
            texture,
            branchPath,
            new Color(0.9f, 0.98f, 1f, 0.84f),
            new Color(0.14f, 0.76f, 1f, 0.18f),
            1,
            4);
    }
}
