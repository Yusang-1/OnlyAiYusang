using UnityEngine;

public class Cell : MonoBehaviour
{
    // 해당 셀로 이동 가능 여부
    private bool isAvailable = true;

    // 외부에서는 읽기만, 변경은 Cell 내부 메서드로만
    public bool IsAvailable
    {
        get => isAvailable;
        private set => isAvailable = value;
    }

    // 셀 접근 가능 여부를 변경하는 공용 메서드
    public void SetAvailable(bool available)
    {
        IsAvailable = available;
    }
}
