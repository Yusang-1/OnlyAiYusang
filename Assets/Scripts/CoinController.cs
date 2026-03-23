using System.Collections;
using UnityEngine;

public class CoinController : MonoBehaviour
{
    [Header("Coin Prefab")]
    [SerializeField] private GameObject coinPrefab;

    [Header("Placement")]
    [SerializeField] private float yOffset = 0.05f;
    [SerializeField] private bool placeOnAvailableCells = true;

    [Header("Relocation")]
    [Tooltip("일정 시간 이상 획득되지 않으면 코인을 다른 셀로 이동합니다.")]
    [SerializeField] private float moveAfterUncollectedSeconds = 6f;

    [SerializeField] private int fallbackAttempts = 10;

    private CellController _cellController;
    private Cell _currentCell;
    private GameObject _coinInstance;

    private Coroutine _timeoutCoroutine;

    private void Start()
    {
        if (coinPrefab == null)
        {
            Debug.LogError("CoinController: coinPrefab이 지정되지 않았습니다.");
            return;
        }

        // 게임 시작 시 코인을 한 번 생성해두고, 위치/활성 여부만 관리합니다.
        _coinInstance = Instantiate(coinPrefab, transform);
        _coinInstance.SetActive(false);

        StartCoroutine(SetupAndRunRoutine());
    }

    private IEnumerator SetupAndRunRoutine()
    {
        while (true)
        {
            if (_cellController == null)
                _cellController = FindFirstObjectByType<CellController>();

            if (_cellController != null && _cellController.IsReadyForCoin)
                break;

            yield return null;
        }

        MoveCoinToRandomCell();
    }

    private void OnDisable()
    {
        CleanupCurrentCellSubscription();

        if (_timeoutCoroutine != null)
            StopCoroutine(_timeoutCoroutine);

        _timeoutCoroutine = null;
    }

    private void CleanupCurrentCellSubscription()
    {
        if (_currentCell == null)
            return;

        _currentCell.CoinCollected -= HandleCoinCollected;
        _currentCell.SetCoinPresence(false);
        _currentCell = null;
    }

    private void MoveCoinToRandomCell()
    {
        if (_cellController == null)
            return;

        Cell previousCell = _currentCell;
        CleanupCurrentCellSubscription();

        Cell nextCell = null;

        // available 셀에서 먼저 시도, 실패하면 전체에서 시도합니다.
        if (!_TryPickCell(ref nextCell, placeOnAvailableCells, previousCell))
            _TryPickCell(ref nextCell, false, previousCell);

        if (nextCell == null)
        {
            if (_coinInstance != null)
                _coinInstance.SetActive(false);

            return;
        }

        _currentCell = nextCell;
        _currentCell.SetCoinPresence(true);
        _currentCell.CoinCollected += HandleCoinCollected;

        if (_coinInstance != null)
        {
            Vector3 pos = _currentCell.transform.position + Vector3.up * yOffset;
            _coinInstance.transform.position = pos;
            _coinInstance.SetActive(true);
        }

        ScheduleTimeoutRelocation();
    }

    private bool _TryPickCell(ref Cell outCell, bool mustBeAvailable, Cell forbiddenCell)
    {
        int attempts = Mathf.Max(fallbackAttempts, 1);
        for (int attempt = 0; attempt < attempts; attempt++)
        {
            if (_cellController.TryGetRandomCell(out Cell candidate, mustBeAvailable))
            {
                if (candidate != null && candidate != forbiddenCell)
                {
                    outCell = candidate;
                    return true;
                }
            }
        }

        outCell = null;
        return false;
    }

    private void ScheduleTimeoutRelocation()
    {
        if (_timeoutCoroutine != null)
            StopCoroutine(_timeoutCoroutine);

        _timeoutCoroutine = StartCoroutine(TimeoutRelocationRoutine());
    }

    private IEnumerator TimeoutRelocationRoutine()
    {
        float waitSeconds = Mathf.Max(moveAfterUncollectedSeconds, 0.01f);
        yield return new WaitForSeconds(waitSeconds);

        if (_currentCell == null)
            yield break;

        // 시간이 지나도 coin이 여전히 존재하면 다른 셀로 이동합니다.
        if (_currentCell.HasCoin)
            MoveCoinToRandomCell();
    }

    private void HandleCoinCollected(Cell cell)
    {
        if (cell == null || cell != _currentCell)
            return;

        // 획득 직후에는 바로 다른 위치로 이동합니다.
        if (_coinInstance != null)
            _coinInstance.SetActive(false);

        MoveCoinToRandomCell();
    }
}

