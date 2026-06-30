using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 플레이어 무기를 제어하는 스크립트.
/// 무기는 항상 마우스 방향을 향하며, 좌클릭 시 부채꼴 범위를 휘둘러 적을 공격한다.
/// WeaponPivot(빈 오브젝트)이 회전 중심이 되고, WeaponVisual(스프라이트)이 그 자식으로 오프셋되어 있다.
/// </summary>
public class Weapon : MonoBehaviour
{
    [Header("Stats")]
    // 적에게 입히는 공격력
    public int damage = 3;
    // 공격 후 다시 공격 가능해지기까지의 대기 시간 (초)
    public float attackCooldown = 0.4f;
    // 좌클릭 방향 기준 좌우로 휘두르는 각도의 절반 (전체 휘두름 = swingHalfAngle * 2)
    public float swingHalfAngle = 65f;
    // 한 번 휘두르는 데 걸리는 시간 (초)
    public float swingDuration = 0.18f;
    // 공격이 닿는 최대 거리 (플레이어 중심 기준)
    public float weaponReach = 2.3f;

    [Header("References")]
    // 회전 중심이 되는 빈 오브젝트 (Player의 자식). 이 오브젝트를 회전시켜 무기 방향을 제어한다.
    public Transform weaponPivot;
    // 무기 스프라이트의 SpriteRenderer. 마우스 방향에 따라 Y 반전에 사용한다.
    public SpriteRenderer weaponRenderer;
    // 공격 시 재생할 타격음 클립 배열 (Melee0, Melee1)
    public AudioClip[] meleeClips;
    // 적을 맞췄을 때 재생할 피격음 클립 배열 (Hit0, Hit1)
    public AudioClip[] hitClips;

