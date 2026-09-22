using UnityEngine;
using UnityEngine.UI;

// 기믹 "한파": 화면 테두리에 서리가 낀 듯한 흰-청색 반투명 프레임을 씌운다 (FogGimmickEffect와
// 같은 런타임 방사형 텍스처 방식, 안쪽 대부분은 투명하고 가장자리만 진해지도록 반지름만 다르게 잡음).
// 실제 공격/재장전 속도 저하는 StageGimmickManager.WeaponTimeScale을 각 무기 스크립트가
// 직접 읽어 처리하므로 이 컴포넌트는 연출만 담당한다.
public class ColdWaveGimmickEffect : MonoBehaviour
{
    private const int TextureSize = 256;
    private const float InnerRadiusFrac = 0.68f; // 화면 중앙 대부분은 투명
    private const float OuterRadiusFrac = 1.05f; // 테두리로 갈수록 서리색이 진해짐
    private static readonly Color FrostColor = new Color(0.75f, 0.9f, 1f, 0.55f);

    private Image image;

    void Awake()
    {
        GameObject canvas = GameObject.Find("Canvas");
        if (canvas == null) { enabled = false; return; }

        GameObject go = new GameObject("ColdWaveFrost");
        go.transform.SetParent(canvas.transform, false);
        go.transform.SetAsLastSibling();

        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        image = go.AddComponent<Image>();
        image.sprite = GimmickTextureUtil.CreateRadialMask(TextureSize, FrostColor, InnerRadiusFrac, OuterRadiusFrac);
        image.raycastTarget = false;
        go.SetActive(false);

        StageGimmickManager.OnGimmickStart += HandleStart;
        StageGimmickManager.OnGimmickEnd += HandleEnd;
    }

    void OnDestroy()
    {
        StageGimmickManager.OnGimmickStart -= HandleStart;
        StageGimmickManager.OnGimmickEnd -= HandleEnd;
    }

    private void HandleStart(GimmickType type)
    {
        if (type == GimmickType.ColdWave && image != null) image.gameObject.SetActive(true);
    }

    private void HandleEnd(GimmickType type)
    {
        if (type == GimmickType.ColdWave && image != null) image.gameObject.SetActive(false);
    }
}
