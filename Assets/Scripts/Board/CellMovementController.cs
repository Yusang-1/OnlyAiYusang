using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class CellMovementController : MonoBehaviour
{
    public enum YTargetMode
    {
        AbsoluteLocalY,
        RelativeToOriginY,
        RelativeToCurrentY
    }

    [Serializable]
    public sealed class MovementStep
    {
        [Tooltip("이동(첫 번째)으로 도달할 Y 값. 모드에 따라 절대/상대가 달라집니다.")]
        public float moveY = 1f;

        [Tooltip("흔들림(두 번째)에서 도달할 Y 값. 모드에 따라 절대/상대가 달라집니다.")]
        public float shakeY = 0f;

        [Tooltip("이동에 걸리는 시간(초). 0 이하이면 즉시 이동합니다.")]
        public float moveDuration = 0.25f;

        [Tooltip("이동 후 흔들림을 유지하는 시간(초). 0 이하이면 흔들림을 생략합니다.")]
        public float shakeDuration = 0.25f;

        [Tooltip("흔들림 주기(초당 횟수).")]
        public float shakeFrequency = 12f;

        [Tooltip("moveY / shakeY 값을 해석하는 기준.")]
        public YTargetMode targetMode = YTargetMode.RelativeToOriginY;

        [Tooltip("이동 동작에 사용할 보간 커브.")]
        public AnimationCurve moveCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Tooltip("흔들림 종료 시 shakeY로 딱 맞춰 끝날지 여부.")]
        public bool endAtShakeTarget = true;
    }

    [Serializable]
    public sealed class MovementSequence
    {
        public List<MovementStep> steps = new List<MovementStep>
        {
            new MovementStep()
        };

        [Tooltip("시퀀스 종료 후 원래 위치로 복귀하는 시간(초).")]
        public float returnDuration = 0.25f;

        [Tooltip("복귀 동작에 사용할 보간 커브.")]
        public AnimationCurve returnCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    }

    [Header("Bindings")]
    [SerializeField] private Cell _cell;

    [Header("Triggers")]
    [SerializeField] private bool runWhenBecameUnavailable = true;
    [SerializeField] private bool runWhenBecameAvailable = false;

    [Tooltip("Cell이 unavailable(비활성) 상태가 되었을 때 실행할 시퀀스입니다.")]
    [SerializeField] private MovementSequence unavailableSequence = new MovementSequence();

    [Tooltip("Cell이 available(활성) 상태가 되었을 때 실행할 시퀀스입니다.")]
    [SerializeField] private MovementSequence availableSequence = new MovementSequence();

    [Header("Optional: GameObject Disable")]
    [Tooltip("Unity의 GameObject 자체가 SetActive(false)로 비활성화될 때 Y를 오프셋할지 여부입니다.")]
    [SerializeField] private bool runWhenGameObjectDisabled = false;

    [Tooltip("GameObject 비활성화 시 오프셋할 Y 값(로컬). 예: 1이면 +1, -1이면 -1.")]
    [SerializeField] private float gameObjectDisableOffsetY = 1f;

    [Header("Availability Movement Override")]
    [Tooltip("AvailabilityChanged가 false로 바뀐 순간, 첫 이동은 이 값만큼 무조건 올립니다.")]
    [SerializeField] private float unavailableOffsetY = 1f;

    [Header("State")]
    [SerializeField] private bool resetOriginOnEnable = true;

    private Coroutine _movementRoutine;
    private Vector3 _originLocalPos;
    private bool _lastIsAvailable = true;
    private bool _hasPendingRestoreScheduled;
    private float _pendingRestoreSeconds;

    private void Awake()
    {
        if (_cell == null)
            _cell = GetComponent<Cell>();

        _originLocalPos = transform.localPosition;
    }

    private void OnEnable()
    {
        if (_cell == null)
        {
            _lastIsAvailable = true;
        }
        else
        {
            _lastIsAvailable = _cell.IsAvailable;
        }

        if (resetOriginOnEnable)
            _originLocalPos = transform.localPosition;

        if (_cell != null)
            _cell.AvailabilityChanged += HandleAvailabilityChanged;

        if (_cell != null)
            _cell.AvailabilityRestoreScheduled += HandleAvailabilityRestoreScheduled;

        // 이미 unavailable 상태라면 즉시 트리거(원하는 경우 위 옵션 조정)
        if (runWhenBecameUnavailable && _cell != null && !_cell.IsAvailable)
            Play(unavailableSequence);
    }

    private void OnDisable()
    {
        if (_cell != null)
            _cell.AvailabilityChanged -= HandleAvailabilityChanged;

        if (_cell != null)
            _cell.AvailabilityRestoreScheduled -= HandleAvailabilityRestoreScheduled;

        StopMovementRoutine();

        if (runWhenGameObjectDisabled)
        {
            // 예시 요구사항: cell이 비활성화되면 y축으로 1 이동
            transform.localPosition = transform.localPosition + Vector3.up * gameObjectDisableOffsetY;
        }
    }

    private void HandleAvailabilityChanged(Cell cell, bool isAvailable)
    {
        if (cell == null || cell != _cell)
            return;

        // true->false: unavailable 되었을 때만 실행 (또는 반대로는 runWhenBecameAvailable 사용)
        if (_lastIsAvailable == isAvailable)
            return;

        _lastIsAvailable = isAvailable;

        if (!isAvailable)
        {
            if (runWhenBecameUnavailable)
            {
                float restoreEndTime = Time.time;

                if (_hasPendingRestoreScheduled)
                {
                    float seconds = Mathf.Max(0f, _pendingRestoreSeconds);
                    restoreEndTime = Time.time + seconds;
                    _hasPendingRestoreScheduled = false;
                }

                PlayUnavailableSynced(unavailableSequence, restoreEndTime);
            }
        }
        else
        {
            if (runWhenBecameAvailable)
                Play(availableSequence);
        }
    }

    private void HandleAvailabilityRestoreScheduled(Cell cell, float secondsUntilAvailable)
    {
        if (cell == null || cell != _cell)
            return;

        _pendingRestoreSeconds = Mathf.Max(0f, secondsUntilAvailable);
        _hasPendingRestoreScheduled = true;

        // SetAvailable이 이미 unavailable 상태에서 다시 호출되는 경우,
        // AvailabilityChanged(false)는 발생하지 않을 수 있으므로 여기서 동기화를 갱신합니다.
        if (runWhenBecameUnavailable && !_cell.IsAvailable)
        {
            float restoreEndTime = Time.time + _pendingRestoreSeconds;
            _hasPendingRestoreScheduled = false;
            PlayUnavailableSynced(unavailableSequence, restoreEndTime);
        }
    }

    public void Play(MovementSequence sequence)
    {
        if (sequence == null || sequence.steps == null || sequence.steps.Count == 0)
            return;

        StopMovementRoutine();

        _movementRoutine = StartCoroutine(RunSequence(sequence));
    }

    private void PlayUnavailableSynced(MovementSequence sequence, float restoreEndTime)
    {
        if (sequence == null || sequence.steps == null || sequence.steps.Count == 0)
            return;

        StopMovementRoutine();
        _movementRoutine = StartCoroutine(RunSequenceSyncedToRestoreTime(sequence, restoreEndTime));
    }

    private void StopMovementRoutine()
    {
        if (_movementRoutine != null)
        {
            StopCoroutine(_movementRoutine);
            _movementRoutine = null;
        }
    }

    private IEnumerator RunSequenceSyncedToRestoreTime(MovementSequence sequence, float restoreEndTime)
    {
        // 현재 AvailabilityRestoreScheduled의 "복구 시점"과,
        // 마지막 복귀(원위치 도달) 시간을 최대한 동일하게 맞춥니다.
        float startTime = Time.time;
        float scheduledDuration = Mathf.Max(0f, restoreEndTime - startTime);

        // steps 시간을 복구 시간에 맞추기 위해 스케일(필요 시)을 적용합니다.
        float totalStepsSeconds = 0f;
        for (int i = 0; i < sequence.steps.Count; i++)
        {
            var step = sequence.steps[i];
            if (step == null)
                continue;

            totalStepsSeconds += Mathf.Max(0f, step.moveDuration);
            if (step.shakeDuration > 0f)
                totalStepsSeconds += step.shakeDuration;
        }

        float durationScale = 1f;
        if (totalStepsSeconds > 0f && totalStepsSeconds > scheduledDuration)
            durationScale = scheduledDuration / totalStepsSeconds;

        for (int i = 0; i < sequence.steps.Count; i++)
        {
            var step = sequence.steps[i];
            if (step == null)
                continue;

            float moveTargetY;
            if (i == 0)
            {
                // 요청사항: AvailabilityChanged가 실행되면 y축으로 1 이동(기본값은 +1)
                moveTargetY = _originLocalPos.y + unavailableOffsetY;
            }
            else
            {
                moveTargetY = ResolveTargetY(step.targetMode, step.moveY);
            }

            float moveDuration = Mathf.Max(0f, step.moveDuration) * durationScale;
            yield return AnimateY(moveTargetY, moveDuration, step.moveCurve);

            float shakeTargetY = ResolveTargetY(step.targetMode, step.shakeY);
            float shakeDuration = Mathf.Max(0f, step.shakeDuration) * durationScale;
            if (shakeDuration > 0f && !Mathf.Approximately(moveTargetY, shakeTargetY))
                yield return ShakeY(moveTargetY, shakeTargetY, shakeDuration, step.shakeFrequency, step.endAtShakeTarget);
        }

        // 남은 시간만큼 "원래 위치"로 복귀(복구 시점과 복귀 완료 시점을 맞춤)
        float returnDuration = Mathf.Max(0f, restoreEndTime - Time.time);
        yield return AnimateY(_originLocalPos.y, returnDuration, sequence.returnCurve);

        _movementRoutine = null;
    }

    private IEnumerator RunSequence(MovementSequence sequence)
    {
        // 1) 여러 단계 실행 가능(확장성 요구사항)
        for (int i = 0; i < sequence.steps.Count; i++)
        {
            var step = sequence.steps[i];
            if (step == null)
                continue;

            float moveTargetY = ResolveTargetY(step.targetMode, step.moveY);
            yield return AnimateY(moveTargetY, step.moveDuration, step.moveCurve);

            float shakeTargetY = ResolveTargetY(step.targetMode, step.shakeY);
            if (step.shakeDuration > 0f && !Mathf.Approximately(moveTargetY, shakeTargetY))
                yield return ShakeY(moveTargetY, shakeTargetY, step.shakeDuration, step.shakeFrequency, step.endAtShakeTarget);
        }

        // 3) 이동 끝나면 원래 위치로 복귀
        yield return AnimateY(_originLocalPos.y, sequence.returnDuration, sequence.returnCurve);
        _movementRoutine = null;
    }

    private float ResolveTargetY(YTargetMode mode, float value)
    {
        switch (mode)
        {
            case YTargetMode.AbsoluteLocalY:
                return value;
            case YTargetMode.RelativeToOriginY:
                return _originLocalPos.y + value;
            case YTargetMode.RelativeToCurrentY:
            default:
                return transform.localPosition.y + value;
        }
    }

    private IEnumerator AnimateY(float targetY, float duration, AnimationCurve curve)
    {
        Vector3 pos = transform.localPosition;
        float startY = pos.y;

        if (duration <= 0f)
        {
            pos.y = targetY;
            transform.localPosition = pos;
            yield break;
        }

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float normalized = Mathf.Clamp01(t / duration);
            float eased = curve != null ? curve.Evaluate(normalized) : normalized;
            pos.y = Mathf.LerpUnclamped(startY, targetY, eased);
            transform.localPosition = pos;
            yield return null;
        }

        pos.y = targetY;
        transform.localPosition = pos;
    }

    private IEnumerator ShakeY(float fromY, float toY, float duration, float frequencyHz, bool endAtTarget)
    {
        Vector3 pos = transform.localPosition;
        float halfRange = Mathf.Abs(toY - fromY) * 0.5f;
        float center = (fromY + toY) * 0.5f;

        float t = 0f;
        float omega = Mathf.PI * 2f * Mathf.Max(0f, frequencyHz);
        while (t < duration)
        {
            t += Time.deltaTime;
            // -1..1 -> 0..1 변환 대신 center + sin 방식
            float y = center + Mathf.Sin(t * omega) * halfRange;
            pos.y = y;
            transform.localPosition = pos;
            yield return null;
        }

        if (endAtTarget)
        {
            pos.y = toY;
            transform.localPosition = pos;
        }
        else
        {
            pos.y = fromY;
            transform.localPosition = pos;
        }
    }
}

