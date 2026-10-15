using UnityEngine;

[RequireComponent(typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler))]
[DefaultExecutionOrder(-100)]
public class PixelArtCanvasScale : MonoBehaviour
{
    [SerializeField] private Vector2Int referenceResolution = new Vector2Int(585, 270);
    private UnityEngine.UI.CanvasScaler scaler;

    private void Awake()
    {
        scaler = GetComponent<UnityEngine.UI.CanvasScaler>();
    }

    private void Update()
    {
        if (scaler.uiScaleMode != UnityEngine.UI.CanvasScaler.ScaleMode.ConstantPixelSize) return;
        float fit = Mathf.Min(Screen.width / (float)Mathf.Max(1, referenceResolution.x),
            Screen.height / (float)Mathf.Max(1, referenceResolution.y));
        scaler.scaleFactor = Mathf.Max(1f, Mathf.Floor(fit));
    }
}
