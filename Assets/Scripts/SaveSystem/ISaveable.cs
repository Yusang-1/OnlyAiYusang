using System;

public interface ISaveable
{
    string SaveKey { get; }

    // save 데이터 저장용 JSON 생성
    string CaptureJson();

    // 저장 데이터 복구용 JSON 적용
    void RestoreFromJson(string json);
}
