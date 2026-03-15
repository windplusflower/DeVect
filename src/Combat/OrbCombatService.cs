using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeVect.Combat;

internal sealed class OrbCombatService
{
    private static readonly bool DrawEnemySearchDebugBox = false;
    private const float EnemySearchDebugDuration = 0.12f;
    private const bool UseStrictTargetDistanceFilter = true;
    private const float EnemySearchLeftRange = 15f;
    private const float EnemySearchRightRange = 15f;
    private const float EnemySearchUpRange = 15f;
    private const float EnemySearchDownRange = 5f;
    private static readonly Color EnemySearchDebugColor = new(1f, 0.1f, 0.1f, 1f);
    private static readonly Vector2 EnemySearchBoxSize = new(EnemySearchLeftRange + EnemySearchRightRange, EnemySearchUpRange + EnemySearchDownRange);
    private static GameObject? _enemySearchDebugRoot;
    private static LineRenderer? _enemySearchDebugRenderer;
    private static float _enemySearchDebugVisibleUntil;

    private readonly Collider2D[] _enemySearchResults = new Collider2D[128];

    public HealthManager? TryPickRandomEnemyInRange(HeroController hero)
    {
        List<HealthManager> candidates = FindAllEnemiesInRange(hero);

        if (candidates.Count == 0)
        {
            return null;
        }

        int index = UnityEngine.Random.Range(0, candidates.Count);
        return candidates[index];
    }

    public HealthManager? TryPickLowestHpEnemyInRange(HeroController hero)
    {
        List<HealthManager> candidates = FindAllEnemiesInRange(hero);
        if (candidates.Count == 0)
        {
            return null;
        }

        HealthManager best = candidates[0];
        for (int i = 1; i < candidates.Count; i++)
        {
            if (candidates[i].hp < best.hp)
            {
                best = candidates[i];
            }
        }

        return best;
    }

    public List<HealthManager> FindAllEnemiesInRange(HeroController hero)
    {
        List<HealthManager> candidates = new();
        if (hero == null)
        {
            return candidates;
        }

        if (DrawEnemySearchDebugBox)
        {
            DrawEnemySearchBounds(hero.transform.position);
        }

        Vector3 searchCenter = hero.transform.position + new Vector3((EnemySearchRightRange - EnemySearchLeftRange) * 0.5f, (EnemySearchUpRange - EnemySearchDownRange) * 0.5f, 0f);

        Array.Clear(_enemySearchResults, 0, _enemySearchResults.Length);
        int hitCount = Physics2D.OverlapBoxNonAlloc(searchCenter, EnemySearchBoxSize, 0f, _enemySearchResults);
        if (hitCount <= 0)
        {
            return candidates;
        }

        HashSet<HealthManager> seen = new();
        for (int i = 0; i < hitCount; i++)
        {
            Collider2D collider = _enemySearchResults[i];
            if (!IsEnemyCollider(collider))
            {
                continue;
            }

            HealthManager healthManager = collider.GetComponentInParent<HealthManager>();
            if (healthManager == null || healthManager.isDead || !seen.Add(healthManager))
            {
                continue;
            }

            if (UseStrictTargetDistanceFilter && !IsTargetWithinStrictRange(hero, healthManager))
            {
                continue;
            }

            candidates.Add(healthManager);
        }

        return candidates;
    }

    public void TickDebugVisuals()
    {
        TickEnemySearchDebugRenderer();
    }

    public void DisposeDebugVisuals()
    {
        _enemySearchDebugVisibleUntil = 0f;

        if (_enemySearchDebugRoot != null)
        {
            UnityEngine.Object.Destroy(_enemySearchDebugRoot);
        }

        _enemySearchDebugRoot = null;
        _enemySearchDebugRenderer = null;
    }

    public bool TryDealOrbDamage(HeroController hero, HealthManager target, int damage, AttackTypes attackType)
    {
        if (hero == null || target == null || target.isDead || damage <= 0)
        {
            return false;
        }

        int previousHp = target.hp;
        bool wasDead = target.isDead;
        HitInstance hitInstance = new()
        {
            Source = hero.gameObject,
            AttackType = attackType,
            CircleDirection = false,
            DamageDealt = damage,
            Direction = GetHitDirection(hero.transform, target.transform),
            IgnoreInvulnerable = true,
            MagnitudeMultiplier = 0f,
            MoveAngle = 0f,
            MoveDirection = false,
            Multiplier = 1f,
            SpecialType = SpecialTypes.None,
            IsExtraDamage = false
        };

        target.Hit(hitInstance);
        return target.isDead || (!wasDead && target.hp < previousHp);
    }

    public Vector3 GetLightningImpactVisualPosition(HealthManager target)
    {
        Vector3 anchor = GetOrbImpactAnchorPosition(target);
        Vector2 offset = GetScaledImpactOffset(target, 0f, 1.12f, 0.9f, 1.45f);
        return anchor + new Vector3(offset.x, offset.y, 0f);
    }

    public Vector3 GetLightningVisualPosition(HealthManager target)
    {
        return GetLightningImpactVisualPosition(target);
    }

    public Vector3 GetGlassHitVisualPosition(HealthManager target)
    {
        return GetWhiteImpactVisualPosition(target);
    }

    public Vector3 GetVoidImpactVisualPosition(HealthManager target)
    {
        Vector3 anchor = GetOrbImpactAnchorPosition(target);
        Vector2 offset = GetScaledImpactOffset(target, 0.54f, 0.74f, 0.38f, 0.88f);
        return anchor + new Vector3(offset.x, offset.y, 0f);
    }

