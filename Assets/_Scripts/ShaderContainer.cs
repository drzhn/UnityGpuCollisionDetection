using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class SortingShaderContainer
{
    public ComputeShader LocalRadixSortShader => _localRadixSortShader;
    public ComputeShader ScanShader => _scanShader;
    public ComputeShader GlobalRadixSortShader => _globalRadixSortShader;

    [SerializeField] private ComputeShader _localRadixSortShader;
    [SerializeField] private ComputeShader _scanShader;
    [SerializeField] private ComputeShader _globalRadixSortShader;
}

[Serializable]
public class PhysicsShaderContainer
{
    public ComputeShader PhysicsIntegrationShader => _physicsIntegrationShader;
    public ComputeShader CellIdGenerationShader => _cellIdGenerationShader;

    [SerializeField] private ComputeShader _physicsIntegrationShader;
    [SerializeField] private ComputeShader _cellIdGenerationShader;
}

[Serializable]
public class ShaderContainer : MonoBehaviour
{
    public SortingShaderContainer Sorting => _sorting;
    public PhysicsShaderContainer Physics => _physics;

    [SerializeField] private SortingShaderContainer _sorting;
    [SerializeField] private PhysicsShaderContainer _physics;
}