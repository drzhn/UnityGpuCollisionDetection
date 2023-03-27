using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Random = UnityEngine.Random;

public class GPUCollisionDetectionTest : MonoBehaviour
{
    [SerializeField] private ShaderContainer _shaderContainer;
    [SerializeField] private Material _instancedMaterial;
    [SerializeField] private Mesh _mesh;

    private const int NumPerSide = 32;
    private const int NumObjects = NumPerSide * NumPerSide * NumPerSide;

    private ComputeBuffer _argsBuffer;

    private ComputeBuffer _positionBuffer;
    private ComputeBuffer _velocityBuffer;

    private ComputeShader _physicsIntegrationShader;
    private int _physicsIntegrationKernel;

    private void Awake()
    {
        if (NumObjects != Constants.THREADS_PER_BLOCK * Constants.BLOCK_SIZE)
        {
            throw new Exception();
        }

        int[] args = new int[5] { 0, 0, 0, 0, 0 };
        _argsBuffer = new ComputeBuffer(1, args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
        args[0] = (int)_mesh.GetIndexCount(0);
        args[1] = NumObjects;
        args[2] = (int)_mesh.GetIndexStart(0);
        args[3] = (int)_mesh.GetBaseVertex(0);

        _argsBuffer.SetData(args);

        _positionBuffer = new ComputeBuffer(NumObjects, Marshal.SizeOf<Vector3>());
        _velocityBuffer = new ComputeBuffer(NumObjects, Marshal.SizeOf<Vector3>());
        Vector3[] data = new Vector3[NumObjects];
        for (int i = 0; i < NumPerSide; i++)
        {
            for (int j = 0; j < NumPerSide; j++)
            {
                for (int k = 0; k < NumPerSide; k++)
                {
                    data[i * NumPerSide * NumPerSide + j * NumPerSide + k] = new Vector3(i, j, k) * 3;
                }
            }
        }

        _positionBuffer.SetData(data);

        for (int i = 0; i < NumPerSide; i++)
        {
            for (int j = 0; j < NumPerSide; j++)
            {
                for (int k = 0; k < NumPerSide; k++)
                {
                    data[i * NumPerSide * NumPerSide + j * NumPerSide + k] = Vector3.zero;
                }
            }
        }

        _velocityBuffer.SetData(data);

        _instancedMaterial.SetBuffer("_positionBuffer", _positionBuffer);

        _physicsIntegrationShader = _shaderContainer.Physics.PhysicsIntegrationShader;
        _physicsIntegrationKernel = _physicsIntegrationShader.FindKernel("PhysicsIntegration");
        _physicsIntegrationShader.SetBuffer(_physicsIntegrationKernel, "_positionBuffer", _positionBuffer);
        _physicsIntegrationShader.SetBuffer(_physicsIntegrationKernel, "_velocityBuffer", _velocityBuffer);
    }

    private void Update()
    {
        _physicsIntegrationShader.SetFloat("_velocityDamping", 1);
        _physicsIntegrationShader.SetFloat("_deltaTime", Time.deltaTime);
        _physicsIntegrationShader.Dispatch(_physicsIntegrationKernel, Constants.BLOCK_SIZE, 1, 1);

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
        _positionBuffer.Release();
        _velocityBuffer.Release();
        _argsBuffer.Release();
    }
}