using UnityEngine;

/// <summary>
/// 쿼터뷰(상단 대각) 시점에서 카메라가 타겟을 바라보며, 타겟이 화면 중앙에 오도록 위치/회전을 동기화합니다.
/// </summary>
public class QuarterViewCameraFollow : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("null이면 자동으로 PlayerController를 찾아서 사용합니다.")]
    [SerializeField] private Transform target;

    [SerializeField] private Vector3 lookAtOffset = new Vector3(0f, 0.5f, 0f);

    [Header("Quarter View Settings")]
    [SerializeField] private float distance = 10f;

    // yawDegrees를 180으로 두면 카메라의 시선 방향(+Z)이 화면의 '앞(위쪽)'과 일치해
    // PlayerController의 입력 방향(Vector3.forward/back/right/left)이 체감 이동 방향과 맞습니다.
    [Tooltip("Y축 기준 회전(도). +Z가 화면 '위'가 되도록 보통 180 사용")]
    [SerializeField] private float yawDegrees = 180f;

    [Tooltip("수평선 기준 고도(도). 예: 45면 대략 45도 내려다봄")]
    [SerializeField] private float pitchDegrees = 45f;

    [Header("Smoothing")]
    [SerializeField] private bool useSmoothing = true;

    [Tooltip("클수록 빠르게 따라옵니다 (위치).")]
    [SerializeField] private float positionDamping = 12f;

    [Tooltip("클수록 빠르게 회전합니다 (각도).")]
    [SerializeField] private float rotationDamping = 12f;

    [Header("Auto Find")]
    [SerializeField] private bool autoFindPlayerController = true;

    private Transform _resolvedTarget;
    [SerializeField] private float targetReacquireIntervalSeconds = 0.5f;
    private float _nextReacquireTime;

    private void Awake()
    {
        if (target != null)
        {
            _resolvedTarget = target;
            return;
        }

        if (!autoFindPlayerController)
            return;

        var player = FindFirstObjectByType<PlayerController>();
        if (player != null)
            _resolvedTarget = player.transform;
    }

    private void LateUpdate()
    {
        if (_resolvedTarget == null)
        {
            if (!autoFindPlayerController)
                return;

            if (Time.time < _nextReacquireTime)
                return;

            _nextReacquireTime = Time.time + Mathf.Max(targetReacquireIntervalSeconds, 0.01f);

            var player = FindFirstObjectByType<PlayerController>();
            if (player != null)
                _resolvedTarget = player.transform;
            else
                return;
        }

        Vector3 targetPos = _resolvedTarget.position + lookAtOffset;

        Vector3 desiredOffset = GetSphericalOffset(distance, yawDegrees, pitchDegrees);
        Vector3 desiredPos = targetPos + desiredOffset;

        Quaternion desiredRot = Quaternion.LookRotation(targetPos - desiredPos, Vector3.up);

        if (!useSmoothing)
        {
            transform.position = desiredPos;
            transform.rotation = desiredRot;
            return;
        }

        // 프레임 레이트에 덜 의존적인 감쇠 (exp smoothing)
        float posT = 1f - Mathf.Exp(-positionDamping * Time.deltaTime);
        float rotT = 1f - Mathf.Exp(-rotationDamping * Time.deltaTime);

        transform.position = Vector3.Lerp(transform.position, desiredPos, posT);
        transform.rotation = Quaternion.Slerp(transform.rotation, desiredRot, rotT);
    }

    private static Vector3 GetSphericalOffset(float dist, float yawDeg, float pitchDeg)
    {
        // pitchDeg: 수평선 기준 고도(elevation)
        float yawRad = yawDeg * Mathf.Deg2Rad;
        float pitchRad = pitchDeg * Mathf.Deg2Rad;

        // elevation 기준으로 XZ 반경과 Y 높이를 분해
        float cosPitch = Mathf.Cos(pitchRad);
        float sinPitch = Mathf.Sin(pitchRad);

        float x = dist * cosPitch * Mathf.Sin(yawRad);
        float y = dist * sinPitch;
        float z = dist * cosPitch * Mathf.Cos(yawRad);

        return new Vector3(x, y, z);
    }
}
