using UnityEngine;

public class GroundGridGenerator : MonoBehaviour
{
    public int width = 20;
    public int depth = 20;
    public float spacing = 1f;

    public float cellY = 0f;
    public int originCellX = 1; // (1,1) -> (0, cellY, 0)
    public int originCellZ = 1;

    [Header("Cell Prefab")]
    public GameObject cellPrefab;

    // 필요하면 외부에서 접근할 수 있도록 배열도 보관
    public Cell[,] cells;

    private void Start()
    {
        if (cellPrefab == null)
        {
            Debug.LogError("GroundGridGenerator: cellPrefab이 지정되지 않았습니다.");
            return;
        }

        BuildGrid();
    }

    private void BuildGrid()
    {
        // 기존 자식 제거(재생 중에만)
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Destroy(transform.GetChild(i).gameObject);
        }

        cells = new Cell[width, depth];

        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < depth; z++)
            {
                var cell = Instantiate(cellPrefab, transform);
                cell.name = $"Cell_{x}_{z}";

                float worldX = (x - originCellX) * spacing;
                float worldZ = (z - originCellZ) * spacing;

                cell.transform.position = new Vector3(worldX, cellY, worldZ);
                cell.transform.rotation = Quaternion.identity;

                cells[x, z] = cell.GetComponent<Cell>();
            }
        }
    }
}
