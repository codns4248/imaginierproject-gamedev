using UnityEngine;
using UnityEngine.UI;

// 기믹 "안개": UI 최상단에 반투명 어두운 마스크를 씌우고 화면 중앙(카메라가 플레이어를 데드존으로
// 쫓아가므로 대체로 플레이어 부근)만 둥글게 뚫어 멀리 있는 몬스터/지형을 알아보기 어렵게 만든다.
// 텍스처는 전용 아트가 없어 런타임에 방사형 그라데이션으로 직접 생성한다 (GimmickTextureUtil).
public class FogGimmickEffect : MonoBehaviour
{
    private const int TextureSize = 256;
    private const float InnerRadiusFrac = 0.22f; // 이 안쪽은 완전히 투명 (플레이어 주변 시야)
    private const float OuterRadiusFrac = 0.62f; // 이 밖은 완전히 불투명
    private static readonly Color MaskColor = new Color(0.02f, 0.02f, 0.05f, 0.88f);

    private Image image;

    void Awake()
    {
        GameObject canvas = GameObject.Find("Canvas");
        if (canvas == null) { enabled = false; return; }

        GameObject go = new GameObject("FogMask");
        go.transform.SetParent(canvas.transform, false);
        go.transform.SetAsLastSibling(); // 다른 UI 위에 그려지도록 맨 뒤(=맨 위)로

        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        image = go.AddComponent<Image>();
        image.sprite = GimmickTextureUtil.CreateRadialMask(TextureSize, MaskColor, InnerRadiusFrac, OuterRadiusFrac);
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
        if (type == GimmickType.Fog && image != null) image.gameObject.SetActive(true);
    }

    private void HandleEnd(GimmickType type)
    {
        if (type == GimmickType.Fog && image != null) image.gameObject.SetActive(false);
    }
}
