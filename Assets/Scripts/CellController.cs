using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CellController : MonoBehaviour
{
    [Header("Source")]
    [SerializeField] private GroundGridGenerator groundGridGenerator;

    [Header("Unavailable Cell Count")]
    [SerializeField] private int minUnavailableCells = 3;
    [SerializeField] private int maxUnavailableCells = 8;

    [Header("Unavailable Time (Seconds)")]
    // 이 값은 두 가지 용도로 사용됩니다.
    // 1) 셀을 비활성화한 뒤 다시 활성화될 시간(=Cell.SetAvailable의 파라미터)
    // 2) 다음 비활성화 이벤트가 발생하기까지의 대기 시간(=코루틴의 WaitForSeconds)
    [SerializeField] private float minUnavailableSeconds = 0.5f;
    [SerializeField] private float maxUnavailableSeconds = 2.0f;

    private Cell[] _cells = new Cell[ZeroInt];
    private Coroutine _loopCoroutine;
    private bool _loopStarted;
    private int _unavailableCellCount;
    private List<Cell> _availableCells = new List<Cell>();
    private List<Cell> _unavailableCells = new List<Cell>();
    private Dictionary<Cell, int> _availableCellIndex = new Dictionary<Cell, int>();
    private Dictionary<Cell, int> _unavailableCellIndex = new Dictionary<Cell, int>();

    private Cell[,] _grid;
    private int _gridWidth;
    private int _gridDepth;
    private Dictionary<Cell, Vector2Int> _cellToIndex = new Dictionary<Cell, Vector2Int>();
    private float _positionToleranceSqr = 0.35f * 0.35f;

    private const int ZeroInt = 0;
    private const int OneInt = 1;
    private const float ZeroSeconds = 0f;
    private const float MinTimeClamp = 0f;

    private void OnValidate()
    {
        if (minUnavailableCells < ZeroInt)
            minUnavailableCells = ZeroInt;

        if (maxUnavailableCells < minUnavailableCells)
            maxUnavailableCells = minUnavailableCells;

        if (minUnavailableSeconds < ZeroSeconds)
            minUnavailableSeconds = ZeroSeconds;

        if (maxUnavailableSeconds < minUnavailableSeconds)
            maxUnavailableSeconds = minUnavailableSeconds;
    }

    private void OnEnable()
    {
        if (groundGridGenerator == null)
            groundGridGenerator = FindFirstObjectByType<GroundGridGenerator>();

        if (groundGridGenerator == null)
            return;

        groundGridGenerator.GridBuilt += HandleGridBuilt;

        // 이미 생성되어 있다면 즉시 처리
        if (groundGridGenerator.IsBuilt && groundGridGenerator.Cells != null)
            HandleGridBuilt(groundGridGenerator.Cells);
    }

    private void OnDisable()
    {
        if (groundGridGenerator != null)
            groundGridGenerator.GridBuilt -= HandleGridBuilt;

        UnsubscribeFromCells();

        if (_loopCoroutine != null)
            StopCoroutine(_loopCoroutine);

        _loopCoroutine = null;
        _loopStarted = false;
        _unavailableCellCount = ZeroInt;
        _availableCells.Clear();
        _unavailableCells.Clear();
        _availableCellIndex.Clear();
        _unavailableCellIndex.Clear();

        _grid = null;
        _gridWidth = ZeroInt;
        _gridDepth = ZeroInt;
        _cellToIndex.Clear();
        _positionToleranceSqr = 0.35f * 0.35f;
    }

    private void HandleGridBuilt(Cell[,] grid)
    {
        if (_loopStarted)
            return;

        if (grid == null)
            return;

        int width = grid.GetLength(ZeroInt);
        int depth = grid.GetLength(OneInt);

        _grid = grid;
        _gridWidth = width;
        _gridDepth = depth;
        _cellToIndex.Clear();

        var list = new List<Cell>(width * depth);
        for (int x = ZeroInt; x < width; x++)
        {
            for (int z = ZeroInt; z < depth; z++)
            {
                var cell = grid[x, z];
                if (cell != null)
                {
                    list.Add(cell);
                    _cellToIndex[cell] = new Vector2Int(x, z);
                }
            }
        }

        _cells = list.ToArray();

        if (_cells.Length == ZeroInt)
            return;

        // 플레이어 좌표 -> 현재 셀 매핑 시 오차 허용 범위를 추정합니다.
        float stepX = ZeroSeconds;
        if (width >= 2)
        {
            for (int z = ZeroInt; z < depth; z++)
            {
                var a = grid[ZeroInt, z];
                var b = grid[OneInt, z];
                if (a != null && b != null)
                {
                    stepX = Vector3.Distance(a.transform.position, b.transform.position);
                    break;
                }
            }
        }

        float stepZ = ZeroSeconds;
        if (depth >= 2)
        {
            for (int x = ZeroInt; x < width; x++)
            {
                var a = grid[x, ZeroInt];
                var b = grid[x, OneInt];
                if (a != null && b != null)
                {
                    stepZ = Vector3.Distance(a.transform.position, b.transform.position);
                    break;
                }
            }
        }

        float minStep = Mathf.Min(stepX > ZeroSeconds ? stepX : 1f, stepZ > ZeroSeconds ? stepZ : 1f);
        float tolerance = Mathf.Max(minStep * 0.35f, 0.15f);
        _positionToleranceSqr = tolerance * tolerance;

        SubscribeToCellsAndComputeInitialCount();

        // 런타임에서도 셀 개수보다 큰 min/max 설정은 불가능하므로 범위를 안전하게 보정
        int cellCount = _cells.Length;
        if (minUnavailableCells > cellCount)
            minUnavailableCells = cellCount;

        if (maxUnavailableCells > cellCount)
            maxUnavailableCells = cellCount;

        if (maxUnavailableCells < minUnavailableCells)
            maxUnavailableCells = minUnavailableCells;

        _loopCoroutine = StartCoroutine(UnavailableLoop());
        _loopStarted = true;
    }

    private void SubscribeToCellsAndComputeInitialCount()
    {
        UnsubscribeFromCells();

        _availableCells.Clear();
        _unavailableCells.Clear();
        _availableCellIndex.Clear();
        _unavailableCellIndex.Clear();

        for (int i = ZeroInt; i < _cells.Length; i++)
        {
            var cell = _cells[i];
            if (cell == null)
                continue;

            cell.AvailabilityChanged += HandleCellAvailabilityChanged;

            if (cell.IsAvailable)
                AddToAvailablePool(cell);
            else
                AddToUnavailablePool(cell);
        }

        _unavailableCellCount = _unavailableCells.Count;
    }

    private void UnsubscribeFromCells()
    {
        if (_cells == null)
            return;

        for (int i = ZeroInt; i < _cells.Length; i++)
        {
            var cell = _cells[i];
            if (cell == null)
                continue;

            cell.AvailabilityChanged -= HandleCellAvailabilityChanged;
        }
    }

    private void HandleCellAvailabilityChanged(Cell cell, bool isAvailable)
    {
        if (cell == null)
            return;

        if (isAvailable)
        {
            RemoveFromUnavailablePool(cell);
            AddToAvailablePool(cell);
        }
        else
        {
            RemoveFromAvailablePool(cell);
            AddToUnavailablePool(cell);
        }

        // 이벤트 처리 과정에서의 예외/불일치(예: 중복 구독 등)를 방어하기 위해 count를 리스트 기준으로 재동기화
        _unavailableCellCount = _unavailableCells.Count;
    }

    private void AddToAvailablePool(Cell cell)
    {
        if (cell == null)
            return;

        if (_availableCellIndex.ContainsKey(cell))
            return;

        _availableCellIndex[cell] = _availableCells.Count;
        _availableCells.Add(cell);
    }

    private void AddToUnavailablePool(Cell cell)
    {
        if (cell == null)
            return;

        if (_unavailableCellIndex.ContainsKey(cell))
            return;

        _unavailableCellIndex[cell] = _unavailableCells.Count;
        _unavailableCells.Add(cell);
    }

    private void RemoveFromAvailablePool(Cell cell)
    {
        if (cell == null)
            return;

        if (!_availableCellIndex.TryGetValue(cell, out int index))
            return;

        int lastIndex = _availableCells.Count - OneInt;
        Cell lastCell = _availableCells[lastIndex];

        _availableCells[index] = lastCell;
        _availableCellIndex[lastCell] = index;

        _availableCells.RemoveAt(lastIndex);
        _availableCellIndex.Remove(cell);
    }

    private void RemoveFromUnavailablePool(Cell cell)
    {
        if (cell == null)
            return;

        if (!_unavailableCellIndex.TryGetValue(cell, out int index))
            return;

        int lastIndex = _unavailableCells.Count - OneInt;
        Cell lastCell = _unavailableCells[lastIndex];

        _unavailableCells[index] = lastCell;
        _unavailableCellIndex[lastCell] = index;

        _unavailableCells.RemoveAt(lastIndex);
        _unavailableCellIndex.Remove(cell);
    }

    private bool DisableOneRandomAvailableCell(float unavailableSeconds)
    {
        if (_availableCells.Count == ZeroInt)
            return false;

        int chosenIndex = Random.Range(ZeroInt, _availableCells.Count);
        var cell = _availableCells[chosenIndex];

        if (cell == null || !cell.IsAvailable)
            return false;

        cell.SetAvailable(unavailableSeconds);
        return true;
    }

    private bool EnableOneRandomUnavailableCell()
    {
        if (_unavailableCells.Count == ZeroInt)
            return false;

        int chosenIndex = Random.Range(ZeroInt, _unavailableCells.Count);
        var cell = _unavailableCells[chosenIndex];

        if (cell == null || cell.IsAvailable)
            return false;

        // SetAvailable(float)는 내부적으로 false->대기->true를 수행하므로 0초면 즉시 true 상태가 됩니다.
        cell.SetAvailable(ZeroSeconds);
        return true;
    }

    private IEnumerator UnavailableLoop()
    {
        if (_cells.Length == ZeroInt)
            yield break;

        // 시작 시 min 이상으로 보정
        while (_unavailableCellCount < minUnavailableCells)
        {
            float unavailableSeconds = Random.Range(minUnavailableSeconds, maxUnavailableSeconds);
            unavailableSeconds = Mathf.Max(unavailableSeconds, MinTimeClamp);

            if (!DisableOneRandomAvailableCell(unavailableSeconds))
                break;
        }

        float nextDisableAtTime = Time.time + Random.Range(minUnavailableSeconds, maxUnavailableSeconds);
        nextDisableAtTime = Mathf.Max(nextDisableAtTime, Time.time);

        while (true)
        {
            // 외부 조작 등으로 상한을 초과했다면 즉시 보정
            while (_unavailableCellCount > maxUnavailableCells)
            {
                if (!EnableOneRandomUnavailableCell())
                    break;
            }

            // 하한이 깨지면 즉시(프레임 단위로) 보정하여 min~max를 유지
            while (_unavailableCellCount < minUnavailableCells)
            {
                float unavailableSeconds = Random.Range(minUnavailableSeconds, maxUnavailableSeconds);
                unavailableSeconds = Mathf.Max(unavailableSeconds, MinTimeClamp);

                if (!DisableOneRandomAvailableCell(unavailableSeconds))
                    break;
            }

            // 랜덤 타이밍이면 1개만 추가로 비활성화 시도 (단, 상한을 넘지 않게)
            if (Time.time >= nextDisableAtTime && _unavailableCellCount < maxUnavailableCells)
            {
                float unavailableSeconds = Random.Range(minUnavailableSeconds, maxUnavailableSeconds);
                unavailableSeconds = Mathf.Max(unavailableSeconds, MinTimeClamp);

                if (DisableOneRandomAvailableCell(unavailableSeconds))
                {
                    nextDisableAtTime = Time.time + Random.Range(minUnavailableSeconds, maxUnavailableSeconds);
                    nextDisableAtTime = Mathf.Max(nextDisableAtTime, Time.time);
                }
            }

            yield return null;
        }
    }

    /// <summary>
    /// 현재 월드좌표가 어떤 셀 중심에 가장 가까운지 찾습니다.
    /// </summary>
    public bool TryGetCellAtWorldPosition(Vector3 worldPosition, out Cell cell)
    {
        cell = null;

        if (_cells == null || _cells.Length == ZeroInt)
            return false;

        float bestDistSqr = float.PositiveInfinity;

        for (int i = ZeroInt; i < _cells.Length; i++)
        {
            var c = _cells[i];
            if (c == null)
                continue;

            Vector3 cp = c.transform.position;
            float dx = worldPosition.x - cp.x;
            float dz = worldPosition.z - cp.z;
            float distSqr = dx * dx + dz * dz;

            if (distSqr < bestDistSqr)
            {
                bestDistSqr = distSqr;
                cell = c;
            }
        }

        if (cell == null)
            return false;

        return bestDistSqr <= _positionToleranceSqr;
    }

    /// <summary>
    /// fromCell 기준으로 입력 방향(상/하/좌/우)의 이웃 셀을 반환합니다.
    /// direction은 PlayerController가 전달하는 Vector3.right/left/forward/back 기준입니다.
    /// </summary>
    public Cell GetNeighborCell(Cell fromCell, Vector3 direction)
    {
        if (fromCell == null || _grid == null)
            return null;

        if (!_cellToIndex.TryGetValue(fromCell, out Vector2Int idx))
            return null;

        int nx = idx.x;
        int nz = idx.y;

        // X축이 더 큰 경우: 좌/우, Z축이 더 큰 경우: 앞/뒤
        if (Mathf.Abs(direction.x) > Mathf.Abs(direction.z))
        {
            if (direction.x > 0f)
                nx++;
            else
                nx--;
        }
        else
        {
            if (direction.z > 0f)
                nz++;
            else
                nz--;
        }

        if (nx < ZeroInt || nx >= _gridWidth || nz < ZeroInt || nz >= _gridDepth)
            return null;

        return _grid[nx, nz];
    }
}

