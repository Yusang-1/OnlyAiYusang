using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float rollSpeed = 360f;    // 한 번 구를 때 회전 속도(도/초)
    [SerializeField] private float rollDelay = 0.1f;    // 한 번 구른 후 다음 구르기까지 대기 시간(초)

    [Header("Grid (Cell Availability)")]
    [SerializeField] private CellController cellController;

    [Header("Score (Coin)")]
    [SerializeField] private int score = 0;
    public int Score => score;

    private Rigidbody _rb;
    private BoxCollider _boxCollider;
    private bool _isRolling;
    private float _lastRollEndTime;

    // 하드코딩 숫자를 상수로 분리 (규칙 준수)
    private const float RollAngleDegrees = 90f;
    private const float HalfFactor = 0.5f;
    private const float MinInputSqrMagnitude = 0.01f;

    private void Awake()
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

    /// <summary>
    /// PlayerInput 컴포넌트가 Invoke Unity Events 모드일 때,
    /// Move 액션에 연결해서 사용하는 Unity Event 핸들러.
    /// </summary>
    public void HandleMove(InputAction.CallbackContext context)
    {
        // "키를 누른" 시점(Started)에만 롤을 시작
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

    private bool TryGetMoveTargetCell(Vector3 direction, out Cell targetCell)
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

    /// <summary>
    /// 현재 상태와 딜레이를 고려하여 구르기 시작을 시도
    /// </summary>
    private void TryStartRoll(Vector3 direction, Cell targetCell)
    {
        if (_isRolling)
            return;

        if (Time.time - _lastRollEndTime < rollDelay)
            return;

        // 코루틴 시작 직전까지는 _isRolling이 true가 아니므로,
        // 같은 프레임에 Started 이벤트가 중복 들어올 경우 다중 코루틴이 생길 수 있음.
        // 이를 방지하기 위해 선점 처리한다.
        _isRolling = true;
        StartCoroutine(RollCoroutine(direction, targetCell));
    }

    /// <summary>
    /// 큐브의 바닥면 중 이동 방향 모서리를 기준으로 90도 회전하면서 구르는 코루틴
    /// </summary>
    private System.Collections.IEnumerator RollCoroutine(Vector3 direction, Cell targetCell)
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
            bool movedToTargetCell = !(targetCell != null && !targetCell.IsAvailable);
            transform.position = movedToTargetCell ? expectedPos : startPos;

            if (movedToTargetCell && targetCell != null && targetCell.HasCoin)
            {
                if (targetCell.TryCollectCoin())
                    score += 1;
            }
        }
        finally
        {
            _lastRollEndTime = Time.time;
            _isRolling = false;
        }
    }

    /// <summary>
    /// 입력 벡터를 상/하/좌/우 중 하나로만 변환하여 대각선 이동 방지
    /// </summary>
    private Vector3 GetCardinalDirection(Vector2 input)
    {
        if (input.sqrMagnitude < MinInputSqrMagnitude)
            return Vector3.zero;

        // 수평 / 수직 중 더 큰 쪽만 사용해서 대각선 제거
        if (Mathf.Abs(input.x) > Mathf.Abs(input.y))
        {
            return input.x > 0 ? Vector3.right : Vector3.left;
        }
        else
        {
            return input.y > 0 ? Vector3.forward : Vector3.back;
        }
    }
}
