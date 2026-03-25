using UnityEngine;

/// <summary>
/// 쿼터뷰(상단 대각) 시점에서 카메라가 타겟을 바라보며, 타겟이 화면 중앙에 오도록 위치/회전을 동기화합니다.
/// </summary>
public class QuarterViewCameraFollow : MonoBehaviour
{
    [Header("Game State")]
    [Tooltip("GameStart 동안에만 카메라를 따라가도록 합니다.")]
    [SerializeField] private bool followOnlyWhenGameStarted = true;

    [Tooltip("참조가 없으면 씬에서 자동으로 찾습니다.")]
    [SerializeField] private GameManager gameManager;

    [Header("State Transition")]
    [Tooltip("GameOver 시 기본 카메라 위치/회전으로 돌아갈 때의 전환 시간(초)입니다.")]
    [SerializeField] private float returnToDefaultDuration = 0.35f;

    [Tooltip("GameStart 직후 카메라가 즉시 점프하는 것을 방지하기 위해, 초기 스무딩을 강제할 시간(초)입니다.")]
    [SerializeField] private float startFollowSmoothingOverrideDuration = 0.35f;

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

    private Vector3 _defaultPosition;
    private Quaternion _defaultRotation;
    private bool _isFollowing;

    private bool _isReturningToDefault;
    private float _returnElapsed;
    private Vector3 _returnFromPosition;
    private Quaternion _returnFromRotation;

    private float _smoothFollowOverrideEndsAt;

    private void Awake()
    {
        // GameOver 시 되돌릴 기본 카메라 Transform(씬에 세팅된 값)을 캐시합니다.
        _defaultPosition = transform.position;
        _defaultRotation = transform.rotation;

        if (target != null)
        {
            _resolvedTarget = target;
            return;
        }

        if (gameManager == null)
            gameManager = FindFirstObjectByType<GameManager>();

        _isFollowing = !followOnlyWhenGameStarted || (gameManager != null && gameManager.IsGameStarted);

        if (!autoFindPlayerController)
            return;

        var player = FindFirstObjectByType<PlayerController>();
        if (player != null)
            _resolvedTarget = player.transform;
    }

    private void OnEnable()
    {
        if (gameManager == null)
            gameManager = FindFirstObjectByType<GameManager>();

        if (gameManager == null)
            return;

        gameManager.GameStarted += HandleGameStarted;
        gameManager.GameOvered += HandleGameOvered;
    }

    private void OnDisable()
    {
        if (gameManager == null)
            return;

        gameManager.GameStarted -= HandleGameStarted;
        gameManager.GameOvered -= HandleGameOvered;
    }

    private void HandleGameStarted()
    {
        if (!followOnlyWhenGameStarted)
            return;

        // GameOver 복귀 전환 중이면 중단하고 바로 따라가기 시작합니다.
        _isReturningToDefault = false;
        _returnElapsed = 0f;

        // useSmoothing이 꺼져있어도 시작 직후에는 보간/스무딩을 강제합니다.
        _smoothFollowOverrideEndsAt = Time.time + Mathf.Max(startFollowSmoothingOverrideDuration, 0f);

        _isFollowing = true;
    }

    private void HandleGameOvered()
    {
        if (!followOnlyWhenGameStarted)
            return;

        _returnFromPosition = transform.position;
        _returnFromRotation = transform.rotation;

        _isFollowing = false;
        _smoothFollowOverrideEndsAt = 0f;

        // GameOver에서는 기본 카메라 위치/회전으로 "부드럽게" 되돌립니다.
        _isReturningToDefault = true;
        _returnElapsed = 0f;
    }

    private void LateUpdate()
    {
        if (followOnlyWhenGameStarted && !_isFollowing)
        {
            if (_isReturningToDefault)
            {
                float duration = Mathf.Max(returnToDefaultDuration, 0.0001f);
                _returnElapsed += Time.deltaTime;
                float t = Mathf.Clamp01(_returnElapsed / duration);
                float easedT = Mathf.SmoothStep(0f, 1f, t);

                transform.position = Vector3.Lerp(_returnFromPosition, _defaultPosition, easedT);
                transform.rotation = Quaternion.Slerp(_returnFromRotation, _defaultRotation, easedT);

                if (t >= 1f)
                    _isReturningToDefault = false;
            }

            return;
        }

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

        bool smoothingThisFrame = useSmoothing || Time.time < _smoothFollowOverrideEndsAt;
        if (!smoothingThisFrame)
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
