using UnityEngine;

public class GameManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GroundGridGenerator groundGridGenerator;
    [SerializeField] private EnemySpawner enemySpawner;
    [SerializeField] private CellController cellController;
    [SerializeField] private CoinController coinController;
    [SerializeField] private bool autoStartOnAwake = true;

    private bool _isGameStarted;

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
    }

    public void GameOver()
    {
        if (!_isGameStarted)
            return;

        if (cellController != null)
            cellController.ResetControllerState();

        if (coinController != null)
            coinController.ResetControllerState();

        if (enemySpawner != null)
            enemySpawner.ClearSpawnedEnemies();

        if (groundGridGenerator != null)
            groundGridGenerator.ClearGrid();

        _isGameStarted = false;
    }

    public void RestartGame()
    {
        GameOver();
        GameStart();
    }
}
