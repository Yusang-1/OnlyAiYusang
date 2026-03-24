using UnityEngine;

[CreateAssetMenu(
    fileName = "PlayerScoreData",
    menuName = "OnlyAi/Score/Player Score Data")]
public class PlayerScoreData : ScriptableObject
{
    private const string BestScoreKey = "OnlyAi.BestScore";

    [Header("Runtime Scores")]
    [SerializeField] private int currentScore;
    [SerializeField] private int bestScore;

    public int CurrentScore => currentScore;
    public int BestScore => bestScore;
    public event System.Action<int, int> ScoreChanged;

    private void OnEnable()
    {
        LoadBestScore();
        currentScore = 0;
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
        SaveBestScore();
    }

    private void LoadBestScore()
    {
        bestScore = Mathf.Max(0, PlayerPrefs.GetInt(BestScoreKey, 0));
    }

    private void SaveBestScore()
    {
        PlayerPrefs.SetInt(BestScoreKey, bestScore);
        PlayerPrefs.Save();
    }

    private void RaiseScoreChanged()
    {
        ScoreChanged?.Invoke(currentScore, bestScore);
    }
}
