using System.Collections;
using UnityEngine;

public abstract class EnemyControllerBase : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] protected float rollSpeed = 360f; // 한 번 구를 때 회전 속도(도/초)
    [SerializeField] protected float rollDelay = 0.1f; // 한 번 구른 후 다음 구르기까지 대기 시간(초)

    [Header("Grid (Cell Availability)")]
    [SerializeField] protected CellController cellController;

    [Header("AI Tick")]
    [SerializeField] protected float aiStepInterval = 0.6f;

    private Rigidbody _rb;
    private BoxCollider _boxCollider;
    private bool _isRolling;
    private float _lastRollEndTime;

    private Coroutine _aiLoop;

    // 상태머신이 런타임에 켜고/끄는 AI 활성 플래그.
    // false이면 이동(새 롤)만 막고, 현재 굴림은 끝나도록(_isRolling 기준) 둡니다.
    private bool _runtimeAIEnabled = false;

    public bool IsRolling => _isRolling;

    public void SetRuntimeAIEnabled(bool enabled)
    {
        _runtimeAIEnabled = enabled;
    }

    protected static readonly Vector3[] CardinalDirections =
    {
        Vector3.forward,
        Vector3.back,
        Vector3.right,
        Vector3.left
    };

    private const float RollAngleDegrees = 90f;
    private const float HalfFactor = 0.5f;

    protected virtual void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _boxCollider = GetComponent<BoxCollider>();

        if (cellController == null)
            cellController = FindFirstObjectByType<CellController>();

        if (_rb != null)
        {
            // 물리 이동 대신 회전만 사용할 것이므로, 물리 시뮬레이션은 비활성화
            _rb.isKinematic = true;
        }
    }

    private void OnEnable()
    {
        if (_aiLoop != null)
            StopCoroutine(_aiLoop);

        _aiLoop = StartCoroutine(EnemyLoop());
    }

    private void OnDisable()
    {
        if (_aiLoop != null)
            StopCoroutine(_aiLoop);

        _aiLoop = null;
    }

    private IEnumerator EnemyLoop()
    {
        while (true)
        {
            if (!_isRolling)
            {
                if (_runtimeAIEnabled && IsAIModeEnabled())
                {
                    if (TryGetCurrentCell(out Cell startCell))
                    {
                        if (TryGetNextTargetCell(startCell, out Cell nextCell))
                        {
                            if (nextCell != null && nextCell.IsAvailable)
                            {
                                Vector3 moveDirection = GetDirectionToNeighbor(startCell, nextCell);
                                if (moveDirection != Vector3.zero)
                                    TryStartRoll(moveDirection, nextCell);
                            }
                        }
                    }
                }
            }

            yield return new WaitForSeconds(Mathf.Max(aiStepInterval, 0.01f));
        }
    }

    protected bool TryGetCurrentCell(out Cell currentCell)
    {
        currentCell = null;

        if (cellController == null)
            return false;

        return cellController.TryGetCellAtWorldPosition(transform.position, out currentCell);
    }

    protected bool TryGetCellAtWorldPosition(Vector3 worldPosition, out Cell cell)
    {
        cell = null;

        if (cellController == null)
            return false;

        return cellController.TryGetCellAtWorldPosition(worldPosition, out cell);
    }

    protected Vector3 GetDirectionToNeighbor(Cell from, Cell to)
    {
        if (cellController == null || from == null || to == null)
            return Vector3.zero;

        for (int i = 0; i < CardinalDirections.Length; i++)
        {
            Vector3 dir = CardinalDirections[i];
            Cell neighbor = cellController.GetNeighborCell(from, dir);
            if (neighbor == to)
                return dir;
        }

        return Vector3.zero;
    }

    protected bool TryGetMoveTargetCell(Vector3 direction, out Cell targetCell)
    {
        targetCell = null;

        if (cellController == null)
            return false;

        if (!cellController.TryGetCellAtWorldPosition(transform.position, out Cell currentCell))
            return false;

        targetCell = cellController.GetNeighborCell(currentCell, direction);
        if (targetCell == null)
            return false;

        return targetCell.IsAvailable;
    }

    protected abstract bool IsAIModeEnabled();

    // startCell은 현재 위치 셀입니다. nextCell은 base에서 direction 변환 및 롤 실행까지 담당합니다.
    protected abstract bool TryGetNextTargetCell(Cell startCell, out Cell nextCell);

    protected void TryStartRoll(Vector3 direction, Cell targetCell)
    {
        if (_isRolling)
            return;

        if (Time.time - _lastRollEndTime < rollDelay)
            return;

        _isRolling = true;
        StartCoroutine(RollCoroutine(direction, targetCell));
    }

    /// <summary>
    /// 큐브의 바닥면 중 이동 방향 모서리를 기준으로 90도 회전하면서 구르는 코루틴
    /// (PlayerController와 동일한 방식)
    /// </summary>
    private IEnumerator RollCoroutine(Vector3 direction, Cell targetCell)
    {
        try
        {
            Vector3 startPos = transform.position;

            // 큐브 크기에 맞춰 롤 이동량/회전 중심을 계산
            // (기본 큐브는 BoxCollider size가 (1,1,1)이므로 step=1이 됩니다.)
            Vector3 colliderSize = _boxCollider != null
                ? Vector3.Scale(_boxCollider.size, transform.lossyScale)
                : Vector3.one;

            float halfY = colliderSize.y * HalfFactor;
            float halfX = colliderSize.x * HalfFactor;
            float halfZ = colliderSize.z * HalfFactor;

            float stepAlong = direction.x != 0f ? colliderSize.x : colliderSize.z;
            float halfAlong = direction.x != 0f ? halfX : halfZ;

            // 회전 중심(모서리) 계산:
            // - 바닥면 기준 아래 halfY
            // - 이동 방향의 해당 면 모서리(halfAlong) 위치
            Vector3 pivot = startPos
                               + Vector3.down * halfY
                               + direction * halfAlong;

            // 회전 축:
            // +Z로 구르는 경우 axis가 +X가 되어야 z가 0->1로 이동합니다.
            Vector3 axis = Vector3.Cross(Vector3.up, direction).normalized;

            float remainingAngle = RollAngleDegrees;

            while (remainingAngle > 0f)
            {
                float step = rollSpeed * Time.deltaTime;
                if (step > remainingAngle)
                    step = remainingAngle;

                transform.RotateAround(pivot, axis, step);
                remainingAngle -= step;

                yield return null;
            }

            // 수치 오차로 인한 위치 드리프트 방지:
            // 최종적으로는 "XZ 평면에서만" 이동하도록 x,z를 스냅합니다.
            Vector3 expectedPos = startPos + direction * stepAlong;
            expectedPos.y = startPos.y;

            // 이동 시작 시점에선 가능했더라도, 롤 중에 셀이 비활성화될 수 있으니
            // 마지막에 한번 더 확인합니다.
            if (targetCell != null && !targetCell.IsAvailable)
                transform.position = startPos;
            else
                transform.position = expectedPos;
        }
        finally
        {
            _lastRollEndTime = Time.time;
            _isRolling = false;
        }
    }
}

