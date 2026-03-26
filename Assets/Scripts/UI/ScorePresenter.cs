using TMPro;
using UnityEngine;

public class ScorePresenter : MonoBehaviour
{
    [Header("Model")]
    [SerializeField] private PlayerScoreData scoreModel;

    [Header("View")]
    [SerializeField] private TextMeshProUGUI currentScoreText;
    [SerializeField] private TextMeshProUGUI bestScoreText;
    [SerializeField] private string currentScoreFormat = "Score: {0}";
    [SerializeField] private string bestScoreFormat = "Best: {0}";

    private void OnEnable()
    {
        if (scoreModel == null)
        {
            Debug.LogWarning("ScorePresenter: scoreModel이 할당되지 않았습니다.");
            return;
        }

        scoreModel.ScoreChanged += HandleScoreChanged;
        scoreModel.RefreshScoreState();
    }

    private void OnDisable()
    {
        if (scoreModel != null)
            scoreModel.ScoreChanged -= HandleScoreChanged;
    }

    private void HandleScoreChanged(int currentScore, int bestScore)
    {
        if (currentScoreText != null)
            currentScoreText.text = string.Format(currentScoreFormat, currentScore);

        if (bestScoreText != null)
            bestScoreText.text = string.Format(bestScoreFormat, bestScore);
    }
}
