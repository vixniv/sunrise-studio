using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Drives a Sprite Shader Graph's backlight and rim properties based on how much
/// of this object's collider overlaps one or more window-light trigger zones.
/// </summary>
public class WindowBacklightReceiver2D : MonoBehaviour
{
    [Tooltip("Assign the layer used by the window-light trigger colliders.")]
    [SerializeField] private LayerMask windowLightZoneLayers;
    [Tooltip("Main body collider whose covered area controls the effect. If empty, uses a Collider2D on this object.")]
    [SerializeField] private Collider2D characterCollider;
    [Tooltip("Leave empty to find all child Sprite Renderers at runtime.")]
    [SerializeField] private List<SpriteRenderer> spriteRenderers = new List<SpriteRenderer>();
    [Header("Shader values at no coverage")]
    [SerializeField, Min(0f)] private float backlightStrengthAtNoCoverage = 1f;
    [SerializeField, Min(0f)] private float rimIntensityAtNoCoverage = 0f;
    [Header("Shader values at full coverage")]
    [SerializeField, Min(0f)] private float backlightStrengthAtFullCoverage = 1.8f;
    [SerializeField, Min(0f)] private float rimIntensityAtFullCoverage = 0.75f;
    [Tooltip("Increase these for a more precise coverage estimate. Sampling runs only while inside a light zone.")]
    [SerializeField, Min(2)] private int coverageSamplesX = 8;
    [SerializeField, Min(2)] private int coverageSamplesY = 16;

    private static readonly int BacklightStrengthId = Shader.PropertyToID("_BacklightStrength");
    private static readonly int RimIntensityId = Shader.PropertyToID("_RimIntensity");
    private readonly HashSet<Collider2D> overlappingZones = new HashSet<Collider2D>();
    private readonly List<Collider2D> inactiveZones = new List<Collider2D>();
    private MaterialPropertyBlock propertyBlock;
    private float currentCoverage = -1f;

    private void Awake()
    {
        if (characterCollider == null)
            characterCollider = GetComponent<Collider2D>();

        if (spriteRenderers.Count == 0)
            spriteRenderers.AddRange(GetComponentsInChildren<SpriteRenderer>(true));

        propertyBlock = new MaterialPropertyBlock();
        ApplyBacklight(0f);

        if (characterCollider == null)
            Debug.LogWarning($"{name}: no character Collider2D assigned or found. Backlight coverage will remain at 0.", this);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (IsWindowLightZone(other))
            overlappingZones.Add(other);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        overlappingZones.Remove(other);
    }

    private void LateUpdate()
    {
        inactiveZones.Clear();
        foreach (Collider2D zone in overlappingZones)
        {
            if (zone == null || !zone.enabled || !zone.gameObject.activeInHierarchy)
                inactiveZones.Add(zone);
        }

        foreach (Collider2D zone in inactiveZones)
            overlappingZones.Remove(zone);

        float coverage = overlappingZones.Count == 0 ? 0f : CalculateCoverage();
        if (!Mathf.Approximately(coverage, currentCoverage))
        {
            currentCoverage = coverage;
            ApplyBacklight(currentCoverage);
        }
    }

    private void OnDisable()
    {
        overlappingZones.Clear();
        if (propertyBlock != null)
        {
            currentCoverage = 0f;
            ApplyBacklight(0f);
        }
    }

    private bool IsWindowLightZone(Collider2D other)
    {
        return other != null && (windowLightZoneLayers.value & (1 << other.gameObject.layer)) != 0;
    }

    private float CalculateCoverage()
    {
        if (characterCollider == null || !characterCollider.enabled)
            return 0f;

        Bounds bounds = characterCollider.bounds;
        int columns = Mathf.Max(2, coverageSamplesX);
        int rows = Mathf.Max(2, coverageSamplesY);
        int samplesInsideCharacter = 0;
        int samplesInsideLight = 0;

        for (int y = 0; y < rows; y++)
        {
            float worldY = bounds.min.y + (y + 0.5f) * bounds.size.y / rows;

            for (int x = 0; x < columns; x++)
            {
                float worldX = bounds.min.x + (x + 0.5f) * bounds.size.x / columns;
                Vector2 point = new Vector2(worldX, worldY);

                if (!characterCollider.OverlapPoint(point))
                    continue;

                samplesInsideCharacter++;
                if (IsPointInsideAnyZone(point))
                    samplesInsideLight++;
            }
        }

        return samplesInsideCharacter == 0
            ? 0f
            : (float)samplesInsideLight / samplesInsideCharacter;
    }

    private bool IsPointInsideAnyZone(Vector2 point)
    {
        foreach (Collider2D zone in overlappingZones)
        {
            if (zone != null && zone.enabled && zone.gameObject.activeInHierarchy && zone.OverlapPoint(point))
                return true;
        }

        return false;
    }

    private void ApplyBacklight(float coverage)
    {
        float backlightStrength = Mathf.Lerp(backlightStrengthAtNoCoverage, backlightStrengthAtFullCoverage, coverage);
        float rimIntensity = Mathf.Lerp(rimIntensityAtNoCoverage, rimIntensityAtFullCoverage, coverage);

        foreach (SpriteRenderer spriteRenderer in spriteRenderers)
        {
            if (spriteRenderer == null)
                continue;

            spriteRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetFloat(BacklightStrengthId, backlightStrength);
            propertyBlock.SetFloat(RimIntensityId, rimIntensity);
            spriteRenderer.SetPropertyBlock(propertyBlock);
        }
    }
}
