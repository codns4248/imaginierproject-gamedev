using System;
using System.Collections.Generic;
using UnityEngine;

// 스테이지 제한 시간을 관리한다. 게임(씬) 시작과 동시에 카운트다운을 시작하고,
// 시간이 다 되면 몬스터 스포너를 멈추고, 남아있는 모든 적을 제거한 뒤 스테이지 클리어 처리를 한다.
public class StageTimer : MonoBehaviour
{
    public float duration = 300f; // 5분

    private float remainingTime;
    private bool isRunning;
    private bool isCleared;

    public float RemainingTime => remainingTime;
    public bool IsCleared => isCleared;

    // 남은 시간이 바뀔 때마다 호출된다. 타이머 UI가 구독해서 표시 갱신에 사용한다.
    public event Action OnTimeChanged;

    // 시간이 다 되어 스테이지를 클리어했을 때 딱 한 번 호출된다. 이후 결과 화면 등에서 사용할 수 있다.
    public event Action OnStageClear;

    void Start()
    {
        remainingTime = duration;
        isRunning = false; // 거점에서는 대기 상태. StageManager.EnterRandomStage()가 BeginStage()로 시작시킨다.
        OnTimeChanged?.Invoke();
    }

    // 스테이지 구역에 진입할 때 호출해서 카운트다운을 새로 시작한다.
    public void BeginStage()
    {
        remainingTime = duration;
        isRunning = true;
        isCleared = false;
        OnTimeChanged?.Invoke();
    }

    // 거점으로 복귀할 때 호출해서 타이머를 멈추고 초기화한다.
    public void StopAndReset()
    {
        isRunning = false;
        remainingTime = duration;
        OnTimeChanged?.Invoke();
    }

    void Update()
    {
        if (!isRunning) return;

        remainingTime -= Time.deltaTime;
        if (remainingTime <= 0f)
        {
            remainingTime = 0f;
            isRunning = false;
            OnTimeChanged?.Invoke();
            ClearStage();
        }
        else
        {
            OnTimeChanged?.Invoke();
        }
    }

    private void ClearStage()
    {
        isCleared = true;

        // 스포너를 전부 꺼서 더 이상 새 적이 생기지 않게 한다 (일반/엘리트 등 스포너가 여러 개 있음).
        foreach (EnemySpawner spawner in FindObjectsByType<EnemySpawner>(FindObjectsSortMode.None))
        {
            spawner.enabled = false;
        }

        // 남아있는 모든 적을 제거한다. Destroy 도중 EnemyManager.ActiveEnemies 목록이 같이 바뀌므로
        // 원본 리스트를 그대로 순회하지 않고 복사본을 만들어서 순회한다.
        List<Enemy> remainingEnemies = new List<Enemy>(EnemyManager.ActiveEnemies);
        foreach (Enemy enemy in remainingEnemies)
        {
            if (enemy != null) enemy.Kill();
        }

        Debug.Log("스테이지 클리어!");
        OnStageClear?.Invoke();
    }
}
