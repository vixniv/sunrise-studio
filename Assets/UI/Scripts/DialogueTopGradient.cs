using UnityEngine;
using UnityEngine.UI;

namespace SunriseStudio.UI
{
    /// <summary>Procedural black-to-transparent overlay. No texture or material asset required.</summary>
    public sealed class DialogueTopGradient : MaskableGraphic
    {
        float topOpacity = 0.65f;

        public void Configure(float opacity, float screenHeight)
        {
            raycastTarget = false;
            maskable = false;
            rectTransform.anchorMin = new Vector2(0f, 1f - Mathf.Clamp01(screenHeight));
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
            if (!Mathf.Approximately(topOpacity, opacity))
            {
                topOpacity = Mathf.Clamp01(opacity);
                SetVerticesDirty();
            }
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect area = rectTransform.rect;
            Color32 clear = new Color(0f, 0f, 0f, 0f);
            Color32 black = new Color(0f, 0f, 0f, topOpacity);
            mesh.AddVert(new Vector3(area.xMin, area.yMin), clear, Vector2.zero);
            mesh.AddVert(new Vector3(area.xMin, area.yMax), black, Vector2.up);
            mesh.AddVert(new Vector3(area.xMax, area.yMax), black, Vector2.one);
            mesh.AddVert(new Vector3(area.xMax, area.yMin), clear, Vector2.right);
            mesh.AddTriangle(0, 1, 2);
            mesh.AddTriangle(2, 3, 0);
        }
    }
}
