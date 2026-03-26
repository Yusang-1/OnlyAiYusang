using UnityEngine;

[DisallowMultipleComponent]
public class PlayerScoreSaveable : MonoBehaviour, ISaveable
{
    [Header("Score Model")]
    [SerializeField] private PlayerScoreData scoreData;

    [Header("Restore")]
    [Tooltip("저장된 lastScore를 currentScore로 복구할지 여부. 일반적으로는 꺼서 새 게임에서 0부터 시작합니다.")]
    [SerializeField] private bool restoreCurrentScoreFromLastScore = false;

    // SaveManager에서 사용할 고유 키
    public string SaveKey => "PlayerScore";

    private void OnEnable()
    {
        EnsureRegistered();
    }

    private void Start()
    {
        EnsureRegistered();
    }

    private void OnDisable()
    {
        if (SaveManager.Instance != null)
            SaveManager.Instance.Unregister(this);
    }

    private void EnsureRegistered()
    {
        if (scoreData == null)
        {
            Debug.LogWarning("PlayerScoreSaveable: scoreData가 지정되지 않았습니다.");
            return;
        }

        if (SaveManager.Instance != null)
            SaveManager.Instance.Register(this);
    }

    [System.Serializable]
    private class PlayerScoreSaveState
    {
        public int currentScore;
        public int bestScore;

        // currentScore는 게임 도중 값이므로, lastScore를 별도로 명확히 저장합니다.
        public int lastScore;
    }

    public string CaptureJson()
    {
        if (scoreData == null)
            return "{}";

        PlayerScoreSaveState state = new PlayerScoreSaveState
        {
            currentScore = scoreData.CurrentScore,
            bestScore = scoreData.BestScore,
            lastScore = scoreData.CurrentScore
        };

        return JsonUtility.ToJson(state);
    }

    public void RestoreFromJson(string json)
    {
        if (scoreData == null)
            return;

        if (string.IsNullOrEmpty(json))
            return;

        PlayerScoreSaveState state = JsonUtility.FromJson<PlayerScoreSaveState>(json);
        if (state == null)
            return;

        int restoredBest = Mathf.Max(0, state.bestScore);
        int restoredCurrent = restoreCurrentScoreFromLastScore ? Mathf.Max(0, state.lastScore) : 0;

        // 새 게임 시작 시 current는 보통 0으로, best는 복구합니다.
        scoreData.SetScores(restoredCurrent, restoredBest);
    }
}
