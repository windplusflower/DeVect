using UnityEngine;

namespace DeVect.Visual;

internal sealed class TransientVisual
{
    public TransientVisual(GameObject root, SpriteRenderer renderer, float initialLifetime, Vector3 velocity, Color baseColor, Vector3 baseScale)
    {
        Root = root;
        Renderer = renderer;
        InitialLifetime = initialLifetime;
        LifetimeRemaining = initialLifetime;
        Velocity = velocity;
        BaseColor = baseColor;
        BaseScale = baseScale;
        StartPosition = root.transform.position;
        EndPosition = root.transform.position;
        ArcCenter = root.transform.position;
    }

    public GameObject Root { get; }

    public SpriteRenderer Renderer { get; }

    public float InitialLifetime { get; }

    public float LifetimeRemaining { get; set; }

    public Vector3 Velocity { get; set; }

    public Color BaseColor { get; }

    public Vector3 BaseScale { get; }

    public bool UseArcMotion { get; set; }

    public Vector3 StartPosition { get; set; }

    public Vector3 EndPosition { get; set; }

    public float ArcHeight { get; set; }

    public Vector3 ArcCenter { get; set; }

    public float ArcRadius { get; set; }

    public float StartAngleDeg { get; set; }

    public float EndAngleDeg { get; set; }
}
