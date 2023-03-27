using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class _debugSphericalFibonacciDrawer : MonoBehaviour
{
    [Range(0, 1000)]
    public uint N = 1;
    // Start is called before the first frame update
    void Start()
    {
    }

    float madfrac(float A, float B)
    {
        return ((A) * (B) - Mathf.Floor((A) * (B)));
    }

    Vector3 sphericalFibonacci(float i, float n)
    {
        float PHI = Mathf.Sqrt(5) * 0.5f + 0.5f;
        float phi = 2.0f * Mathf.PI * madfrac(i, PHI - 1);
        float cosTheta = 1.0f - (2.0f * i + 1.0f) * (1.0f / n);
        float sinTheta = Mathf.Sqrt(Mathf.Clamp01(1.0f - cosTheta * cosTheta));

        return new Vector3(
            Mathf.Cos(phi) * sinTheta,
            Mathf.Sin(phi) * sinTheta,
            cosTheta);
    }

    // Update is called once per frame
    void Update()
    {
        for (float i = 0.0f; i < N; i += 1.0f)
        {
            Debug.DrawLine(transform.position, transform.position + sphericalFibonacci(i, N));
        }
    }
}