    public Vector3 GetOrbImpactAnchorPosition(HealthManager target)
    {
        float verticalOffset = 0.8f;
        Collider2D collider = target.GetComponent<Collider2D>();
        if (collider != null)
        {
            verticalOffset = Mathf.Clamp(collider.bounds.extents.y * 0.42f, 0.65f, 1.2f);
        }

        return target.transform.position + new Vector3(0f, verticalOffset, 0f);
    }

    public Vector3 GetWhiteImpactVisualPosition(HealthManager target)
    {
        Vector3 anchor = GetOrbImpactAnchorPosition(target);
        Vector2 offset = GetScaledImpactOffset(target, -0.54f, 0.74f, 0.38f, 0.88f);
        return anchor + new Vector3(offset.x, offset.y, 0f);
    }

    public static bool IsEnemyCollider(Collider2D collider)
    {
        if (collider == null)
        {
            return false;
        }

        HealthManager healthManager = collider.GetComponentInParent<HealthManager>();
        if (healthManager == null || healthManager.isDead)
        {
            return false;
        }

        HeroController hero = collider.GetComponentInParent<HeroController>();
        return hero == null;
    }

    public static int GetCeilThirdDamage(int baseDamage)
    {
        return Mathf.CeilToInt(Math.Max(1, baseDamage) / 3f);
    }

    public static int GetCeilHalfDamage(int baseDamage)
    {
        return Mathf.CeilToInt(Math.Max(1, baseDamage) / 2f);
    }

    private static float GetHitDirection(Transform heroTransform, Transform enemyTransform)
    {
        Vector2 delta = enemyTransform.position - heroTransform.position;
        return Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
    }

    private static Vector2 GetScaledImpactOffset(HealthManager target, float baseX, float baseY, float minScale, float maxScale)
    {
        float scale = 1f;
        Collider2D collider = target.GetComponent<Collider2D>();
        if (collider != null)
        {
            float sizeBasis = Mathf.Max(collider.bounds.size.x, collider.bounds.size.y);
            scale = Mathf.Clamp(sizeBasis * 0.45f, minScale, maxScale);
        }

        return new Vector2(baseX * scale, baseY * scale);
    }

    private static void DrawEnemySearchBounds(Vector3 heroPosition)
    {
        EnsureEnemySearchDebugRenderer();
        UpdateEnemySearchDebugRenderer(heroPosition);
        _enemySearchDebugVisibleUntil = Time.time + EnemySearchDebugDuration;
    }

    private static void EnsureEnemySearchDebugRenderer()
    {
        if (_enemySearchDebugRenderer != null && _enemySearchDebugRoot != null)
        {
            return;
        }

        GameObject root = new("DeVect_EnemySearchDebug");
        UnityEngine.Object.DontDestroyOnLoad(root);

        LineRenderer renderer = root.AddComponent<LineRenderer>();
        renderer.enabled = false;
        renderer.loop = false;
        renderer.useWorldSpace = true;
        renderer.positionCount = 5;
        renderer.startWidth = 0.08f;
        renderer.endWidth = 0.08f;
        renderer.numCapVertices = 2;
        renderer.numCornerVertices = 2;
        renderer.sortingLayerName = "HUD";
        renderer.sortingOrder = 50;
        renderer.material = new Material(Shader.Find("Sprites/Default"));
        renderer.startColor = EnemySearchDebugColor;
        renderer.endColor = EnemySearchDebugColor;

        _enemySearchDebugRoot = root;
        _enemySearchDebugRenderer = renderer;
    }

    private static void UpdateEnemySearchDebugRenderer(Vector3 heroPosition)
    {
        if (_enemySearchDebugRenderer == null)
        {
            return;
        }

        Vector3 topLeft = heroPosition + new Vector3(-EnemySearchLeftRange, EnemySearchUpRange, 0f);
        Vector3 topRight = heroPosition + new Vector3(EnemySearchRightRange, EnemySearchUpRange, 0f);
        Vector3 bottomRight = heroPosition + new Vector3(EnemySearchRightRange, -EnemySearchDownRange, 0f);
        Vector3 bottomLeft = heroPosition + new Vector3(-EnemySearchLeftRange, -EnemySearchDownRange, 0f);

        _enemySearchDebugRenderer.enabled = true;
        _enemySearchDebugRenderer.SetPosition(0, topLeft);
        _enemySearchDebugRenderer.SetPosition(1, topRight);
        _enemySearchDebugRenderer.SetPosition(2, bottomRight);
        _enemySearchDebugRenderer.SetPosition(3, bottomLeft);
        _enemySearchDebugRenderer.SetPosition(4, topLeft);
    }

    private static void TickEnemySearchDebugRenderer()
    {
        if (_enemySearchDebugRenderer == null)
        {
            return;
        }

        if (Time.time > _enemySearchDebugVisibleUntil)
        {
            _enemySearchDebugRenderer.enabled = false;
        }
    }

    private static bool IsTargetWithinStrictRange(HeroController hero, HealthManager target)
    {
        Vector3 offset = target.transform.position - hero.transform.position;
        return offset.x >= -EnemySearchLeftRange
            && offset.x <= EnemySearchRightRange
            && offset.y >= -EnemySearchDownRange
            && offset.y <= EnemySearchUpRange;
    }
}