    private AudioSource audioSource;
    // 현재 휘두르는 중인지 여부 - true이면 마우스 추적을 중단하고 스윙 코루틴이 회전을 담당
    private bool isSwinging = false;
    // 공격 쿨다운 타이머
    private float cooldownTimer = 0f;
    // 한 번의 스윙에서 이미 피격된 적을 추적 (같은 스윙에 중복 피격 방지)
    private readonly HashSet<EnemyController> hitThisSwing = new HashSet<EnemyController>();

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        // 씬 저장/로드 과정에서 직렬화 참조가 소실된 경우를 대비해 자식 오브젝트에서 자동 탐색
        if (weaponPivot == null)
        {
            // WeaponPivot → WeaponVisual 계층 구조에서 탐색
            Transform pivot = transform.Find("WeaponPivot");
            if (pivot != null)
            {
                weaponPivot = pivot;
                Transform visual = pivot.Find("WeaponVisual");
                if (visual != null)
                    weaponRenderer = visual.GetComponent<SpriteRenderer>();
            }
        }
    }

    void Update()
    {
        // 쿨다운 감소
        if (cooldownTimer > 0f)
            cooldownTimer -= Time.deltaTime;

        // 스윙 중이 아닐 때만 마우스 방향으로 무기를 회전
        if (!isSwinging)
            AimAtMouse();

        // 좌클릭이고 쿨다운이 끝났고 스윙 중이 아닐 때 공격 시작
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame
            && cooldownTimer <= 0f && !isSwinging)
        {
            StartCoroutine(SwingAttack());
        }
    }

    /// <summary>
    /// 마우스 스크린 좌표를 2D 직교 카메라 기준 월드 좌표로 변환해 반환한다.
    /// z값으로 카메라와 게임 월드 간의 거리를 전달해야 올바른 위치가 계산된다.
    /// </summary>
    Vector2 GetMouseWorldPos()
    {
        Vector2 screenPos = Mouse.current.position.ReadValue();
        Vector3 world = Camera.main.ScreenToWorldPoint(
            new Vector3(screenPos.x, screenPos.y, Mathf.Abs(Camera.main.transform.position.z)));
        return world;
    }

    /// <summary>
    /// WeaponPivot을 마우스 방향으로 회전시키고, 방향에 따라 스프라이트를 Y 반전한다.
    /// 마우스가 왼쪽을 향할 때 flipY를 적용해 무기가 뒤집혀 보이지 않도록 한다.
    /// </summary>
    void AimAtMouse()
    {
        if (weaponPivot == null || Camera.main == null || Mouse.current == null) return;

        Vector2 dir = GetMouseWorldPos() - (Vector2)transform.position;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        weaponPivot.rotation = Quaternion.Euler(0f, 0f, angle);
        UpdateFlip(angle);
    }

    /// <summary>
    /// 좌클릭 방향 기준으로 swingHalfAngle씩 좌우로 무기를 휘두른다.
    /// SmoothStep으로 시작/끝을 부드럽게 처리하고, 매 프레임 적 피격 판정을 수행한다.
    /// </summary>
    IEnumerator SwingAttack()
    {
        isSwinging = true;
        cooldownTimer = attackCooldown;
        hitThisSwing.Clear(); // 이번 스윙의 피격 기록 초기화

        PlayMeleeSound();

        // 클릭 시점의 마우스 방향을 스윙 중심 각도로 사용
        Vector2 dir = GetMouseWorldPos() - (Vector2)transform.position;
        float centerAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

        float startAngle = centerAngle - swingHalfAngle;
        float endAngle   = centerAngle + swingHalfAngle;

        float elapsed = 0f;
        while (elapsed < swingDuration)
        {
            elapsed += Time.deltaTime;
            // SmoothStep으로 시작과 끝의 움직임을 부드럽게 처리
            float t = Mathf.SmoothStep(0f, 1f, elapsed / swingDuration);
            float currentAngle = Mathf.Lerp(startAngle, endAngle, t);

            if (weaponPivot != null)
                weaponPivot.rotation = Quaternion.Euler(0f, 0f, currentAngle);

            UpdateFlip(currentAngle);
            CheckHits(currentAngle); // 매 프레임 현재 무기 각도로 피격 판정
            yield return null;
        }

        // 스윙 종료 시 최종 각도 고정
        if (weaponPivot != null)
            weaponPivot.rotation = Quaternion.Euler(0f, 0f, endAngle);

        isSwinging = false;
    }

    /// <summary>
    /// 현재 무기 각도를 기준으로 weaponReach 이내에 있고 25도 이내의 각도에 위치한 적을 타격한다.
    /// hitThisSwing으로 같은 스윙에 중복 피격을 방지한다.
    /// </summary>
    /// <param name="weaponAngle">현재 무기가 향하는 각도 (도, Z축 회전)</param>
    void CheckHits(float weaponAngle)
    {
        // 현재 무기 방향을 단위 벡터로 변환
        Vector2 weaponDir = new Vector2(
            Mathf.Cos(weaponAngle * Mathf.Deg2Rad),
            Mathf.Sin(weaponAngle * Mathf.Deg2Rad));

        // 리스트를 복사해서 순회 (Die() 호출 시 원본 리스트가 수정되는 것을 방지)
        var enemies = new List<EnemyController>(EnemyController.ActiveEnemies);
        foreach (EnemyController enemy in enemies)
        {
            if (enemy == null || hitThisSwing.Contains(enemy)) continue;

            Vector2 toEnemy = (Vector2)(enemy.transform.position - transform.position);

            // 거리 체크: 무기 사거리 초과 시 스킵
            if (toEnemy.magnitude > weaponReach) continue;

            // 각도 체크: 무기 방향과 적 방향의 각도 차이가 25도 이내여야 피격
            if (Vector2.Angle(weaponDir, toEnemy) <= 25f)
            {
                hitThisSwing.Add(enemy);
                PlayHitSound();
                enemy.TakeDamage(damage, transform.position);
            }
        }
    }

    /// <summary>
    /// 무기 각도가 90~270도(왼쪽 방향) 범위일 때 스프라이트를 Y축 반전한다.
    /// 오른쪽을 향할 때는 정상, 왼쪽을 향할 때는 뒤집어 칼날이 항상 바깥쪽을 향하게 한다.
    /// </summary>
    void UpdateFlip(float angle)
    {
        if (weaponRenderer == null) return;
        // 각도를 0~360 범위로 정규화
        float n = (angle % 360f + 360f) % 360f;
        weaponRenderer.flipY = n > 90f && n < 270f;
    }

    // 공격 시 Melee 사운드를 배열에서 랜덤하게 재생
    void PlayMeleeSound()
    {
        if (meleeClips == null || meleeClips.Length == 0) return;
        audioSource.PlayOneShot(meleeClips[Random.Range(0, meleeClips.Length)]);
    }

    // 적 명중 시 Hit 사운드를 배열에서 랜덤하게 재생
    void PlayHitSound()
    {
        if (hitClips == null || hitClips.Length == 0) return;
        audioSource.PlayOneShot(hitClips[Random.Range(0, hitClips.Length)]);
    }
}
