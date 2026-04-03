using System.Collections.Generic;
using DeVect.Orbs.Definitions;
using UnityEngine;

namespace DeVect.Visual;

internal sealed class OrbVisualService
{
    private const float LightningTextureSize = 128f;
    private const float LightningPixelsPerUnit = 48f;
    private const float LightningBaseWorldHeight = LightningTextureSize / LightningPixelsPerUnit;
    private const float DashedRingRadius = 0.225f;
    private const float DashScale = 0.086f;
    private const float DashThicknessFactor = 0.32f;
    private const float PassiveLightningScale = 0.94f;
    private const float PassiveLightningLifetime = 0.5f;
    private const float EvocationLightningScale = 1.68f;
    private const float EvocationLightningLifetime = 0.8f;
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
    private static Sprite? _lightningImpactSprite;
    private static Sprite? _iceOrbSprite;
    private static Sprite? _icePetalSprite;
    private static Sprite? _iceBloomSprite;
    private static Sprite? _iceCrystalSprite;

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
            OrbTypeId.Black => CreateIceOrbSprite(),
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
            AddIceOrbMaterialLayers(orb.transform, color);
        }
        else
        {
            AddOrbMaterialLayers(orb.transform, color);
        }

        return renderer;
    }

    public void SpawnLightningVisual(Vector3 worldPosition, bool isEvocation = false)
    {
        LightningVisualProfile profile = isEvocation ? LightningVisualProfile.Evocation() : LightningVisualProfile.Passive();
        Vector3 lightningEnd = GetLightningEndWorldPosition(worldPosition);
        Vector3 lightningTop = GetLightningTopWorldPosition(lightningEnd, profile.TopInsetWorld);
        float boltWorldHeight = Mathf.Max(Mathf.Abs(lightningTop.y - lightningEnd.y), profile.MinimumBoltWorldHeight);
        float normalizedBoltHeight = Mathf.Max(0.01f, profile.StartHeightNormalized - profile.ImpactHeightNormalized);
        float spriteWorldHeight = boltWorldHeight / normalizedBoltHeight;
        float impactOffset = spriteWorldHeight * profile.ImpactHeightNormalized;

        GameObject lightning = new("DeVect_LightningVisual");
        lightning.transform.position = new Vector3(lightningEnd.x, lightningEnd.y - impactOffset + profile.TargetYOffset, lightningEnd.z);
        lightning.transform.rotation = Quaternion.identity;
        lightning.transform.localScale = new Vector3(profile.BeamScale, profile.BeamScale, 1f);

        SpriteRenderer renderer = lightning.AddComponent<SpriteRenderer>();
        renderer.sprite = CreateLightningSprite(profile, spriteWorldHeight);
        renderer.color = profile.BeamTint;
        renderer.sortingLayerName = "HUD";
        renderer.sortingOrder = 12;

        _transientVisuals.Add(new TransientVisual(lightning, renderer, profile.Lifetime, Vector3.up * profile.DriftVelocity, renderer.color, lightning.transform.localScale));

        GameObject impactFlash = new("DeVect_LightningImpactFlash");
        impactFlash.transform.position = lightningEnd + new Vector3(0f, profile.TargetYOffset + profile.ImpactYOffset, 0f);
        impactFlash.transform.rotation = Quaternion.identity;
        impactFlash.transform.localScale = new Vector3(profile.ImpactFlashScale, profile.ImpactFlashScale, 1f);

        SpriteRenderer flashRenderer = impactFlash.AddComponent<SpriteRenderer>();
        flashRenderer.sprite = CreateLightningImpactSprite();
        flashRenderer.color = profile.ImpactFlashColor;
        flashRenderer.sortingLayerName = "HUD";
        flashRenderer.sortingOrder = 13;
        _transientVisuals.Add(new TransientVisual(impactFlash, flashRenderer, profile.Lifetime * 0.52f, Vector3.zero, flashRenderer.color, new Vector3(profile.ImpactFlashScale * 1.35f, profile.ImpactFlashScale * 1.35f, 1f)));
    }

    private static Vector3 GetLightningTopWorldPosition(Vector3 endWorldPosition, float topInsetWorld)
    {
        Camera? mainCamera = GameCameras.instance != null ? GameCameras.instance.mainCamera : null;
        if (mainCamera == null || !mainCamera.gameObject.activeInHierarchy)
        {
            return endWorldPosition + new Vector3(0f, LightningBaseWorldHeight + topInsetWorld, 0f);
        }

        float cameraDistance = Mathf.Abs(endWorldPosition.z - mainCamera.transform.position.z);
        Vector3 targetViewportPosition = mainCamera.WorldToViewportPoint(endWorldPosition);
        float viewportX = Mathf.Clamp01(targetViewportPosition.x);
        float viewportY = Mathf.Clamp01(1f - (topInsetWorld / Mathf.Max(0.01f, mainCamera.orthographicSize * 2f)));
        Vector3 topWorldPosition = mainCamera.ViewportToWorldPoint(new Vector3(viewportX, viewportY, cameraDistance));
        topWorldPosition.z = endWorldPosition.z;
        return topWorldPosition;
    }

    private static Vector3 GetLightningEndWorldPosition(Vector3 worldPosition)
    {
        return new Vector3(worldPosition.x, worldPosition.y, 0f);
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
        bloomRenderer.sprite = CreateIceBloomSprite();
        bloomRenderer.color = new Color(0.9f, 0.78f, 1f, 0.98f);
        bloomRenderer.sortingLayerName = "HUD";
        bloomRenderer.sortingOrder = 15;
        _transientVisuals.Add(new TransientVisual(bloom, bloomRenderer, VoidImpactBloomLifetime, Vector3.zero, bloomRenderer.color, new Vector3(0.96f, 0.96f, 1f)));

        GameObject rift = new("DeVect_VoidImpactRift");
        rift.transform.position = worldPosition;
        rift.transform.rotation = Quaternion.identity;
        rift.transform.localScale = new Vector3(0.42f, 1.12f, 1f);

        SpriteRenderer riftRenderer = rift.AddComponent<SpriteRenderer>();
        riftRenderer.sprite = CreateIceCrystalSprite();
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
            wispRenderer.sprite = CreateIceCrystalSprite();
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

    public void SpawnIcePetalEffect(Vector3 worldPosition, int petalCount)
    {
        bool isFullBloom = petalCount >= 4;
        float lifetime = isFullBloom ? 1.15f : 0.82f;

        GameObject mist = new(isFullBloom ? "DeVect_IceLotusMist" : "DeVect_IceMist");
        mist.transform.position = worldPosition + new Vector3(0f, 0.02f, 0f);
        mist.transform.rotation = Quaternion.identity;
        mist.transform.localScale = new Vector3(isFullBloom ? 1.08f : 0.78f, isFullBloom ? 0.92f : 0.68f, 1f);

        SpriteRenderer mistRenderer = mist.AddComponent<SpriteRenderer>();
        mistRenderer.sprite = CreateIceBloomSprite();
        mistRenderer.color = isFullBloom
            ? new Color(0.54f, 0.84f, 1f, 0.2f)
            : new Color(0.52f, 0.8f, 1f, 0.14f);
        mistRenderer.sortingLayerName = "HUD";
        mistRenderer.sortingOrder = 15;
        _transientVisuals.Add(new TransientVisual(mist, mistRenderer, lifetime, Vector3.up * (isFullBloom ? 0.12f : 0.08f), mistRenderer.color, new Vector3(isFullBloom ? 1.34f : 1.12f, isFullBloom ? 1.12f : 0.92f, 1f)));

        GameObject bloom = new(isFullBloom ? "DeVect_IceLotusBloom" : "DeVect_IcePetalBloom");
        bloom.transform.position = worldPosition;
        bloom.transform.rotation = Quaternion.identity;
        bloom.transform.localScale = new Vector3(isFullBloom ? 0.92f : 0.62f, isFullBloom ? 0.92f : 0.62f, 1f);

        SpriteRenderer bloomRenderer = bloom.AddComponent<SpriteRenderer>();
        bloomRenderer.sprite = CreateIceBloomSprite();
        bloomRenderer.color = isFullBloom
            ? new Color(0.74f, 0.92f, 1f, 0.42f)
            : new Color(0.7f, 0.89f, 1f, 0.3f);
        bloomRenderer.sortingLayerName = "HUD";
        bloomRenderer.sortingOrder = 16;
        _transientVisuals.Add(new TransientVisual(bloom, bloomRenderer, lifetime, Vector3.up * (isFullBloom ? 0.18f : 0.11f), bloomRenderer.color, bloom.transform.localScale));

        int visualPetalCount = isFullBloom ? 6 : Mathf.Max(3, petalCount + 2);
        for (int i = 0; i < visualPetalCount; i++)
        {
            float angle = ((Mathf.PI * 2f * i) / visualPetalCount) + (Mathf.PI / visualPetalCount);
            Vector3 outward = new(Mathf.Cos(angle), Mathf.Sin(angle), 0f);

            float ringRadius = isFullBloom ? 0.19f : 0.11f;
            GameObject petal = new($"DeVect_IceLotusPetal_{i}");
            petal.transform.position = worldPosition + (outward * ringRadius);
            petal.transform.rotation = Quaternion.Euler(0f, 0f, (angle * Mathf.Rad2Deg) - 90f);
            petal.transform.localScale = new Vector3(isFullBloom ? 0.68f : 0.48f, isFullBloom ? 0.78f : 0.56f, 1f);

            SpriteRenderer petalRenderer = petal.AddComponent<SpriteRenderer>();
            petalRenderer.sprite = CreateIcePetalSprite();
            petalRenderer.color = isFullBloom
                ? new Color(0.74f, 0.93f, 1f, 0.58f)
                : new Color(0.7f, 0.9f, 1f, 0.42f);
            petalRenderer.sortingLayerName = "HUD";
            petalRenderer.sortingOrder = 17;

            TransientVisual visual = new(petal, petalRenderer, lifetime, Vector3.zero, petalRenderer.color, petal.transform.localScale)
            {
                UseArcMotion = true,
                StartPosition = petal.transform.position,
                EndPosition = worldPosition + (outward * (isFullBloom ? 1.02f : 0.64f)) + new Vector3(0f, isFullBloom ? 0.36f : 0.2f, 0f),
                ArcHeight = isFullBloom ? 0.34f : 0.16f
            };
            _transientVisuals.Add(visual);
        }

        int crystalCount = isFullBloom ? 8 : 4;
        for (int i = 0; i < crystalCount; i++)
        {
            float angle = i * (360f / crystalCount) * Mathf.Deg2Rad;
            Vector3 outward = new(Mathf.Cos(angle), Mathf.Sin(angle), 0f);

            GameObject crystal = new($"DeVect_IceLotusCrystal_{i}");
            crystal.transform.position = worldPosition + (outward * (isFullBloom ? 0.2f : 0.1f));
            crystal.transform.rotation = Quaternion.Euler(0f, 0f, (i * (360f / crystalCount)) + 15f);
            crystal.transform.localScale = new Vector3(isFullBloom ? 0.24f : 0.16f, isFullBloom ? 0.24f : 0.16f, 1f);

            SpriteRenderer crystalRenderer = crystal.AddComponent<SpriteRenderer>();
            crystalRenderer.sprite = CreateIceCrystalSprite();
            crystalRenderer.color = isFullBloom
                ? new Color(0.9f, 0.98f, 1f, 0.44f)
                : new Color(0.82f, 0.95f, 1f, 0.28f);
            crystalRenderer.sortingLayerName = "HUD";
            crystalRenderer.sortingOrder = 18;
            _transientVisuals.Add(new TransientVisual(crystal, crystalRenderer, lifetime * 0.72f, outward * (isFullBloom ? 0.72f : 0.46f), crystalRenderer.color, crystal.transform.localScale));
        }
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

    private static void AddIceOrbMaterialLayers(Transform parent, Color baseColor)
    {
        CreateLayerRenderer(
            parent,
            "IceHalo",
            CreateIceBloomSprite(),
            new Color(0.72f, 0.9f, 1f, 0.46f),
            8,
            Vector3.zero,
            new Vector3(1.36f, 1.36f, 1f));

        CreateLayerRenderer(
            parent,
            "IceCore",
            CreateCircleSprite(),
            new Color(baseColor.r, baseColor.g, baseColor.b, 0.34f),
            11,
            new Vector3(0.015f, -0.015f, 0f),
            new Vector3(0.7f, 0.7f, 1f));

        CreateLayerRenderer(
            parent,
            "IceGloss",
            CreateHighlightSprite(),
            new Color(0.94f, 0.98f, 1f, 0.72f),
            12,
            new Vector3(-0.08f, 0.09f, 0f),
            new Vector3(0.46f, 0.46f, 1f));

        CreateLayerRenderer(
            parent,
            "IceCrystal",
            CreateIceCrystalSprite(),
            new Color(0.86f, 0.96f, 1f, 0.62f),
            13,
            new Vector3(0.11f, -0.06f, 0f),
            new Vector3(0.18f, 0.18f, 1f));
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

    private static Sprite CreateIceOrbSprite()
    {
        if (_iceOrbSprite != null)
        {
            return _iceOrbSprite;
        }

        const int size = 64;
        Texture2D texture = new(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            name = "DeVect_IceOrb"
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
                float frostVeinA = Mathf.Clamp01(1f - Mathf.Abs(offset.x + offset.y) * 3.2f);
                float frostVeinB = Mathf.Clamp01(1f - Mathf.Abs(offset.x - offset.y) * 3.2f);
                float coreGlow = Mathf.Clamp01(1f - distance * 1.35f);
                float brightness = 0.14f + (sphereMask * 0.26f) + (Mathf.Max(frostVeinA, frostVeinB) * 0.18f) + (coreGlow * 0.16f);
                texture.SetPixel(x, y, new Color(brightness * 0.84f, brightness * 0.96f, brightness, edgeFade * 0.98f));
            }
        }

        texture.Apply();
        _iceOrbSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        _iceOrbSprite.name = "DeVect_IceOrbSprite";
        return _iceOrbSprite;
    }

    private static Sprite CreateIceBloomSprite()
    {
        if (_iceBloomSprite != null)
        {
            return _iceBloomSprite;
        }

        const int size = 48;
        Texture2D texture = new(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            name = "DeVect_IceBloom"
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

                float angle = Mathf.Atan2(offset.y, offset.x);
                float petalWave = 0.64f + (0.14f * Mathf.Cos(angle * 4f));
                float shell = 1f - Mathf.Clamp01(Mathf.Abs(distance - petalWave) / 0.18f);
                float haze = Mathf.Clamp01(1f - distance) * 0.36f;
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(shell + haze)));
            }
        }

        texture.Apply();
        _iceBloomSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        _iceBloomSprite.name = "DeVect_IceBloomSprite";
        return _iceBloomSprite;
    }

    private static Sprite CreateIcePetalSprite()
    {
        if (_icePetalSprite != null)
        {
            return _icePetalSprite;
        }

        const int size = 32;
        Texture2D texture = new(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            name = "DeVect_IcePetal"
        };

        Vector2 center = new(size * 0.5f, size * 0.34f);
        float radiusX = size * 0.22f;
        float radiusY = size * 0.26f;
        Vector2 tip = new(size * 0.5f, size * 0.9f);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float normalizedX = (x - center.x) / radiusX;
                float normalizedY = (y - center.y) / radiusY;
                float distance = (normalizedX * normalizedX) + (normalizedY * normalizedY);
                float bodyMask = distance <= 1f ? Mathf.Pow(1f - distance, 1.2f) : 0f;
                float tipMask = Mathf.Clamp01(1f - (Vector2.Distance(new Vector2(x, y), tip) / (size * 0.24f)));
                float veinMask = Mathf.Clamp01(1f - Mathf.Abs(x - center.x) / (size * 0.12f)) * Mathf.Clamp01((y - center.y) / (size * 0.42f));
                float alpha = Mathf.Clamp01(bodyMask + (tipMask * 0.58f));
                texture.SetPixel(x, y, alpha <= 0f ? Color.clear : new Color(1f, 1f, 1f, Mathf.Clamp01(alpha + (veinMask * 0.1f))));
            }
        }

        texture.Apply();
        _icePetalSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.18f), size);
        _icePetalSprite.name = "DeVect_IcePetalSprite";
        return _icePetalSprite;
    }

    private static Sprite CreateIceCrystalSprite()
    {
        if (_iceCrystalSprite != null)
        {
            return _iceCrystalSprite;
        }

        const int size = 32;
        Texture2D texture = new(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            name = "DeVect_IceCrystal"
        };

        Vector2[] crystal =
        {
            new(size * 0.5f, size * 0.98f),
            new(size * 0.7f, size * 0.58f),
            new(size * 0.56f, size * 0.06f),
            new(size * 0.32f, size * 0.52f)
        };

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 point = new(x, y);
                if (!IsPointInPolygon(point, crystal))
                {
                    texture.SetPixel(x, y, Color.clear);
                    continue;
                }

                float centerGlow = Mathf.Clamp01(1f - (Mathf.Abs(x - (size * 0.5f)) / (size * 0.18f)));
                float taper = Mathf.Clamp01(y / (size * 0.98f));
                texture.SetPixel(x, y, new Color(0.92f, 0.98f, 1f, Mathf.Clamp01(0.38f + (centerGlow * 0.42f) + (taper * 0.2f))));
            }
        }

        texture.Apply();
        _iceCrystalSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.08f), size);
        _iceCrystalSprite.name = "DeVect_IceCrystalSprite";
        return _iceCrystalSprite;
    }

    private static Sprite CreateLightningSprite(LightningVisualProfile profile, float spriteWorldHeight)
    {
        Texture2D texture = CreateLightningTexture(profile, spriteWorldHeight);
        float pivotY = Mathf.Clamp01(profile.ImpactHeightNormalized);
        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, pivotY), LightningPixelsPerUnit);
        sprite.name = profile.IsEvocation ? "DeVect_LightningSprite_Evocation" : "DeVect_LightningSprite_Passive";
        return sprite;
    }

    private static Sprite CreateLightningImpactSprite()
    {
        if (_lightningImpactSprite != null)
        {
            return _lightningImpactSprite;
        }

        const int size = 64;
        Texture2D texture = new(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            name = "DeVect_LightningImpact"
        };

        Vector2 center = new(size * 0.5f, size * 0.5f);
        float radius = size * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 offset = new(x, y);
                offset -= center;
                float distance = offset.magnitude / radius;
                if (distance > 1f)
                {
                    texture.SetPixel(x, y, Color.clear);
                    continue;
                }

                float radial = Mathf.Pow(1f - distance, 1.25f);
                float starA = Mathf.Clamp01(1f - (Mathf.Abs(offset.x + offset.y) / (size * 0.18f)));
                float starB = Mathf.Clamp01(1f - (Mathf.Abs(offset.x - offset.y) / (size * 0.18f)));
                float vertical = Mathf.Clamp01(1f - (Mathf.Abs(offset.x) / (size * 0.12f)));
                float horizontal = Mathf.Clamp01(1f - (Mathf.Abs(offset.y) / (size * 0.12f)));
                float alpha = Mathf.Clamp01((radial * 0.8f) + (Mathf.Max(starA, starB) * 0.48f) + (Mathf.Max(vertical, horizontal) * 0.22f));
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply();
        _lightningImpactSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        _lightningImpactSprite.name = "DeVect_LightningImpactSprite";
        return _lightningImpactSprite;
    }

    private static Texture2D CreateLightningTexture(LightningVisualProfile profile, float spriteWorldHeight)
    {
        int width = Mathf.RoundToInt(LightningTextureSize);
        int height = Mathf.Max(Mathf.RoundToInt(spriteWorldHeight * LightningPixelsPerUnit), Mathf.RoundToInt(LightningTextureSize));
        Texture2D texture = new(width, height, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            name = profile.IsEvocation ? "DeVect_Lightning_Evocation" : "DeVect_Lightning_Passive"
        };

        Color clear = new(0f, 0f, 0f, 0f);
        Color[] clearPixels = new Color[width * height];
        for (int i = 0; i < clearPixels.Length; i++)
        {
            clearPixels[i] = clear;
        }

        texture.SetPixels(clearPixels);

        float startX = (width * 0.5f) + Random.Range(-profile.StartOffsetPixels, profile.StartOffsetPixels);
        Vector2 start = new(startX, height * profile.StartHeightNormalized);
        Vector2 end = new((width * 0.5f) + Random.Range(-profile.EndOffsetPixels, profile.EndOffsetPixels), height * profile.ImpactHeightNormalized);
        Vector2[] mainPath = BuildLightningPath(Random.Range(1, 5000), start, end, width * profile.MainJitterFactor, profile.MainSegmentCount);
        DrawLightningBolt(texture, mainPath, profile.CoreColor, profile.GlowColor, profile.CoreRadius, profile.GlowRadius);

        int minBranchIndex = Mathf.Max(2, Mathf.FloorToInt(mainPath.Length * 0.28f));
        int maxBranchIndex = Mathf.Min(mainPath.Length - 3, Mathf.CeilToInt(mainPath.Length * 0.78f));
        for (int i = 0; i < profile.BranchCount; i++)
        {
            int pointIndex = Mathf.Clamp(Random.Range(minBranchIndex, maxBranchIndex + 1), 1, mainPath.Length - 2);
            float baseAngle = mainPath[pointIndex].x <= end.x ? 210f : 150f;
            if (Random.value > 0.5f)
            {
                baseAngle = mainPath[pointIndex].x <= end.x ? 28f : 332f;
            }

            float angle = baseAngle + Random.Range(-22f, 22f);
            float length = width * Random.Range(profile.BranchLengthMin, profile.BranchLengthMax);
            DrawLightningBranch(texture, mainPath[pointIndex], angle, length, Random.Range(1, 5000), profile.BranchCoreColor, profile.BranchGlowColor, profile.BranchCoreRadius, profile.BranchGlowRadius);
        }

        Vector2 midFlash = mainPath[Mathf.Clamp(mainPath.Length / 2, 1, mainPath.Length - 2)];
        StampSoftPixel(texture, Mathf.RoundToInt(midFlash.x), Mathf.RoundToInt(midFlash.y), profile.MidGlowColor, profile.MidGlowRadius);
        StampSoftPixel(texture, Mathf.RoundToInt(midFlash.x), Mathf.RoundToInt(midFlash.y), profile.MidCoreColor, profile.MidCoreRadius);

        Vector2 impact = end;
        StampSoftPixel(texture, Mathf.RoundToInt(impact.x), Mathf.RoundToInt(impact.y), profile.ImpactGlowColor, profile.ImpactGlowRadius);
        StampSoftPixel(texture, Mathf.RoundToInt(impact.x), Mathf.RoundToInt(impact.y), profile.ImpactCoreColor, profile.ImpactCoreRadius);
        StampSoftPixel(texture, Mathf.RoundToInt(impact.x), Mathf.RoundToInt(impact.y), profile.ImpactHotColor, profile.ImpactHotRadius);

        texture.Apply();

        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;
        return texture;
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

    private static void DrawLightningBranch(Texture2D texture, Vector2 origin, float angleDeg, float length, int seed, Color coreColor, Color glowColor, int coreRadius, int glowRadius)
    {
        float radians = angleDeg * Mathf.Deg2Rad;
        Vector2 direction = new(Mathf.Cos(radians), Mathf.Sin(radians));
        Vector2 end = origin + (direction * length);
        Vector2[] branchPath = BuildLightningPath(seed, origin, end, length * 0.16f, 5);
        DrawLightningBolt(texture, branchPath, coreColor, glowColor, coreRadius, glowRadius);
    }

    private readonly struct LightningVisualProfile
    {
        public bool IsEvocation { get; init; }

        public float Lifetime { get; init; }

        public float BeamScale { get; init; }

        public float DriftVelocity { get; init; }

        public float MinimumBoltWorldHeight { get; init; }

        public float TopInsetWorld { get; init; }

        public float TargetYOffset { get; init; }

        public float StartHeightNormalized { get; init; }

        public float ImpactHeightNormalized { get; init; }

        public float StartOffsetPixels { get; init; }

        public float EndOffsetPixels { get; init; }

        public int BranchCount { get; init; }

        public int MainSegmentCount { get; init; }

        public float MainJitterFactor { get; init; }

        public float BranchLengthMin { get; init; }

        public float BranchLengthMax { get; init; }

        public int CoreRadius { get; init; }

        public int GlowRadius { get; init; }

        public int BranchCoreRadius { get; init; }

        public int BranchGlowRadius { get; init; }

        public int MidGlowRadius { get; init; }

        public int MidCoreRadius { get; init; }

        public int ImpactGlowRadius { get; init; }

        public int ImpactCoreRadius { get; init; }

        public int ImpactHotRadius { get; init; }

        public float ImpactFlashScale { get; init; }

        public float ImpactYOffset { get; init; }

        public Color BeamTint { get; init; }

        public Color GlowColor { get; init; }

        public Color CoreColor { get; init; }

        public Color BranchGlowColor { get; init; }

        public Color BranchCoreColor { get; init; }

        public Color MidGlowColor { get; init; }

        public Color MidCoreColor { get; init; }

        public Color ImpactGlowColor { get; init; }

        public Color ImpactCoreColor { get; init; }

        public Color ImpactHotColor { get; init; }

        public Color ImpactFlashColor { get; init; }

        public static LightningVisualProfile Passive()
        {
            return new LightningVisualProfile
            {
                IsEvocation = false,
                Lifetime = PassiveLightningLifetime,
                BeamScale = PassiveLightningScale,
                DriftVelocity = 1.2f,
                MinimumBoltWorldHeight = 3.15f,
                TopInsetWorld = 0.55f,
                TargetYOffset = -0.24f,
                StartHeightNormalized = 0.92f,
                ImpactHeightNormalized = 0.03f,
                StartOffsetPixels = 16f,
                EndOffsetPixels = 3f,
                BranchCount = Random.Range(3, 5),
                MainSegmentCount = 10,
                MainJitterFactor = 0.12f,
                BranchLengthMin = 0.12f,
                BranchLengthMax = 0.18f,
                CoreRadius = 1,
                GlowRadius = 5,
                BranchCoreRadius = 1,
                BranchGlowRadius = 3,
                MidGlowRadius = 8,
                MidCoreRadius = 3,
                ImpactGlowRadius = 10,
                ImpactCoreRadius = 5,
                ImpactHotRadius = 2,
                ImpactFlashScale = 0.72f,
                ImpactYOffset = 0.02f,
                BeamTint = new Color(0.95f, 0.98f, 1f, 0.96f),
                GlowColor = new Color(0.18f, 0.78f, 1f, 0.22f),
                CoreColor = new Color(0.94f, 0.99f, 1f, 0.94f),
                BranchGlowColor = new Color(0.16f, 0.74f, 1f, 0.15f),
                BranchCoreColor = new Color(0.9f, 0.98f, 1f, 0.78f),
                MidGlowColor = new Color(0.52f, 0.9f, 1f, 0.14f),
                MidCoreColor = new Color(1f, 1f, 1f, 0.26f),
                ImpactGlowColor = new Color(0.46f, 0.88f, 1f, 0.2f),
                ImpactCoreColor = new Color(0.82f, 0.97f, 1f, 0.28f),
                ImpactHotColor = new Color(1f, 1f, 1f, 0.46f),
                ImpactFlashColor = new Color(0.8f, 0.95f, 1f, 0.7f)
            };
        }

        public static LightningVisualProfile Evocation()
        {
            return new LightningVisualProfile
            {
                IsEvocation = true,
                Lifetime = EvocationLightningLifetime,
                BeamScale = EvocationLightningScale,
                DriftVelocity = 1.55f,
                MinimumBoltWorldHeight = 4.2f,
                TopInsetWorld = 0.7f,
                TargetYOffset = -0.3f,
                StartHeightNormalized = 0.96f,
                ImpactHeightNormalized = 0.025f,
                StartOffsetPixels = 8f,
                EndOffsetPixels = 2f,
                BranchCount = Random.Range(5, 7),
                MainSegmentCount = 12,
                MainJitterFactor = 0.1f,
                BranchLengthMin = 0.15f,
                BranchLengthMax = 0.24f,
                CoreRadius = 2,
                GlowRadius = 8,
                BranchCoreRadius = 1,
                BranchGlowRadius = 4,
                MidGlowRadius = 12,
                MidCoreRadius = 5,
                ImpactGlowRadius = 16,
                ImpactCoreRadius = 8,
                ImpactHotRadius = 4,
                ImpactFlashScale = 1.02f,
                ImpactYOffset = 0.05f,
                BeamTint = new Color(1f, 1f, 1f, 1f),
                GlowColor = new Color(0.28f, 0.86f, 1f, 0.32f),
                CoreColor = new Color(1f, 1f, 1f, 0.98f),
                BranchGlowColor = new Color(0.22f, 0.82f, 1f, 0.2f),
                BranchCoreColor = new Color(0.96f, 0.99f, 1f, 0.82f),
                MidGlowColor = new Color(0.72f, 0.95f, 1f, 0.2f),
                MidCoreColor = new Color(1f, 1f, 1f, 0.38f),
                ImpactGlowColor = new Color(0.58f, 0.92f, 1f, 0.28f),
                ImpactCoreColor = new Color(0.9f, 0.99f, 1f, 0.42f),
                ImpactHotColor = new Color(1f, 1f, 1f, 0.74f),
                ImpactFlashColor = new Color(0.92f, 0.99f, 1f, 0.9f)
            };
        }
    }
}
