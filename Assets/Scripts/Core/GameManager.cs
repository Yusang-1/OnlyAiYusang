using System;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public event Action GameStarted;
    public event Action GameOvered;

    [Header("References")]
    [SerializeField] private GroundGridGenerator groundGridGenerator;
    [SerializeField] private EnemySpawner enemySpawner;
    [SerializeField] private CellController cellController;
    [SerializeField] private CoinController coinController;
    [SerializeField] private SaveManager saveManager;
    [SerializeField] private PlayerController playerController;
    [SerializeField] private bool autoStartOnAwake = true;

    private bool _isGameStarted;

    public bool IsGameStarted => _isGameStarted;

    private void Awake()
    {
        if (autoStartOnAwake)
        {
            GameStart();
        }
    }

    public void GameStart()
    {
        if (_isGameStarted)
            return;

        SaveManager sm = saveManager != null ? saveManager : SaveManager.Instance;
        if (sm != null)
            sm.LoadFromPlayerPrefs();
        else
            Debug.LogWarning("GameManager: SaveManager를 찾지 못했습니다.");

        if (groundGridGenerator == null)
        {
            Debug.LogError("GameManager: GroundGridGenerator 참조가 비어 있습니다.");
            return;
        }

        if (enemySpawner == null)
        {
            Debug.LogError("GameManager: EnemySpawner 참조가 비어 있습니다.");
            return;
        }

        groundGridGenerator.GenerateGrid();

        if (!groundGridGenerator.IsBuilt || groundGridGenerator.Cells == null)
        {
            Debug.LogError("GameManager: 그리드 생성에 실패하여 에너미 생성을 중단합니다.");
            return;
        }

        enemySpawner.SpawnEnemiesFromGrid(groundGridGenerator.Cells);

        if (coinController != null)
            coinController.StartCoinPlacement();

        _isGameStarted = true;
        GameStarted?.Invoke();
    }

    public void GameOver()
    {
        if (!_isGameStarted)
            return;

        // 게임 오버 시 플레이어를 기본 좌표로 리셋합니다.
        var pc = playerController != null ? playerController : FindFirstObjectByType<PlayerController>();
        pc?.ResetToPosition(new Vector3(0f, 1f, 0f));

        SaveManager sm = saveManager != null ? saveManager : SaveManager.Instance;
        if (sm != null)
            sm.Save();

        if (cellController != null)
            cellController.ResetControllerState();

        if (coinController != null)
            coinController.ResetControllerState();

        if (enemySpawner != null)
            enemySpawner.ClearSpawnedEnemies();

        if (groundGridGenerator != null)
            groundGridGenerator.ClearGrid();

        _isGameStarted = false;
        GameOvered?.Invoke();
    }

    public void RestartGame()
    {
        GameOver();
        GameStart();
    }
}
