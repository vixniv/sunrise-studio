using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Drives a Sprite Shader Graph's backlight and rim properties according to
/// how much each character collider overlaps the window-light zones.
/// </summary>
public class WindowBacklightReceiver2D : MonoBehaviour
{
    [Serializable]
    private class CharacterEntry
    {
        [Tooltip("Collider whose area controls this character's backlight.")]
        public Collider2D characterCollider;

        [Tooltip("Only this character's Sprite Renderers. Leave empty to find renderers below the collider object.")]
        public List<SpriteRenderer> spriteRenderers = new List<SpriteRenderer>();

        [NonSerialized] public List<Collider2D> overlappingZones = new List<Collider2D>();
        [NonSerialized] public float currentCoverage = -1f;
    }

    [Tooltip("Assign the layer used by the window-light zone colliders.")]
    [SerializeField] private LayerMask windowLightZoneLayers;

    [Header("Multiple characters")]
    [Tooltip("Add one entry per character under this object. Each collider controls only its listed Sprite Renderers. When this list is not empty, the single-character fields below are ignored.")]
    [SerializeField] private List<CharacterEntry> characters = new List<CharacterEntry>();

    [Header("Single character (used when Multiple Characters is empty)")]
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
    private MaterialPropertyBlock propertyBlock;
    private ContactFilter2D zoneFilter;

    private void Awake()
    {
        if (characters == null)
            characters = new List<CharacterEntry>();

        if (characters.Count == 0)
            CreateSingleCharacterEntry();
        else
            PrepareCharacterEntries();

        propertyBlock = new MaterialPropertyBlock();
        zoneFilter = new ContactFilter2D();
        zoneFilter.SetLayerMask(windowLightZoneLayers);
        zoneFilter.useTriggers = true;

        foreach (CharacterEntry character in characters)
        {
            if (character == null)
                continue;

            character.currentCoverage = -1f;
            ApplyBacklight(character, 0f);
        }
    }

    private void CreateSingleCharacterEntry()
    {
        if (characterCollider == null)
            characterCollider = GetComponent<Collider2D>();

        if (spriteRenderers == null)
            spriteRenderers = new List<SpriteRenderer>();

        if (spriteRenderers.Count == 0)
            spriteRenderers.AddRange(GetComponentsInChildren<SpriteRenderer>(true));

        characters.Add(new CharacterEntry
        {
            characterCollider = characterCollider,
            spriteRenderers = spriteRenderers
        });
    }

    private void PrepareCharacterEntries()
    {
        foreach (CharacterEntry character in characters)
        {
            if (character == null)
                continue;

            if (character.spriteRenderers == null)
                character.spriteRenderers = new List<SpriteRenderer>();

            if (character.characterCollider == null)
            {
                Debug.LogWarning($"{name}: a character entry has no Collider2D assigned.", this);
                continue;
            }

            if (character.spriteRenderers.Count == 0)
                character.spriteRenderers.AddRange(character.characterCollider.GetComponentsInChildren<SpriteRenderer>(true));
        }
    }

    private void LateUpdate()
    {
        foreach (CharacterEntry character in characters)
        {
            if (character == null)
                continue;

            float coverage = 0f;
            Collider2D collider = character.characterCollider;
            if (collider != null && collider.enabled && collider.gameObject.activeInHierarchy)
            {
                character.overlappingZones.Clear();
                collider.Overlap(zoneFilter, character.overlappingZones);

                if (character.overlappingZones.Count > 0)
                    coverage = CalculateCoverage(character);
            }

            if (!Mathf.Approximately(coverage, character.currentCoverage))
            {
                character.currentCoverage = coverage;
                ApplyBacklight(character, coverage);
            }
        }
    }

    private void OnDisable()
    {
        foreach (CharacterEntry character in characters)
        {
            if (character == null)
                continue;

            character.overlappingZones.Clear();
            character.currentCoverage = 0f;
            ApplyBacklight(character, 0f);
        }
    }

    private float CalculateCoverage(CharacterEntry character)
    {
        Collider2D collider = character.characterCollider;
        if (collider == null || !collider.enabled)
            return 0f;

        Bounds bounds = collider.bounds;
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

                if (!collider.OverlapPoint(point))
                    continue;

                samplesInsideCharacter++;
                if (IsPointInsideAnyZone(character, point))
                    samplesInsideLight++;
            }
        }

        return samplesInsideCharacter == 0
            ? 0f
            : (float)samplesInsideLight / samplesInsideCharacter;
    }

    private static bool IsPointInsideAnyZone(CharacterEntry character, Vector2 point)
    {
        foreach (Collider2D zone in character.overlappingZones)
        {
            if (zone != null && zone.enabled && zone.gameObject.activeInHierarchy && zone.OverlapPoint(point))
                return true;
        }

        return false;
    }

    private void ApplyBacklight(CharacterEntry character, float coverage)
    {
        float backlightStrength = Mathf.Lerp(backlightStrengthAtNoCoverage, backlightStrengthAtFullCoverage, coverage);
        float rimIntensity = Mathf.Lerp(rimIntensityAtNoCoverage, rimIntensityAtFullCoverage, coverage);

        foreach (SpriteRenderer spriteRenderer in character.spriteRenderers)
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
