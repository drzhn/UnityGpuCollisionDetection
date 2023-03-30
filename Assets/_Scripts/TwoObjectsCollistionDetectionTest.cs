using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

public class TwoObjectsCollistionDetectionTest : MonoBehaviour
{
    struct ObjData
    {
        public Vector3 position;
        public Vector3 velocity;
    }

    private List<ObjData> _data = new List<ObjData>();

    private const int NumObjects = 150;
    private const float Diameter = 1;

    private void Awake()
    {
        for (int i = 0; i < NumObjects; i++)
        {
            _data.Add(new ObjData()
            {
                position = new Vector3(Random.Range(-5f, 5f), Random.Range(-5f, 5f), Random.Range(-5f, 5f)),
                velocity = Vector3.zero
            });
        }
    }

    void FixedUpdate()
    {
        UpdateObjectsPositions();
        CalculateCollisionDetection();
    }

    void UpdateObjectsPositions()
    {
        for (int i = 0; i < NumObjects; i++)
        {
            var data = _data[i];
            data.velocity += 10 * Time.fixedDeltaTime * (-1 * data.position).normalized;
            data.velocity *= 0.95f;

            data.position += data.velocity * Time.fixedDeltaTime;
            _data[i] = data;
        }
    }

    void CalculateCollisionDetection()
    {
        for (int i = 0; i < NumObjects - 1; i++)
        {
            for (int j = i + 1; j < NumObjects; j++)
            {
                var data1 = _data[i];
                var data2 = _data[j];
                float currentDistance = Vector3.Distance(data1.position, data2.position);
                const float desiredDistance = Diameter;
                if (currentDistance < desiredDistance)
                {
                    // float3 dir = normalize(objectPos - collisionPos) * (desiredDistance-currentDistance)/2;
                    // // float k =  0.5;
                    // // _positionBuffer[index] -= k * 0.95 * dir;
                    // _positionBuffer[index] += dir*0.8;
                    // _positionBuffer[id2] -= dir*0.8;

                    Vector3 dir = (data1.position - data2.position) * (currentDistance - desiredDistance) / currentDistance;
                    float k = 0.5f; //_pointsDataBuffer[index].mass / (_pointsDataBuffer[index].mass + _pointsDataBuffer[j].mass);
                    data1.position -= k * 0.5f * dir;
                    data2.position += k * 0.5f * dir;

                    _data[i] = data1;
                    _data[j] = data2;
                }
            }
        }
    }

    private void OnDrawGizmos()
    {
        foreach (var data in _data)
        {
            Gizmos.DrawWireSphere(data.position, Diameter / 2);
        }
    }
}