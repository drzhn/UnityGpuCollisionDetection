using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class FPSCounter : MonoBehaviour
{
    private const int _amount = 20;
    private const float _fpsUpdateFrequency = 0.2f;
    private readonly float[] _fpsArray = new float[_amount];
    private float _currentFps = 0;
    private float _currentDeltaTime = 0;
    private float _lastTimeFpsUpdated = 0;
    void Update()
    {
        for (int i = 0; i < _amount-1; i++)
        {
            _fpsArray[i] = _fpsArray[i + 1];
        }

        _fpsArray[_amount - 1] = 1 / Time.unscaledDeltaTime;
        if (Time.time > _lastTimeFpsUpdated + _fpsUpdateFrequency)
        {
            _currentFps = _fpsArray.Average();
            _currentDeltaTime = Time.deltaTime;
            _lastTimeFpsUpdated += _fpsUpdateFrequency;
        }
    }

    private void OnGUI()
    {
        GUILayout.Label(string.Format("FPS: {0:0.0}", _currentFps));
        GUILayout.Label(string.Format("DeltaTime: {0:0.000000}", _currentDeltaTime));
    }
}
