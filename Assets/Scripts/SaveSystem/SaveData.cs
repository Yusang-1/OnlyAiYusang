using System;

[Serializable]
public class SaveEntry
{
    public string key;
    public string json;
}

[Serializable]
public class SaveData
{
    public const int CurrentVersion = 1;

    public int version;
    public SaveEntry[] entries;
}
