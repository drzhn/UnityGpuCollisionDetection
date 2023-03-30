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
    [SerializeField] [Range(0, 1)] private float _velocityDamping = 1;
    [SerializeField] [Range(5, 60)] private float _collisionStiffness = 15;
    [SerializeField] private ShaderContainer _shaderContainer;
    [SerializeField] private Material _instancedMaterial;
    [SerializeField] private Mesh _mesh;

    private const int NumPerSide = 32;

    private const int NumObjects = NumPerSide * NumPerSide * NumPerSide;

    private const float SphereDiameter = 1;
    private const float CellSize = SphereDiameter * 1.41421356f;

    private ComputeBuffer _argsBuffer;

    private ComputeBuffer _positionBuffer;

    private ComputeBuffer _prevPositionsBuffer;

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

    private ComputeShader _postCollisionUpdateShader;
    private int _postCollisionUpdateKernel;

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
        _prevPositionsBuffer = new ComputeBuffer(NumObjects, Marshal.SizeOf<Vector3>());
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
                        new Vector3(i, k, j) + Vector3.one * 3 +
                        new Vector3(Random.Range(0.1f, 0.3f), Random.Range(0.1f, 0.3f), Random.Range(0.1f, 0.3f));
                }
            }
        }

        _positionBuffer.SetData(data);
        _prevPositionsBuffer.SetData(data);

        _instancedMaterial.SetBuffer("_positionBuffer", _positionBuffer);

        _physicsIntegrationShader = _shaderContainer.Physics.PhysicsIntegrationShader;
        _physicsIntegrationKernel = _physicsIntegrationShader.FindKernel("PhysicsIntegration");
        _physicsIntegrationShader.SetBuffer(_physicsIntegrationKernel, "_positionBuffer", _positionBuffer);
        _physicsIntegrationShader.SetBuffer(_physicsIntegrationKernel, "_prevPositions", _prevPositionsBuffer);

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
        _collisionDetectionShader.SetBuffer(_collisionDetectionKernel, "_cellHash", _hashBuffer);
        _collisionDetectionShader.SetBuffer(_collisionDetectionKernel, "_packedCellTypeControlAndIndex", _packedCellTypeControlAndIndexBuffer);
        _collisionDetectionShader.SetBuffer(_collisionDetectionKernel, "_offsets", _offsetsBuffer);

        _postCollisionUpdateShader = _shaderContainer.Physics.PostCollisionUpdateShader;
        _postCollisionUpdateKernel = _postCollisionUpdateShader.FindKernel("PostCollisionUpdate");
        _postCollisionUpdateShader.SetBuffer(_postCollisionUpdateKernel, "_positionBuffer", _positionBuffer);

        _prevDeltaTime = Time.deltaTime;
    }

    #region Test data arrays for debug purposes

    // private uint[] _sortedHashData = new uint[Constants.BLOCK_SIZE * Constants.THREADS_PER_BLOCK];
    // private uint[] _sortedObjectData = new uint[Constants.BLOCK_SIZE * Constants.THREADS_PER_BLOCK];
    // private uint[] _offsetsData = new uint[Constants.BLOCK_SIZE * Constants.THREADS_PER_BLOCK];
    // private uint[] _changesData = new uint[Constants.BLOCK_SIZE * Constants.THREADS_PER_BLOCK];
    // private Vector3[] _positionsData = new Vector3[Constants.THREADS_PER_BLOCK * Constants.DATA_BLOCK_SIZE];
    // private uint[] _testData3 = new uint[Constants.BLOCK_SIZE * Constants.THREADS_PER_BLOCK];

    // private void TestPrintComputeBuffer(ComputeBuffer buffer)
    // {
    //     buffer.GetData(_sortedHashData);
    //     Debug.Log(Utils.ArrayToString(_sortedHashData));
    // }

    #endregion

    private float _prevDeltaTime = 0;

    private void Update()
    {
        _physicsIntegrationShader.SetFloat("_velocityDamping", _velocityDamping);
        _physicsIntegrationShader.SetFloat("_deltaTime", Time.deltaTime);
        _physicsIntegrationShader.SetFloat("_prevDeltaTime", _prevDeltaTime);
        _physicsIntegrationShader.Dispatch(_physicsIntegrationKernel, Constants.DATA_BLOCK_SIZE, 1, 1);

        _cellIdGenerationShader.Dispatch(_cellIdGenerationKernel, Constants.DATA_BLOCK_SIZE, 1, 1);

        _sorter.Sort();

        _offsetsGenerationShader.Dispatch(_changesGenerationKernel, Constants.BLOCK_SIZE, 1, 1);
        _numOverlapsBuffer.GetData(_numOverlaps);

        _sorter.Scan(_changesBuffer, Constants.BLOCK_SIZE * Constants.THREADS_PER_BLOCK);

        _offsetsGenerationShader.Dispatch(_offsetsGenerationKernel, Constants.BLOCK_SIZE, 1, 1);
        _numOffsetsBuffer.GetData(_numOffsets);

        #region hash and offsets validation

        // { 
        //    
        //     _hashBuffer.GetData(_sortedHashData);
        //     _packedCellTypeControlAndIndexBuffer.GetData(_sortedObjectData);
        //     _offsetsBuffer.GetData(_offsetsData);
        //     _positionBuffer.GetData(_positionsData);
        //     _changesBuffer.GetData(_changesData);
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

        #endregion

        _collisionDetectionShader.SetFloat("_deltaTime", Time.deltaTime);
        for (uint i = 0; i < 8; i++)
        {
            _collisionDetectionShader.SetInt("_currentCellType", (int)i);
            _collisionDetectionShader.SetInt("_numOverlaps", _numOverlaps[0] + 1);
            _collisionDetectionShader.SetInt("_numOffsets", _numOffsets[0] + 1);
            _collisionDetectionShader.SetFloat("_collisionStiffness", _collisionStiffness);
            // ValidateCollisionAlgorithm(i, _numOverlaps[0] + 1, _numOffsets[0]);
            _collisionDetectionShader.Dispatch(_changesGenerationKernel, Constants.BLOCK_SIZE, 1, 1);
        }

        _postCollisionUpdateShader.Dispatch(_postCollisionUpdateKernel, Constants.DATA_BLOCK_SIZE, 1, 1);

        Graphics.DrawMeshInstancedIndirect(
            _mesh,
            0,
            _instancedMaterial,
            new Bounds(Vector3.zero, Vector3.one * 1000),
            _argsBuffer
        );

        _prevDeltaTime = Time.deltaTime;
    }

    private void OnDestroy()
    {
        _positionBuffer.Release();
        _prevPositionsBuffer.Release();
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