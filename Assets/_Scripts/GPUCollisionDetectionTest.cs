using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public class GPUCollisionDetectionTest : MonoBehaviour
{
    [SerializeField] private ShaderContainer _shaderContainer;
    [SerializeField] private Material _instancedMaterial;
    [SerializeField] private Mesh _mesh;

    private const int NumPerSide = 30;
    private const int NumObjects = NumPerSide * NumPerSide * NumPerSide;

    private ComputeBuffer _objectMatricesBuffer;
    private ComputeBuffer _argsBuffer;

    private void Awake()
    {
        int[] args = new int[5] { 0, 0, 0, 0, 0 };
        _argsBuffer = new ComputeBuffer(1, args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
        args[0] = (int)_mesh.GetIndexCount(0);
        args[1] = NumObjects;
        args[2] = (int)_mesh.GetIndexStart(0);
        args[3] = (int)_mesh.GetBaseVertex(0);

        _argsBuffer.SetData(args);

        _objectMatricesBuffer = new ComputeBuffer(NumObjects, 64);
        Matrix4x4[] data = new Matrix4x4[NumObjects];
        for (int i = 0; i < NumPerSide; i++)
        {
            for (int j = 0; j < NumPerSide; j++)
            {
                for (int k = 0; k < NumPerSide; k++)
                {
                    data[i * NumPerSide * NumPerSide + j * NumPerSide + k] =
                        Matrix4x4.TRS(
                            new Vector3(i, j, k) * 3,
                            Quaternion.identity,
                            Vector3.one
                        );
                }
            }
        }

        _objectMatricesBuffer.SetData(data);
        _instancedMaterial.SetBuffer("objectMatrices", _objectMatricesBuffer);
    }

    private void Update()
    {
        Graphics.DrawMeshInstancedIndirect(
            _mesh,
            0,
            _instancedMaterial,
            new Bounds(Vector3.zero, Vector3.one * 1000),
            _argsBuffer
        );
    }

    private void OnDestroy()
    {
        _objectMatricesBuffer.Release();
        _argsBuffer.Release();
    }
}