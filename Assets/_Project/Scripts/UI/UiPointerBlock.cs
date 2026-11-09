using UnityEngine;

// 게임을 멈추지 않는 팝업(긴급탈출 확인창 등) 위에 마우스가 올라가 있으면 true.
// 그동안 무기들은 왼쪽 클릭을 공격으로 쓰지 않는다 (팝업 버튼을 누르는 클릭에 칼질/사격이 같이 나가지 않게).
// 팝업이 매 프레임 직접 켜고 끈다 (RetreatManager 참고). 자동공격(들고 있지 않은 무기)은 영향 없다.
public static class UiPointerBlock
{
    public static bool Active;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Active = false;
    }
}
