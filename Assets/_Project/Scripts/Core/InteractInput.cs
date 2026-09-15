using UnityEngine;
using UnityEngine.InputSystem;

// F키로 반응하는 상호작용이 여러 개(바닥 무기 줍기, 상인 구매 등) 있는데, 같은 프레임에 우연히
// 겹치면(예: 상인 상품 근처에 바닥 무기도 같이 있는 경우) 한 번의 F 입력으로 둘 다 발동해서
// "하나 샀는데 두 개 얻었다" 같은 중복 반응이 생긴다. 이걸 막기 위해 프레임당 한 번만 F를
// "가져갈" 수 있게 한다 - 먼저 확인한 쪽이 가져가면 그 프레임엔 다른 쪽은 반응하지 않는다.
public static class InteractInput
{
    private static int consumedFrame = -1;

    public static bool TryConsumeFKey()
    {
        if (Keyboard.current == null) return false;
        if (!Keyboard.current.fKey.wasPressedThisFrame) return false;
        if (consumedFrame == Time.frameCount) return false; // 이번 프레임엔 이미 다른 쪽이 가져감

        consumedFrame = Time.frameCount;
        return true;
    }
}
