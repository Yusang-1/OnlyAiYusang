using UnityEngine;

public class GameStartUIController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameManager gameManager;
    [SerializeField] private GameObject startUIRoot;

    private void Awake()
    {
        if (startUIRoot == null)
            startUIRoot = gameObject;
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

    public void OnClickStartButton()
    {
        if (gameManager == null)
            gameManager = FindFirstObjectByType<GameManager>();

        if (gameManager == null)
        {
            Debug.LogError("GameStartUIController: GameManager를 찾지 못했습니다.");
            return;
        }

        gameManager.GameStart();
    }

    private void HandleGameStarted()
    {
        if (startUIRoot != null)
            startUIRoot.SetActive(false);
    }

    private void HandleGameOvered()
    {
        if (startUIRoot != null)
            startUIRoot.SetActive(true);
    }
}
