using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Listens to PlayerHealth events and renders health as a row of pixel-art hearts.
/// Assign fullHeartSprite and emptyHeartSprite in the Inspector (or via the Setup tool).
/// The Setup tool (Tools > Setup Pixel Art Health Bar) creates the full hierarchy automatically.
/// </summary>
public class HeartHealthUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private Transform heartsContainer;

    [Header("Sprites")]
    [SerializeField] private Sprite fullHeartSprite;
    [SerializeField] private Sprite emptyHeartSprite;

    [Header("Layout")]
    [SerializeField] private Vector2 heartSize = new Vector2(32f, 32f);

    private readonly List<Image> _heartImages = new List<Image>();

    public PlayerHealth PlayerHealth
    {
        get => playerHealth;
        set => playerHealth = value;
    }

    public Transform HeartsContainer
    {
        get => heartsContainer;
        set => heartsContainer = value;
    }

    public Sprite FullHeartSprite
    {
        get => fullHeartSprite;
        set => fullHeartSprite = value;
    }

    public Sprite EmptyHeartSprite
    {
        get => emptyHeartSprite;
        set => emptyHeartSprite = value;
    }

    private void Awake()
    {
        // Auto-initialize when references are wired in the Inspector or by the Setup tool.
        if (playerHealth != null && heartsContainer != null)
            Initialize();
    }

    private void OnEnable()
    {
        if (playerHealth != null)
            Subscribe();
    }

    private void OnDisable()
    {
        if (playerHealth != null)
            Unsubscribe();
    }

    /// <summary>
    /// Called by the Setup tool (or manually) after both playerHealth and
    /// heartsContainer are assigned, so the UI is ready before the first frame.
    /// </summary>
    public void Initialize()
    {
        if (playerHealth == null)
        {
            Debug.LogError("[HeartHealthUI] PlayerHealth reference is missing.", this);
            return;
        }

        Subscribe();
        BuildHearts(playerHealth.MaxHealth);
        Refresh(playerHealth.CurrentHealth, playerHealth.MaxHealth);
    }

    private void Subscribe()
    {
        playerHealth.OnHealthChanged -= HandleHealthChanged;
        playerHealth.OnHealthChanged += HandleHealthChanged;
    }

    private void Unsubscribe()
    {
        playerHealth.OnHealthChanged -= HandleHealthChanged;
    }

    private void HandleHealthChanged(int current, int max)
    {
        if (_heartImages.Count != max)
            BuildHearts(max);

        Refresh(current, max);
    }

    // ── Internal helpers ────────────────────────────────────────────────────

    private void BuildHearts(int count)
    {
        // Destroy old hearts
        foreach (var img in _heartImages)
        {
            if (img != null)
                Destroy(img.gameObject);
        }
        _heartImages.Clear();

        if (heartsContainer == null)
        {
            Debug.LogError("[HeartHealthUI] HeartsContainer is not assigned.", this);
            return;
        }

        for (int i = 0; i < count; i++)
        {
            var go = new GameObject($"Heart_{i + 1}", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(heartsContainer, false);

            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = heartSize;

            var img = go.GetComponent<Image>();
            img.sprite = fullHeartSprite;
            img.preserveAspect = true;

            _heartImages.Add(img);
        }
    }

    private void Refresh(int current, int max)
    {
        for (int i = 0; i < _heartImages.Count; i++)
        {
            if (_heartImages[i] == null) continue;
            _heartImages[i].sprite = i < current ? fullHeartSprite : emptyHeartSprite;
        }
    }
}
