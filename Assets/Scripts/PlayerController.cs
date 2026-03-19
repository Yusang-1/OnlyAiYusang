using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float rollSpeed = 360f;    // 한 번 구를 때 회전 속도(도/초)
    [SerializeField] private float rollDelay = 0.1f;    // 한 번 구른 후 다음 구르기까지 대기 시간(초)

    private Vector2 _moveInput;       // WASD 입력 (Vector2)
    private Vector3 _moveDirection;   // 실제 이동 방향 (대각선 제거 후)
    private Rigidbody _rb;
    private bool _isRolling;
    private float _lastRollEndTime;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
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

        _moveInput = context.ReadValue<Vector2>();
        _moveDirection = GetCardinalDirection(_moveInput);

        if (_moveDirection == Vector3.zero)
            return;

        TryStartRoll(_moveDirection);
    }

    /// <summary>
    /// 현재 상태와 딜레이를 고려하여 구르기 시작을 시도
    /// </summary>
    private void TryStartRoll(Vector3 direction)
    {
        if (_isRolling)
            return;

        if (Time.time - _lastRollEndTime < rollDelay)
            return;

        StartCoroutine(RollCoroutine(direction));
    }

    /// <summary>
    /// 큐브의 바닥면 중 이동 방향 모서리를 기준으로 90도 회전하면서 구르는 코루틴
    /// </summary>
    private System.Collections.IEnumerator RollCoroutine(Vector3 direction)
    {
        _isRolling = true;

        // 큐브 크기에 맞춰 롤 이동량/회전 중심을 계산
        // (기본 큐브는 BoxCollider size가 (1,1,1)이므로 step=1이 됩니다.)
        BoxCollider box = GetComponent<BoxCollider>();
        Vector3 colliderSize = box != null ? Vector3.Scale(box.size, transform.lossyScale) : Vector3.one;

        float halfY = colliderSize.y * 0.5f;
        float halfX = colliderSize.x * 0.5f;
        float halfZ = colliderSize.z * 0.5f;

        float stepAlong = direction.x != 0f ? colliderSize.x : colliderSize.z;
        float halfAlong = direction.x != 0f ? halfX : halfZ;

        // 회전 중심(모서리) 계산:
        // - 바닥면 기준 아래 halfY
        // - 이동 방향의 해당 면 모서리(halfAlong) 위치
        Vector3 startPos = transform.position;
        Vector3 pivot = startPos
                       + Vector3.down * halfY
                       + direction * halfAlong;

        // 회전 축:
        // +Z로 구르는 경우 axis가 +X가 되어야 z가 0->1로 이동합니다.
        Vector3 axis = Vector3.Cross(Vector3.up, direction).normalized;

        float remainingAngle = 90f;

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
        transform.position = expectedPos;

        _lastRollEndTime = Time.time;
        _isRolling = false;
    }

    /// <summary>
    /// 입력 벡터를 상/하/좌/우 중 하나로만 변환하여 대각선 이동 방지
    /// </summary>
    private Vector3 GetCardinalDirection(Vector2 input)
    {
        if (input.sqrMagnitude < 0.01f)
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
