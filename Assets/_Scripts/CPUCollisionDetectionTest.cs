using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CPUCollisionDetectionTest : MonoBehaviour
{
    [SerializeField] private List<Transform> _items;

    private const float SphereDiameter = 1;
    private const float CellSize = SphereDiameter * 1.41421356f;
    private const int NumCells = 10;

    private const int CellIdArraySize = 100;
    private const int XSHIFT = 20;
    private const int YSHIFT = 10;
    private const int ZSHIFT = 0;

    struct CellIdItem
    {
        public uint hash;
        public int objectId;
        public bool isHome;
    }

    private CellIdItem[] _cellIds = new CellIdItem[CellIdArraySize];

    void EmptyCellIds()
    {
        for (var i = 0; i < _cellIds.Length; i++)
        {
            _cellIds[i] = new CellIdItem()
            {
                hash = 0xFFFFFFFF,
                objectId = -1,
                isHome = false
            };
        }
    }

    private void Awake()
    {
        EmptyCellIds();
    }

    int GetNeighbourOffset(float v)
    {
        int floor = Mathf.FloorToInt(v);
        float middle = floor + 0.5f;
        int sign = (int)Mathf.Sign(v - middle);
        float abs = Mathf.Abs(v - middle);
        // return ((abs + delta) > 0.5f) && ((floor + sign) >= 0) ? sign : 0;
        return sign;
    }

    // bool IntersectNeighbourOffset(float v, float delta, int sign)
    // {
    //     int floor = Mathf.FloorToInt(v);
    //     float middle = floor + 0.5f;
    //     float abs = Mathf.Abs(v - middle);
    //     int sign2 = (int)Mathf.Sign(v - middle);
    //     return ((abs + delta) > 0.5f) && (sign2 == sign || sign == 0);
    // }

    bool SphereCubeIntersection(Vector3Int cubeMin, Vector3Int cubeMax, Vector3 sphereCenter, float sphereRadius)
    {
        float dmin = 0;
        float r2 = Mathf.Pow(sphereRadius, 2);
        for (int i = 0; i < 3; i++)
        {
            if (sphereCenter[i] < cubeMin[i])
                dmin += Mathf.Pow(sphereCenter[i] - cubeMin[i], 2);
            else if (sphereCenter[i] > cubeMax[i])
                dmin += Mathf.Pow(sphereCenter[i] - cubeMax[i], 2);
        }

        if (dmin <= r2) return (true);
        return false;
    }
    
    int numPossibleCollisions = 0;
    void Update()
    {
        for (var i = 0; i < _items.Count; i++)
        {
            Vector3 cellPos = new Vector3(
                _items[i].position.x / CellSize,
                _items[i].position.y / CellSize,
                _items[i].position.z / CellSize);

            Vector3Int cellId = new Vector3Int(
                Mathf.FloorToInt(cellPos.x),
                Mathf.FloorToInt(cellPos.y),
                Mathf.FloorToInt(cellPos.z));

            _cellIds[i * 8].hash = (uint)((cellId.x << XSHIFT) |
                                          (cellId.y << YSHIFT) |
                                          (cellId.z << ZSHIFT));
            _cellIds[i * 8].isHome = true;
            _cellIds[i * 8].objectId = i;

            Vector3Int offset = new Vector3Int(
                GetNeighbourOffset(cellPos.x),
                GetNeighbourOffset(cellPos.y),
                GetNeighbourOffset(cellPos.z)
            );
            int curIndex = 1;
            for (int j = 1; j < 8; j++)
            {
                Vector3Int neighbourOffset = Vector3Int.Scale(
                    offset,
                    new Vector3Int((j >> 0) & 1, (j >> 1) & 1, (j >> 2) & 1)
                );

                Vector3Int neighbour = cellId + neighbourOffset;


                if (
                    neighbour.x < 0 ||
                    neighbour.y < 0 ||
                    neighbour.z < 0 ||
                    !SphereCubeIntersection(
                        neighbour,
                        neighbour + Vector3Int.one,
                        cellPos,
                        SphereDiameter / 2 / CellSize)
                )
                {
                    continue;
                }

                _cellIds[i * 8 + curIndex].hash = (uint)((neighbour.x << XSHIFT) |
                                                         (neighbour.y << YSHIFT) |
                                                         (neighbour.z << ZSHIFT));
                _cellIds[i * 8 + curIndex].isHome = false;
                _cellIds[i * 8 + curIndex].objectId = i;

                curIndex++;
            }
        }

        Array.Sort(_cellIds, (item1, item2) =>
        {
            if (item1.hash > item2.hash) return 1;
            if (item1.hash < item2.hash) return -1;
            return 0;
        });

        uint currentHash = _cellIds[0].hash;
        numPossibleCollisions = 0;
        for (var i = 1; i < _cellIds.Length; i++)
        {
            if (_cellIds[i].objectId == -1) break;

            if (_cellIds[i].hash == currentHash)
            {
                numPossibleCollisions++;
            }
            else
            {
                currentHash = _cellIds[i].hash;
            }
        }

        // Debug.Log(numPossibleCollisions);

        EmptyCellIds();
    }
    void OnGUI() {
        GUILayout.Label(numPossibleCollisions.ToString());
    }
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        foreach (Transform item in _items)
        {
            Gizmos.DrawWireSphere(item.position, SphereDiameter / 2);
        }

        Gizmos.color = new Color(0, 0, 1, 0.2f);
        for (int i = 0; i <= NumCells; i++)
        {
            for (int j = 0; j <= NumCells; j++)
            {
                for (int k = 0; k <= NumCells; k++)
                {
                    Gizmos.DrawSphere(new Vector3(i, j, k) * CellSize, 0.1f);
                }
            }
        }
    }
}