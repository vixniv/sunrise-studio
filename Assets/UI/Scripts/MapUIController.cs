using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace SunriseStudio.UI
{
    [Serializable]
    public sealed class MapMarkerData
    {
        [SerializeField] string id;
        [SerializeField] string displayName;
        [SerializeField] Sprite icon;
        [SerializeField] Vector2 normalizedPosition = new(0.5f, 0.5f);

        public string Id => id;
        public string DisplayName => displayName;
        public Sprite Icon => icon;
        public Vector2 NormalizedPosition => normalizedPosition;
    }

    public sealed class MapUIController : MonoBehaviour
    {
        [Header("Map")]
        [SerializeField] GameObject mapRoot;
        [SerializeField] RectTransform questMarkersRoot;
        [SerializeField] MapMarkerView questMarkerPrefab;
        [SerializeField] MapMarkerView playerMarker;

        [Header("Room Information")]
        [SerializeField] GameObject roomInfoPanel;
        [SerializeField] TMP_Text roomNameLabel;
        [SerializeField] TMP_Text roomDescriptionLabel;
        [SerializeField] Button closeButton;

        [Header("Events")]
        [SerializeField] StringEvent onMarkerSelected = new();
        [SerializeField] StringEvent onRoomSelected = new();
        [SerializeField] UnityEvent onCloseRequested = new();

        readonly List<MapMarkerView> questMarkers = new();

        void OnEnable()
        {
            if (closeButton != null)
                closeButton.onClick.AddListener(Close);
        }

        void OnDisable()
        {
            if (closeButton != null)
                closeButton.onClick.RemoveListener(Close);
        }

        public void Open()
        {
            if (mapRoot != null)
                mapRoot.SetActive(true);
        }

        public void Close()
        {
            if (mapRoot != null)
                mapRoot.SetActive(false);

            onCloseRequested.Invoke();
        }

        public void SetPlayerPosition(Vector2 normalizedPosition)
        {
            if (playerMarker != null)
                playerMarker.SetNormalizedPosition(normalizedPosition);
        }

        public void SetQuestMarkers(IEnumerable<MapMarkerData> markers)
        {
            ClearQuestMarkers();

            if (markers == null || questMarkersRoot == null || questMarkerPrefab == null)
                return;

            foreach (MapMarkerData marker in markers)
            {
                MapMarkerView view = Instantiate(questMarkerPrefab, questMarkersRoot);
                view.Bind(
                    marker.Id,
                    marker.DisplayName,
                    marker.Icon,
                    marker.NormalizedPosition,
                    onMarkerSelected);
                questMarkers.Add(view);
            }
        }

        public void SelectRoom(string roomId)
        {
            onRoomSelected.Invoke(roomId);
        }

        public void ShowRoomInfo(string roomName, string description)
        {
            if (roomInfoPanel != null)
                roomInfoPanel.SetActive(true);
            if (roomNameLabel != null)
                roomNameLabel.text = roomName;
            if (roomDescriptionLabel != null)
                roomDescriptionLabel.text = description;
        }

        public void HideRoomInfo()
        {
            if (roomInfoPanel != null)
                roomInfoPanel.SetActive(false);
        }

        void ClearQuestMarkers()
        {
            foreach (MapMarkerView marker in questMarkers)
            {
                if (marker != null)
                {
                    marker.gameObject.SetActive(false);
                    Destroy(marker.gameObject);
                }
            }

            questMarkers.Clear();
        }
    }
}
