using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class DrawRect : MonoBehaviour
{
    [SerializeField] private GameObject _rectanglePrefab;
    [SerializeField] private float _rectPersistenceTime = 2f; // Time to keep rectangles visible (seconds)

    private List<UIRectObject> _rectObjects = new();
    private List<int> _openIndices = new();
    private List<float> _rectTimers = new(); // Track time each rectangle has been active

    private void Awake()
    {
        _rectTimers = new List<float>();
    }

    private void Update()
    {
        // Update timers for active rectangles
        for (int i = 0; i < _rectObjects.Count; i++)
        {
            if (_rectObjects[i].gameObject.activeSelf)
            {
                _rectTimers[i] += Time.deltaTime;
                if (_rectTimers[i] >= _rectPersistenceTime)
                {
                    _rectObjects[i].gameObject.SetActive(false);
                    if (!_openIndices.Contains(i))
                    {
                        _openIndices.Add(i);
                    }
                }
            }
        }
    }

    public void CreateRect(Rect rect, Color color, string text)
    {
        if (_openIndices.Count == 0)
        {
            var newRect = Instantiate(_rectanglePrefab, parent: transform).GetComponent<UIRectObject>();
            _rectObjects.Add(newRect);
            _rectTimers.Add(0f); // Initialize timer
            _openIndices.Add(_rectObjects.Count - 1);
        }

        int index = _openIndices[0];
        _openIndices.RemoveAt(0);

        UIRectObject rectObject = _rectObjects[index];
        rectObject.SetRectTransform(rect);
        rectObject.SetColor(color);
        rectObject.SetText(text);
        rectObject.gameObject.SetActive(true);
        _rectTimers[index] = 0f; // Reset timer
    }

    public void ClearRects()
    {
        for (int i = 0; i < _rectObjects.Count; i++)
        {
            if (!_openIndices.Contains(i))
            {
                _openIndices.Add(i);
            }
        }
    }
}