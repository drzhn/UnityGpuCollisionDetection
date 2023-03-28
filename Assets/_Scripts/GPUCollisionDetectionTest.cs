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

    private const float SphereDiameter = 1;
    private const float CellSize = SphereDiameter * 1.41421356f;

    private ComputeBuffer _argsBuffer;

    private ComputeBuffer _positionBuffer;
    private ComputeBuffer _velocityBuffer;
    private ComputeBuffer _hashBuffer;
    private ComputeBuffer _packedCellTypeControlAndIndexBuffer;
    private ComputeBuffer _changesBuffer;
    private ComputeBuffer _offsetsBuffer;

    private ComputeShader _physicsIntegrationShader;
    private int _physicsIntegrationKernel;

    private ComputeShader _cellIdGenerationShader;
    private int _cellIdGenerationKernel;

    private ComputeShader _offsetsGenerationShader;
    private int _changesGenerationKernel;
    private int _offsetsGenerationKernel;

    private ComputeBufferSorter _sorter;

    private void Awake()
    {
        if (NumObjects != Constants.THREADS_PER_BLOCK * Constants.DATA_BLOCK_SIZE)
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
        _hashBuffer = new ComputeBuffer(NumObjects * 8, Marshal.SizeOf<uint>());
        _changesBuffer = new ComputeBuffer(NumObjects * 8, Marshal.SizeOf<uint>());
        _offsetsBuffer = new ComputeBuffer(NumObjects * 8, Marshal.SizeOf<uint>());
        _packedCellTypeControlAndIndexBuffer = new ComputeBuffer(NumObjects * 8, Marshal.SizeOf<uint>());

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

        _cellIdGenerationShader = _shaderContainer.Physics.CellIdGenerationShader;
        _cellIdGenerationKernel = _cellIdGenerationShader.FindKernel("CellIdGeneration");
        _cellIdGenerationShader.SetFloat("_sphereDiameter", SphereDiameter);
        _cellIdGenerationShader.SetFloat("_cellSize", CellSize);
        _cellIdGenerationShader.SetBuffer(_cellIdGenerationKernel, "_positionBuffer", _positionBuffer);
        _cellIdGenerationShader.SetBuffer(_cellIdGenerationKernel, "_cellHash", _hashBuffer);
        _cellIdGenerationShader.SetBuffer(_cellIdGenerationKernel, "_packedCellTypeControlAndIndex", _packedCellTypeControlAndIndexBuffer);

        _sorter = new ComputeBufferSorter(
            Constants.BLOCK_SIZE * Constants.THREADS_PER_BLOCK,
            _hashBuffer,
            _packedCellTypeControlAndIndexBuffer,
            _shaderContainer
        );

        _offsetsGenerationShader = _shaderContainer.Physics.OffsetsGenerationShader;
        _changesGenerationKernel = _offsetsGenerationShader.FindKernel("ChangesGeneration");
        _offsetsGenerationKernel = _offsetsGenerationShader.FindKernel("OffsetsGeneration");
        _offsetsGenerationShader.SetBuffer(_changesGenerationKernel, "_cellHash", _hashBuffer);
        _offsetsGenerationShader.SetBuffer(_changesGenerationKernel, "_changes", _changesBuffer);
        _offsetsGenerationShader.SetBuffer(_offsetsGenerationKernel, "_changes", _changesBuffer);
        _offsetsGenerationShader.SetBuffer(_offsetsGenerationKernel, "_offsets", _offsetsBuffer);
    }

    private uint[] _testData = new uint[Constants.BLOCK_SIZE * Constants.THREADS_PER_BLOCK];
    private uint[] _testData2 = new uint[Constants.BLOCK_SIZE * Constants.THREADS_PER_BLOCK];
    private uint[] _testData3 = new uint[Constants.BLOCK_SIZE * Constants.THREADS_PER_BLOCK];

    private void TestPrintComputeBuffer(ComputeBuffer buffer)
    {
        buffer.GetData(_testData);
        Debug.Log(Utils.ArrayToString(_testData));
    }

    private void Update()
    {
        _physicsIntegrationShader.SetFloat("_velocityDamping", 1);
        _physicsIntegrationShader.SetFloat("_deltaTime", Time.deltaTime);
        _physicsIntegrationShader.Dispatch(_physicsIntegrationKernel, Constants.DATA_BLOCK_SIZE, 1, 1);

        _cellIdGenerationShader.Dispatch(_cellIdGenerationKernel, Constants.DATA_BLOCK_SIZE, 1, 1);

        _sorter.Sort();
        _hashBuffer.GetData(_testData);
        // TestPrintComputeBuffer(_hashBuffer);
        for (int i = 1; i < Constants.BLOCK_SIZE * Constants.THREADS_PER_BLOCK; i++)
        {
            if (_testData[i] < _testData[i - 1])
            {
                Debug.LogError("error in sorting" + i.ToString());
                throw new Exception();
            }
        }

        _offsetsGenerationShader.Dispatch(_changesGenerationKernel, Constants.BLOCK_SIZE, 1, 1);
        _changesBuffer.GetData(_testData2);
        for (int i = 0; i < Constants.BLOCK_SIZE * Constants.THREADS_PER_BLOCK - 1; i++)
        {
            uint value = _testData[i] < _testData[i + 1] ? 1u : 0u;
            if (_testData2[i] != value)
            {
                Debug.LogError("error in changes" + i.ToString());
                throw new Exception();
            }
        }
        // TestPrintComputeBuffer(_changesBuffer);

        _sorter.Scan(_changesBuffer);
        _changesBuffer.GetData(_testData3);
        uint accumulatedValue = 0;
        for (int i = 1; i < Constants.BLOCK_SIZE * Constants.THREADS_PER_BLOCK; i++)
        {
            accumulatedValue += _testData2[i - 1];
            if (_testData3[i] != accumulatedValue)
            {
                Debug.LogError("error in scan" + i.ToString());
                throw new Exception();
            }
        }

        // TestPrintComputeBuffer(_changesBuffer);
        _offsetsGenerationShader.Dispatch(_offsetsGenerationKernel, Constants.BLOCK_SIZE, 1, 1);
        // TestPrintComputeBuffer(_offsetsBuffer);

        _hashBuffer.GetData(_testData);
        _offsetsBuffer.GetData(_testData2);

        int curIndexOfChange = 1;
        for (int i = 1; i < Constants.BLOCK_SIZE * Constants.THREADS_PER_BLOCK; i++)
        {
            if (_testData[i] > _testData[i - 1])
            {
                if (_testData2[curIndexOfChange] == i)
                {
                    curIndexOfChange++;
                }
                else
                {
                    Debug.LogError(i.ToString());
                    throw new Exception();
                }
            }
        }

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
        _hashBuffer.Release();
        _packedCellTypeControlAndIndexBuffer.Release();
        _changesBuffer.Release();
        _offsetsBuffer.Release();
        _sorter.Dispose();
    }
}