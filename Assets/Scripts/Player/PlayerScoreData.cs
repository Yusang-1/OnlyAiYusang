using UnityEngine;

[CreateAssetMenu(
    fileName = "PlayerScoreData",
    menuName = "OnlyAi/Score/Player Score Data")]
public class PlayerScoreData : ScriptableObject
{
    [Header("Runtime Scores")]
    [SerializeField] private int currentScore;
    [SerializeField] private int bestScore;

    public int CurrentScore => currentScore;
    public int BestScore => bestScore;
    public event System.Action<int, int> ScoreChanged;

    private void OnEnable()
    {
        // 저장/복구는 SaveSystem에서 담당합니다.
        currentScore = 0;
        bestScore = Mathf.Max(0, bestScore);
        RaiseScoreChanged();
    }

    public void SetScores(int currentScoreValue, int bestScoreValue)
    {
        currentScore = Mathf.Max(0, currentScoreValue);
        bestScore = Mathf.Max(0, bestScoreValue);
        RaiseScoreChanged();
    }

    public void ResetCurrentScore()
    {
        if (currentScore == 0)
        {
            RaiseScoreChanged();
            return;
        }

        currentScore = 0;
        RaiseScoreChanged();
    }

    public void AddScore(int amount)
    {
        if (amount <= 0)
            return;

        currentScore += amount;
        TryUpdateBestScore();
        RaiseScoreChanged();
    }

    public void SetCurrentScore(int value)
    {
        currentScore = Mathf.Max(0, value);
        TryUpdateBestScore();
        RaiseScoreChanged();
    }

    public void RefreshScoreState()
    {
        RaiseScoreChanged();
    }

    private void TryUpdateBestScore()
    {
        if (currentScore <= bestScore)
            return;

        bestScore = currentScore;
    }

    private void RaiseScoreChanged()
    {
        ScoreChanged?.Invoke(currentScore, bestScore);
    }
}
