using System;
using System.Collections.Generic;
using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    [Header("Enemy Prefab")]
    [SerializeField] private GameObject enemyPrefab;

    [Header("Spawn Cells (x,z) in Grid")]
    [Tooltip("GroundGridGenerator의 Cells 배열 인덱스 기준입니다. (x,z) → 해당 Cell 월드 위치에 생성")]
    [SerializeField] private Vector2Int[] spawnCellCoords = Array.Empty<Vector2Int>();

    [Header("Instantiation")]
    [SerializeField] private float yOffset = 0f;
    [SerializeField] private bool parentEnemiesUnderSelf = true;
    [SerializeField] private bool usePrefabRotation = true;

    public void ClearSpawnedEnemies()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Destroy(transform.GetChild(i).gameObject);
        }
    }

    public void SpawnEnemiesFromGrid(Cell[,] cells)
    {
        if (enemyPrefab == null)
        {
            Debug.LogError("EnemySpawner: enemyPrefab이 설정되지 않았습니다.");
            return;
        }

        if (cells == null)
        {
            Debug.LogError("EnemySpawner: Cells가 null입니다. 그리드 빌드가 완료됐는지 확인해주세요.");
            return;
        }

        int width = cells.GetLength(0);
        int depth = cells.GetLength(1);

        Quaternion rot = usePrefabRotation ? enemyPrefab.transform.rotation : Quaternion.identity;
        Transform parent = parentEnemiesUnderSelf ? transform : null;

        int spawnedCount = 0;
        var warned = new HashSet<string>();

        for (int i = 0; i < spawnCellCoords.Length; i++)
        {
            Vector2Int coord = spawnCellCoords[i];

            if (coord.x < 0 || coord.x >= width || coord.y < 0 || coord.y >= depth)
            {
                string key = $"OOB_{coord.x}_{coord.y}";
                if (warned.Add(key))
                    Debug.LogWarning($"EnemySpawner: spawnCellCoords[{i}]={coord} 가 범위를 벗어났습니다. (width={width}, depth={depth})");
                continue;
            }

            Cell cell = cells[coord.x, coord.y];
            if (cell == null)
                continue;

            Vector3 pos = cell.transform.position + Vector3.up * yOffset;

            if (parentEnemiesUnderSelf)
                Instantiate(enemyPrefab, pos, rot, parent);
            else
                Instantiate(enemyPrefab, pos, rot);

            spawnedCount++;
        }

        Debug.Log($"EnemySpawner: spawned {spawnedCount} enemy(s).");
    }
}
