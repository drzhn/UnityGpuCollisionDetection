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
    // private const int NumObjects = 5;

    private const float SphereDiameter = 1;
    private const float CellSize = SphereDiameter * 1.41421356f;

    private ComputeBuffer _argsBuffer;

    private ComputeBuffer _positionBuffer;
    private ComputeBuffer _temporaryPositionBuffer;
    private ComputeBuffer _velocityBuffer;
    private ComputeBuffer _hashBuffer;
    private ComputeBuffer _packedCellTypeControlAndIndexBuffer;
    private ComputeBuffer _changesBuffer;
    private ComputeBuffer _offsetsBuffer;
    private ComputeBuffer _numOverlapsBuffer;
    private ComputeBuffer _numOffsetsBuffer;
    private int[] _numOverlaps = new int[1];
    private int[] _numOffsets = new int[1];

    private ComputeShader _physicsIntegrationShader;
    private int _physicsIntegrationKernel;

    private ComputeShader _cellIdGenerationShader;
    private int _cellIdGenerationKernel;

    private ComputeShader _offsetsGenerationShader;
    private int _changesGenerationKernel;
    private int _offsetsGenerationKernel;

    private ComputeShader _collisionDetectionShader;
    private int _collisionDetectionKernel;

    private ComputeBufferSorter _sorter;

    private void Awake()
    {
        // if (NumObjects != Constants.THREADS_PER_BLOCK * Constants.DATA_BLOCK_SIZE)
        // {
        //     throw new Exception();
        // }

        int[] args = new int[5] { 0, 0, 0, 0, 0 };
        _argsBuffer = new ComputeBuffer(1, args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
        args[0] = (int)_mesh.GetIndexCount(0);
        args[1] = NumObjects;
        args[2] = (int)_mesh.GetIndexStart(0);
        args[3] = (int)_mesh.GetBaseVertex(0);

        _argsBuffer.SetData(args);

        _positionBuffer = new ComputeBuffer(NumObjects, Marshal.SizeOf<Vector3>());
        _temporaryPositionBuffer = new ComputeBuffer(NumObjects, Marshal.SizeOf<Vector3>());
        _velocityBuffer = new ComputeBuffer(NumObjects, Marshal.SizeOf<Vector3>());
        _hashBuffer = new ComputeBuffer(NumObjects * 8, Marshal.SizeOf<uint>());
        _changesBuffer = new ComputeBuffer(NumObjects * 8, Marshal.SizeOf<uint>());
        _offsetsBuffer = new ComputeBuffer(NumObjects * 8, Marshal.SizeOf<uint>());
        _numOverlapsBuffer = new ComputeBuffer(1, Marshal.SizeOf<int>());
        _numOffsetsBuffer = new ComputeBuffer(1, Marshal.SizeOf<int>());
        _packedCellTypeControlAndIndexBuffer = new ComputeBuffer(NumObjects * 8, Marshal.SizeOf<uint>());

        Vector3[] data = new Vector3[NumObjects];
        for (int i = 0; i < NumPerSide; i++)
        {
            for (int j = 0; j < NumPerSide; j++)
            {
                for (int k = 0; k < NumPerSide; k++)
                {
                    data[i * NumPerSide * NumPerSide + j * NumPerSide + k] =
                        new Vector3(i, k, j)  + Vector3.one * 3 +
                        new Vector3(Random.Range(0.1f, 0.3f), Random.Range(0.1f, 0.3f), Random.Range(0.1f, 0.3f));
                }
            }
        }
        _positionBuffer.SetData(data);
        _temporaryPositionBuffer.SetData(data);

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
        _physicsIntegrationShader.SetBuffer(_physicsIntegrationKernel, "_temporaryPositions", _temporaryPositionBuffer);

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
        _offsetsGenerationShader.SetBuffer(_changesGenerationKernel, "_numOverlaps", _numOverlapsBuffer);
        _offsetsGenerationShader.SetBuffer(_offsetsGenerationKernel, "_changes", _changesBuffer);
        _offsetsGenerationShader.SetBuffer(_offsetsGenerationKernel, "_offsets", _offsetsBuffer);
        _offsetsGenerationShader.SetBuffer(_offsetsGenerationKernel, "_numOffsets", _numOffsetsBuffer);


        _collisionDetectionShader = _shaderContainer.Physics.CollisionDetectionShader;
        _collisionDetectionKernel = _collisionDetectionShader.FindKernel("CollisionDetection");

        _collisionDetectionShader.SetBuffer(_collisionDetectionKernel, "_positionBuffer", _positionBuffer);
        _collisionDetectionShader.SetBuffer(_collisionDetectionKernel, "_temporaryPositions", _temporaryPositionBuffer);
        _collisionDetectionShader.SetBuffer(_collisionDetectionKernel, "_cellHash", _hashBuffer);
        _collisionDetectionShader.SetBuffer(_collisionDetectionKernel, "_packedCellTypeControlAndIndex", _packedCellTypeControlAndIndexBuffer);
        _collisionDetectionShader.SetBuffer(_collisionDetectionKernel, "_offsets", _offsetsBuffer);
    }

    private uint[] _sortedHashData = new uint[Constants.BLOCK_SIZE * Constants.THREADS_PER_BLOCK];
    private uint[] _sortedObjectData = new uint[Constants.BLOCK_SIZE * Constants.THREADS_PER_BLOCK];
    private uint[] _offsetsData = new uint[Constants.BLOCK_SIZE * Constants.THREADS_PER_BLOCK];
    private uint[] _changesData = new uint[Constants.BLOCK_SIZE * Constants.THREADS_PER_BLOCK];
    private Vector3[] _positionsData = new Vector3[Constants.THREADS_PER_BLOCK * Constants.DATA_BLOCK_SIZE];
    private uint[] _testData3 = new uint[Constants.BLOCK_SIZE * Constants.THREADS_PER_BLOCK];

    private void TestPrintComputeBuffer(ComputeBuffer buffer)
    {
        buffer.GetData(_sortedHashData);
        Debug.Log(Utils.ArrayToString(_sortedHashData));
    }

    private void Update()
    {
        _physicsIntegrationShader.SetFloat("_velocityDamping", 1);
        _physicsIntegrationShader.SetFloat("_deltaTime", Time.deltaTime);
        _physicsIntegrationShader.Dispatch(_physicsIntegrationKernel, Constants.DATA_BLOCK_SIZE, 1, 1);

        _cellIdGenerationShader.Dispatch(_cellIdGenerationKernel, Constants.DATA_BLOCK_SIZE, 1, 1);

        _sorter.Sort();

        _offsetsGenerationShader.Dispatch(_changesGenerationKernel, Constants.BLOCK_SIZE, 1, 1);
        _numOverlapsBuffer.GetData(_numOverlaps);

        _sorter.Scan(_changesBuffer, Constants.BLOCK_SIZE * Constants.THREADS_PER_BLOCK);

        _offsetsGenerationShader.Dispatch(_offsetsGenerationKernel, Constants.BLOCK_SIZE, 1, 1);
        _numOffsetsBuffer.GetData(_numOffsets);

        _hashBuffer.GetData(_sortedHashData);
        _packedCellTypeControlAndIndexBuffer.GetData(_sortedObjectData);
        _offsetsBuffer.GetData(_offsetsData);
        _positionBuffer.GetData(_positionsData);
        _changesBuffer.GetData(_changesData);

        // {
        //     // hash and offsets validation
        //     int curIndexOfChange = 1;
        //     for (int i = 1; i < Constants.BLOCK_SIZE * Constants.THREADS_PER_BLOCK; i++)
        //     {
        //         if (_sortedHashData[i] < _sortedHashData[i - 1])
        //         {
        //             Debug.LogError("sorting error " + i.ToString());
        //             throw new Exception();
        //         }
        //
        //         if (_sortedHashData[i] > _sortedHashData[i - 1])
        //         {
        //             if (_offsetsData[curIndexOfChange] == i)
        //             {
        //                 curIndexOfChange++;
        //             }
        //             else
        //             {
        //                 Debug.LogError(i.ToString());
        //                 throw new Exception();
        //             }
        //         }
        //     }
        // }


        for (uint i = 0; i < 8; i++)
        {
            _collisionDetectionShader.SetInt("_currentCellType", (int)i);
            _collisionDetectionShader.SetInt("_numOverlaps", _numOverlaps[0] + 1);
            _collisionDetectionShader.SetInt("_numOffsets", _numOffsets[0]+1);
            // ValidateCollisionAlgorithm(i, _numOverlaps[0] + 1, _numOffsets[0]);
            _collisionDetectionShader.Dispatch(_changesGenerationKernel, Constants.BLOCK_SIZE, 1, 1);
        }

        Graphics.DrawMeshInstancedIndirect(
            _mesh,
            0,
            _instancedMaterial,
            new Bounds(Vector3.zero, Vector3.one * 1000),
            _argsBuffer
        );
    }

    struct ObjectData
    {
        public bool isHome;
        public uint cellType;
        public uint controlBits;
        public uint objectIndex;
    };

    ObjectData UnpackObjectData(uint packed)
    {
        ObjectData data = new ObjectData
        {
            isHome = ((packed >> 31) & ((1 << 1) - 1)) == 1,
            cellType = (packed >> 28) & ((1 << 3) - 1),
            controlBits = (packed >> 20) & ((1 << 8) - 1),
            objectIndex = (packed >> 0) & ((1 << 20) - 1),
        };
        return data;
    }

    uint mod2(uint v)
    {
        return v & 1;
    }

    uint CalculateCellType(uint x, uint y, uint z)
    {
        return mod2(x) + mod2(y) * 2 + mod2(z) * 4;
    }

    uint GetCellType(uint hash)
    {
        uint x = (hash >> 20) & ((1 << 10) - 1);
        uint y = (hash >> 10) & ((1 << 10) - 1);
        uint z = (hash >> 0) & ((1 << 10) - 1);

        return CalculateCellType(x, y, z);
    }

    void CheckCollision(uint index, uint id2)
    {
        // _positionBuffer[index]  = float3(0,0,0);
        // _positionBuffer[id2]  = float3(0,1,0);
        // return;
        Vector3 objectPos = _positionsData[index];
        Vector3 collisionPos = _positionsData[id2];
        float currentDistance = Vector3.Distance(collisionPos, objectPos);
        float desiredDistance = 1;
        if (currentDistance < desiredDistance)
        {
            Vector3 dir = (objectPos - collisionPos).normalized * (desiredDistance - currentDistance) / 2;
            // float k =  0.5;
            // _positionBuffer[index] -= k * 0.95 * dir;
            // _positionBuffer[index] += dir;
        }
    }


    void ValidateCollisionAlgorithm(uint cellType, int numOverlaps, int numOffsets)
    {
        for (uint id = 0; id < Constants.THREADS_PER_BLOCK * Constants.BLOCK_SIZE; id++)
        {
            if (id >= numOffsets)
            {
                continue;
            }

            uint offsetStart = _offsetsData[id];
            uint offsetEnd = _offsetsData[id + 1];
            if (offsetEnd - offsetStart <= 1)
            {
                continue;
            }

            bool homeEnded = false;
            
            uint cellId1 = _sortedHashData[offsetStart];
            
            if (cellId1 == 0xFFFFFFFF || GetCellType(cellId1) != cellType)
            {
                continue;
            }
            
            for (uint i = offsetStart; i < offsetEnd - 1; i++)
            {
                ObjectData data1 = UnpackObjectData(_sortedObjectData[i]);
                if (!data1.isHome)
                {
                    homeEnded = true;
                }

                if (data1.isHome && homeEnded)
                {
                    throw new Exception("WTF");
                }

                if (homeEnded) continue;
                for (uint j = i + 1; j < offsetEnd; j++)
                {
                    ObjectData data2 = UnpackObjectData(_sortedObjectData[j]);

                    CheckCollision(data1.objectIndex, data2.objectIndex);
                }
            }
        }
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
        _numOverlapsBuffer.Release();
        _numOffsetsBuffer.Release();
        _sorter.Dispose();
    }
}