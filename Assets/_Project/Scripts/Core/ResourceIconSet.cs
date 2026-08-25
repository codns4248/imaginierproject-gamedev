using UnityEngine;

// 자원 종류별 실제 아이콘 스프라이트 모음. 아직 아이콘이 없는 종류는 배열 칸을 비워두면
// ResourcePickup이 알아서 기존 단색 임시 스프라이트로 대체한다.
// Resources 폴더에 두고 런타임에 Resources.Load로 불러오기 때문에 빌드에도 정상 포함된다.
[CreateAssetMenu(fileName = "ResourceIconSet", menuName = "뱀서라이크/자원 아이콘 세트")]
public class ResourceIconSet : ScriptableObject
{
    // ResourceType의 순서(Wood, Iron, Copper, Chemical, Oil)와 정확히 맞춰서 채운다.
    public Sprite[] icons = new Sprite[5];

    // 아이콘별 표시 배율. icons와 순서를 맞춰서 쓰고, 기본값은 1(원본 크기)이다.
    public float[] scales = { 1f, 1f, 1f, 1f, 1f };
}
