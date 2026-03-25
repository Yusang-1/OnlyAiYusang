using System;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-100)]
public class SaveManager : MonoBehaviour
{
    public static SaveManager Instance { get; private set; }

    [Header("PlayerPrefs")]
    [SerializeField] private string playerPrefsKey = "OnlyAi.SaveData";

    private readonly Dictionary<string, ISaveable> _saveables = new Dictionary<string, ISaveable>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // 이미 로드되어 있는 saveable 컴포넌트를 찾아 등록
        var behaviours = FindObjectsOfType<MonoBehaviour>(true);
        foreach (var b in behaviours)
        {
            if (b is ISaveable saveable)
            {
                string key = saveable.SaveKey;
                if (!string.IsNullOrEmpty(key))
                    _saveables[key] = saveable;
            }
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void Register(ISaveable saveable)
    {
        if (saveable == null)
            return;

        string key = saveable.SaveKey;
        if (string.IsNullOrEmpty(key))
        {
            Debug.LogWarning("SaveManager: saveable SaveKey가 null/빈 값입니다.");
            return;
        }

        _saveables[key] = saveable;
    }

    public void Unregister(ISaveable saveable)
    {
        if (saveable == null)
            return;

        string key = saveable.SaveKey;
        if (string.IsNullOrEmpty(key))
            return;

        if (_saveables.TryGetValue(key, out ISaveable existing) && existing == saveable)
            _saveables.Remove(key);
    }

    public void Save()
    {
        SaveData data = new SaveData();
        data.version = SaveData.CurrentVersion;

        SaveEntry[] entries = new SaveEntry[_saveables.Count];
        int i = 0;

        foreach (var kv in _saveables)
        {
            string json = kv.Value != null ? kv.Value.CaptureJson() : "{}";
            if (string.IsNullOrEmpty(json))
                json = "{}";

            entries[i++] = new SaveEntry { key = kv.Key, json = json };
        }

        data.entries = entries;

        string wrapperJson = JsonUtility.ToJson(data);
        PlayerPrefs.SetString(playerPrefsKey, wrapperJson);
        PlayerPrefs.Save();
    }

    public bool LoadFromPlayerPrefs()
    {
        if (!PlayerPrefs.HasKey(playerPrefsKey))
            return false;

        string wrapperJson = PlayerPrefs.GetString(playerPrefsKey, "");
        if (string.IsNullOrEmpty(wrapperJson))
            return false;

        SaveData data;
        try
        {
            data = JsonUtility.FromJson<SaveData>(wrapperJson);
        }
        catch (Exception e)
        {
            Debug.LogWarning("SaveManager: 저장 데이터 파싱 실패. " + e.Message);
            return false;
        }

        if (data == null || data.entries == null)
            return true;

        foreach (var entry in data.entries)
        {
            if (entry == null || string.IsNullOrEmpty(entry.key))
                continue;

            if (_saveables.TryGetValue(entry.key, out ISaveable saveable) && saveable != null)
                saveable.RestoreFromJson(entry.json);
        }

        return true;
    }
}
