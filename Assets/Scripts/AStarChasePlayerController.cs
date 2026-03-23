using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// A*로 플레이어에게 최단 경로(통과 가능한 Cell만)를 찾아 다음 칸으로 이동합니다.
/// </summary>
public class AStarChasePlayerController : EnemyControllerBase
{
    [Header("AI (A* Pathfinding)")]
    [Tooltip("true면 A*로 플레이어에게 최단거리 접근을 시도합니다.")]
    [SerializeField] private bool useAStarAI = true;

    [Tooltip("A* 탐색에서 방문(확장)할 최대 노드 수입니다. 너무 작으면 길 못 찾을 수 있습니다.")]
    [SerializeField] private int maxSearchNodes = 2048;

    [Header("Target")]
    [Tooltip("null이면 자동으로 PlayerController를 찾아서 사용합니다.")]
    [SerializeField] private Transform playerTransform;

    private PlayerController _playerController;

    // 하드코딩 숫자를 상수로 분리 (규칙 준수)
    private const float MinInputSqrMagnitude = 0.01f;

    protected override bool IsAIModeEnabled()
    {
        return useAStarAI;
    }

    protected override bool TryGetNextTargetCell(Cell startCell, out Cell nextCell)
    {
        nextCell = null;

        if (cellController == null || startCell == null)
            return false;

        if (playerTransform == null)
        {
            _playerController = _playerController != null ? _playerController : FindFirstObjectByType<PlayerController>();
            if (_playerController != null)
                playerTransform = _playerController.transform;
        }

        if (playerTransform == null)
            return false;

        if (!TryGetCellAtWorldPosition(playerTransform.position, out Cell goalCell))
            return false;

        nextCell = GetNextCellByAStar(startCell, goalCell);
        return nextCell != null && nextCell.IsAvailable;
    }

    private Cell GetNextCellByAStar(Cell startCell, Cell goalCell)
    {
        List<Cell> path = FindPathAStar(startCell, goalCell);
        if (path == null || path.Count < 2)
            return null;

        return path[1];
    }

    private List<Cell> FindPathAStar(Cell startCell, Cell goalCell)
    {
        if (startCell == null || goalCell == null)
            return null;

        // openSet: 현재까지 발견된 후보 노드
        List<Cell> openSet = new List<Cell>(32);
        HashSet<Cell> openSetLookup = new HashSet<Cell>();
        HashSet<Cell> closedSet = new HashSet<Cell>();

        Dictionary<Cell, Cell> cameFrom = new Dictionary<Cell, Cell>();
        Dictionary<Cell, float> gScore = new Dictionary<Cell, float>();
        Dictionary<Cell, float> fScore = new Dictionary<Cell, float>();

        openSet.Add(startCell);
        openSetLookup.Add(startCell);
        gScore[startCell] = 0f;
        fScore[startCell] = HeuristicCost(startCell, goalCell);

        // goal에 도달하지 못하면, 방문한 노드 중 목표와 가장 가까운 노드를 반환(접근 목적).
        Cell bestCandidate = startCell;
        float bestCandidateH = HeuristicCost(startCell, goalCell);

        int expansions = 0;

        while (openSet.Count > 0 && expansions < maxSearchNodes)
        {
            // openSet에서 fScore 최소 노드 선택
            Cell current = openSet[0];
            float bestF = fScore.TryGetValue(current, out float curF) ? curF : float.PositiveInfinity;

            for (int i = 1; i < openSet.Count; i++)
            {
                Cell c = openSet[i];
                float f = fScore.TryGetValue(c, out float cf) ? cf : float.PositiveInfinity;
                if (f < bestF)
                {
                    bestF = f;
                    current = c;
                }
            }

            openSetLookup.Remove(current);
            openSet.Remove(current);
            closedSet.Add(current);

            if (current == goalCell)
                return ReconstructPath(cameFrom, current);

            // 다음 step을 뽑기 위한 최선 접근 fallback 갱신
            if (current == startCell || current.IsAvailable)
            {
                float h = HeuristicCost(current, goalCell);
                if (h <= bestCandidateH)
                {
                    bestCandidateH = h;
                    bestCandidate = current;
                }
            }

            for (int i = 0; i < CardinalDirections.Length; i++)
            {
                Vector3 dir = CardinalDirections[i];
                Cell neighbor = cellController.GetNeighborCell(current, dir);
                if (neighbor == null)
                    continue;

                if (closedSet.Contains(neighbor))
                    continue;

                // 요청사항: Cell의 isAvailable여부 확인해서 경로 계산
                if (!neighbor.IsAvailable)
                    continue;

                float stepCost = GetStepCostXZ(current, neighbor);
                float tentativeG = gScore[current] + stepCost;

                if (!openSetLookup.Contains(neighbor))
                {
                    cameFrom[neighbor] = current;
                    gScore[neighbor] = tentativeG;
                    fScore[neighbor] = tentativeG + HeuristicCost(neighbor, goalCell);
                    openSet.Add(neighbor);
                    openSetLookup.Add(neighbor);
                }
                else if (tentativeG < (gScore.TryGetValue(neighbor, out float oldG) ? oldG : float.PositiveInfinity))
                {
                    cameFrom[neighbor] = current;
                    gScore[neighbor] = tentativeG;
                    fScore[neighbor] = tentativeG + HeuristicCost(neighbor, goalCell);
                }
            }

            expansions++;
        }

        // goal까지 못 갔으면, bestCandidate까지의 경로를 반환 시도
        if (bestCandidate == null || bestCandidate == startCell)
            return null;

        return ReconstructPath(cameFrom, bestCandidate);
    }

