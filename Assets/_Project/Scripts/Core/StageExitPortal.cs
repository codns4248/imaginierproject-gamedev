using UnityEngine;

// 스테이지 클리어 후 나타나는 이동용 포탈 하나. 거점 포탈("potal") 오브젝트를 복제해서 만들어지므로
// 스프라이트/콜라이더 구성을 그대로 물려받고, 여기서는 색깔만 다시 입힌다 (원본 아트를 바꾸면 같이 따라간다).
// targetTheme이 있으면 그 테마의 스테이지로, 비어있으면(추출 포탈) 자원을 확정하고 거점으로 이동한다.
public class StageExitPortal : MonoBehaviour
{
    private string targetTheme;

    public void Init(string targetTheme, Color color)
    {
        this.targetTheme = targetTheme;

        // 스프라이트 자체는 원본(거점 포탈)과 그대로 공유하고, 이 인스턴스의 렌더 색만 곱해서 칠한다.
        // 텍스처를 직접 새로 그리면 원본 모양(투명도로 표현된 실루엣)이 사라지므로 틴트로만 처리한다.
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr != null) sr.color = color;

        BoxCollider2D col = GetComponent<BoxCollider2D>();
        if (col != null) col.isTrigger = true;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.gameObject.name != "Player") return;

        if (string.IsNullOrEmpty(targetTheme))
        {
            ResourceBank.CommitRunToStash();
            StageProgress.ResetToFirstStage();
            StageManager.ReturnToHub();
        }
        else
        {
            StageProgress.AdvanceToNextStage();
            StageManager.EnterStage(targetTheme);
        }

        ClearGroundItems();

        // 선택 안 된 나머지 포탈들도 같이 정리한다 (다음 스테이지까지 안 따라오도록).
        Destroy(transform.parent != null ? transform.parent.gameObject : gameObject);
    }

    // 스테이지에 떨어져 있던 자원/무기 드랍 아이템을 깔끔하게 치운다 (다음 스테이지까지 안 따라오도록).
    private static void ClearGroundItems()
    {
        foreach (ResourcePickup pickup in Object.FindObjectsByType<ResourcePickup>(FindObjectsSortMode.None))
        {
            Destroy(pickup.gameObject);
        }
        foreach (WeaponPickup pickup in Object.FindObjectsByType<WeaponPickup>(FindObjectsSortMode.None))
        {
            Destroy(pickup.gameObject);
        }
    }
}
