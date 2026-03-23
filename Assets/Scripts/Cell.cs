using UnityEngine;
using System.Collections;
using System;

public class Cell : MonoBehaviour
{
    // 해당 셀로 이동 가능 여부
    private bool isAvailable = true;

    private Coroutine _restoreCoroutine;

    [Header("Cell Colors")]
    [SerializeField] private Color availableColor = Color.white;
    [SerializeField] private Color unavailableColor = Color.red;
    private CellColorApplier _colorApplier;

    private Renderer _renderer;

    public event Action<Cell, bool> AvailabilityChanged;

    // coin 상태 저장 (CoinController가 추가/제거, PlayerController가 획득)
    private bool _hasCoin;

    public bool HasCoin => _hasCoin;

    // coin이 획득될 때만 발생합니다.
    public event Action<Cell> CoinCollected;

    // 외부에서는 읽기만, 변경은 Cell 내부 메서드로만
    public bool IsAvailable
    {
        get => isAvailable;
        private set
        {
            if (isAvailable == value)
                return;

            isAvailable = value;
            ApplyColor();
            AvailabilityChanged?.Invoke(this, isAvailable);
        }
    }

    private void Awake()
    {
        _renderer = GetComponentInChildren<Renderer>(true);
        _colorApplier = new CellColorApplier();
        _colorApplier.Initialize(_renderer, availableColor, unavailableColor);
    }

    // 셀 접근 가능 여부를 변경하는 공용 메서드
    public void SetAvailable(float secondsUntilAvailable)
    {
        // 요청사항: SetAvailable 호출 시 isAvailable은 'false'로만 바뀌며,
        // 일정 시간 이후 자동으로 true로 복구된다.
        IsAvailable = false;

        if (_restoreCoroutine != null)
            StopCoroutine(_restoreCoroutine);

        _restoreCoroutine = StartCoroutine(RestoreAvailableAfterSeconds(secondsUntilAvailable));
    }

    private const float ZeroSeconds = 0f;

    private IEnumerator RestoreAvailableAfterSeconds(float secondsUntilAvailable)
    {
        float delaySeconds = Mathf.Max(secondsUntilAvailable, ZeroSeconds);
        if (delaySeconds > ZeroSeconds)
            yield return new WaitForSeconds(delaySeconds);

        IsAvailable = true;
        _restoreCoroutine = null;
    }

    private void ApplyColor()
    {
        if (_colorApplier == null)
            return;

        _colorApplier.SetAvailable(isAvailable, availableColor, unavailableColor);
    }

    // CoinController가 coin의 존재 여부를 "표시"할 때 사용합니다.
    public void SetCoinPresence(bool hasCoin)
    {
        _hasCoin = hasCoin;
    }

    // PlayerController가 획득 처리할 때 사용합니다.
    // 성공(true)일 때만 CoinCollected 이벤트가 발생합니다.
    public bool TryCollectCoin()
    {
        if (!_hasCoin)
            return false;

        _hasCoin = false;
        CoinCollected?.Invoke(this);
        return true;
    }

    private void Start()
    {
        // Awake에서 applier 준비 후, 초기 상태 반영
        ApplyColor();
    }
}
