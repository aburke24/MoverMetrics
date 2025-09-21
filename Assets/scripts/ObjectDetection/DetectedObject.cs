using System;
using UnityEngine;

[System.Serializable]
public class DetectedObject
{
    public Guid ID;
    public string Name;             // Semantic label (from Lightship)
    public float Confidence;        // Semantic confidence
    public Rect LastBoundingBox;    // 2D box (for UI only)
    public Bounds MeshBounds;       // 3D bounds (from LiDAR mesh/depth)
    public Vector3 WorldPosition;   // Center in world space
    public int TimesSeen;
    public float VolumeFeet3 { get; private set; }
    public float LastSeenTime { get; private set; }
    public bool IsActive { get; private set; }

    public DetectedObject(string name, float confidence, Rect boundingBox, Bounds meshBounds)
    {
        ID = Guid.NewGuid();
        Name = name;
        Confidence = confidence;
        LastBoundingBox = boundingBox;
        MeshBounds = meshBounds;
        WorldPosition = meshBounds.center;
        TimesSeen = 1;
        VolumeFeet3 = CalculateVolume(meshBounds);
        LastSeenTime = Time.time;
        IsActive = true;
    }

    public void Update(string name, float confidence, Rect boundingBox, Bounds meshBounds)
    {
        Name = name;
        Confidence = Mathf.Max(Confidence, confidence);
        LastBoundingBox = boundingBox;
        MeshBounds = meshBounds;
        WorldPosition = meshBounds.center;
        VolumeFeet3 = CalculateVolume(meshBounds);
        TimesSeen++;
        LastSeenTime = Time.time;
        IsActive = true;
    }

    private float CalculateVolume(Bounds meshBounds)
    {
        float volumeMeters3 = meshBounds.size.x * meshBounds.size.y * meshBounds.size.z;
        return volumeMeters3 * 35.3147f; // Convert to cubic feet
    }

    public void MarkInactiveIfExpired(float persistenceTime)
    {
        if (Time.time - LastSeenTime > persistenceTime)
            IsActive = false;
    }
}