    private float HeuristicCost(Cell a, Cell b)
    {
        // XZ 평면에서의 Manhattan 거리(대각선 이동은 없으므로 잘 맞는 휴리스틱)
        Vector3 pa = a.transform.position;
        Vector3 pb = b.transform.position;
        return Mathf.Abs(pa.x - pb.x) + Mathf.Abs(pa.z - pb.z);
    }

    private float GetStepCostXZ(Cell a, Cell b)
    {
        Vector3 pa = a.transform.position;
        Vector3 pb = b.transform.position;
        return Mathf.Abs(pa.x - pb.x) + Mathf.Abs(pa.z - pb.z);
    }

    private static List<Cell> ReconstructPath(Dictionary<Cell, Cell> cameFrom, Cell current)
    {
        List<Cell> totalPath = new List<Cell>();
        totalPath.Add(current);

        // startCell은 cameFrom에 없을 수 있음
        while (cameFrom.TryGetValue(current, out Cell prev))
        {
            current = prev;
            totalPath.Add(current);
        }

        totalPath.Reverse();
        return totalPath;
    }

    // Player처럼 입력 이벤트를 받아 이동시키고 싶을 때를 위한 핸들러(테스트용).
    public void HandleMove(InputAction.CallbackContext context)
    {
        if (!context.started)
            return;

        Vector2 moveInput = context.ReadValue<Vector2>();
        Vector3 moveDirection = GetCardinalDirection(moveInput);

        if (moveDirection == Vector3.zero)
            return;

        if (!TryGetMoveTargetCell(moveDirection, out Cell targetCell))
            return;

        TryStartRoll(moveDirection, targetCell);
    }

    private Vector3 GetCardinalDirection(Vector2 input)
    {
        if (input.sqrMagnitude < MinInputSqrMagnitude)
            return Vector3.zero;

        // 수평 / 수직 중 더 큰 쪽만 사용해서 대각선 제거
        if (Mathf.Abs(input.x) > Mathf.Abs(input.y))
            return input.x > 0 ? Vector3.right : Vector3.left;

        return input.y > 0 ? Vector3.forward : Vector3.back;
    }
}

