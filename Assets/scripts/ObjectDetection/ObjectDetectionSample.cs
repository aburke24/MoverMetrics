using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using Niantic.Lightship.AR.ObjectDetection;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;

public class ARLiDARObjectDetection : MonoBehaviour
{
    [SerializeField] private float _probabilityThreshold = 0.5f;
    [SerializeField] private float _objectPersistenceTime = 2f;
    [SerializeField] private ARObjectDetectionManager _objectDetectionManager;
    [SerializeField] private ARPointCloudManager _pointCloudManager;
    [SerializeField] private DrawRect _drawRect;

    private Dictionary<Guid, DetectedObject> _roomDetectedObjects = new();
    public IReadOnlyDictionary<Guid, DetectedObject> RoomDetectedObjects => _roomDetectedObjects;
    private DetectedObject _currentlyDisplayedObject;

    private Color[] _colors = { Color.red, Color.blue, Color.green, Color.yellow, Color.magenta, Color.cyan, Color.white, Color.black };

    [Header("UI References")]
    [SerializeField] private Button _showListButton;
    [SerializeField] private GameObject _masterListScrollView;
    [SerializeField] private TMP_Text _masterListText;
    [SerializeField] private TMP_Text _showListButtonText;

    private void Start()
    {
        _objectDetectionManager.enabled = true;
        _objectDetectionManager.MetadataInitialized += OnMetadataInitialized;

        if (_showListButton != null)
            _showListButton.onClick.AddListener(OnShowListButtonClicked);
    }

    private void Update()
    {
        foreach (var obj in _roomDetectedObjects.Values)
            obj.MarkInactiveIfExpired(_objectPersistenceTime);

        if (_currentlyDisplayedObject != null && !_currentlyDisplayedObject.IsActive)
        {
            _currentlyDisplayedObject = null;
            _drawRect.ClearRects();
        }
    }

    private void OnDestroy()
    {
        _objectDetectionManager.MetadataInitialized -= OnMetadataInitialized;
        _objectDetectionManager.ObjectDetectionsUpdated -= OnObjectDetectionsUpdated;

        if (_showListButton != null)
            _showListButton.onClick.RemoveListener(OnShowListButtonClicked);
    }

    private void OnMetadataInitialized(ARObjectDetectionModelEventArgs obj)
    {
        _objectDetectionManager.ObjectDetectionsUpdated += OnObjectDetectionsUpdated;
    }

    private void OnObjectDetectionsUpdated(ARObjectDetectionsUpdatedEventArgs obj)
    {
        if (obj.Results == null) return;

        _drawRect.ClearRects();

        foreach (var detection in obj.Results)
        {
            var categorizations = detection.GetConfidentCategorizations(_probabilityThreshold);
            if (categorizations.Count <= 0) continue;

            categorizations.Sort((a, b) => b.Confidence.CompareTo(a.Confidence));
            var categoryToDisplay = categorizations[0];

            string name = categoryToDisplay.CategoryName;
            float confidence = categoryToDisplay.Confidence;

            int h = Screen.height;
            int w = Screen.width;
            var rect = detection.CalculateRect(w, h, Screen.orientation);

            // Expand for safety margin
            rect = ExpandRect(rect, 1.2f);

            // Compute volume from LiDAR points
            Bounds bounds;
            float volumeFt3 = EstimateVolumeUsingPointCloud(rect, out bounds);

            if (volumeFt3 <= 0) continue;

            // Try to match with an existing object by overlap
            DetectedObject detected = _roomDetectedObjects.Values
                .FirstOrDefault(o => o.MeshBounds.Intersects(bounds));

            if (detected == null)
            {
                detected = new DetectedObject(name, confidence, rect, bounds);
                _roomDetectedObjects[detected.ID] = detected;
            }
            else
            {
                detected.Update(name, confidence, rect, bounds);
            }

            // Debug info
            Debug.Log($"{detected.Name} → Volume ≈ {detected.VolumeFeet3:F2} ft³ @ {detected.WorldPosition}");
        }

        // Pick object with highest confidence
        var selected = _roomDetectedObjects.Values
            .Where(o => o.IsActive)
            .OrderByDescending(o => o.Confidence)
            .FirstOrDefault();

        if (selected != null)
        {
            _currentlyDisplayedObject = selected;
            _drawRect.CreateRect(
                selected.LastBoundingBox,
                _colors[0],
                $"{selected.Name}: {selected.Confidence:F2}, {selected.VolumeFeet3:F1} ft³"
            );
        }
    }

    private Rect ExpandRect(Rect rect, float factor)
    {
        return new Rect(
            rect.x - rect.width * (factor - 1f) / 2,
            rect.y - rect.height * (factor - 1f) / 2,
            rect.width * factor,
            rect.height * factor
        );
    }

    private float EstimateVolumeUsingPointCloud(Rect screenRect, out Bounds bounds)
    {
        bounds = new Bounds(Vector3.zero, Vector3.zero);
        if (_pointCloudManager == null) return -1f;

        List<Vector3> objectPoints = new();

        foreach (var cloud in _pointCloudManager.trackables)
        {
            if (cloud.positions == null) continue;

            foreach (var point in cloud.positions)
            {
                Vector3 screenPoint = Camera.main.WorldToScreenPoint(point);
                if (screenRect.Contains(new Vector2(screenPoint.x, screenPoint.y)))
                    objectPoints.Add(point);
            }
        }

        if (objectPoints.Count < 10) return -1f;

        bounds = new Bounds(objectPoints[0], Vector3.zero);
        foreach (var v in objectPoints) bounds.Encapsulate(v);
        bounds.Expand(0.1f);

        float volumeMeters3 = bounds.size.x * bounds.size.y * bounds.size.z;
        return volumeMeters3 * 35.3147f;
    }

    public void ResetRoomDetections()
    {
        _roomDetectedObjects.Clear();
        _currentlyDisplayedObject = null;
        _drawRect.ClearRects();
    }

    private void OnShowListButtonClicked()
    {
        if (_masterListScrollView == null || _masterListText == null || _showListButtonText == null) return;

        bool isActive = _masterListScrollView.activeSelf;
        _masterListScrollView.SetActive(!isActive);

        if (!isActive)
        {
            StringBuilder sb = new();
            sb.AppendLine("📋 Master Detected Objects:");
            foreach (var obj in _roomDetectedObjects.Values)
            {
                string volumeText = obj.VolumeFeet3 > 0 ? $"{obj.VolumeFeet3:F2} ft³" : "N/A";
                sb.AppendLine($"{obj.Name} — {volumeText} (seen {obj.TimesSeen}, best {obj.Confidence:F2})");
            }
            _masterListText.text = sb.ToString();
            _showListButtonText.text = "Hide Items";
        }
        else
        {
            _showListButtonText.text = "Show Items";
        }
    }
}
