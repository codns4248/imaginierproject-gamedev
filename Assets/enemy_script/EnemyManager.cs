using System.Collections.Generic;

// 현재 씬에 살아있는 모든 적을 추적하는 정적(static) 목록.
// EnemySpawner가 이 개수를 보고 최대 마리 수(예: 50마리)를 넘지 않도록 스폰을 제어하고,
// 각 Enemy는 이 목록을 훑어서 근처의 다른 적과 겹치지 않게 서로를 밀어내는 데 사용한다.
// static이라 씬에 오브젝트를 따로 두지 않아도 어디서든 EnemyManager.ActiveEnemies로 접근 가능하다.
public static class EnemyManager
{
    public static readonly List<Enemy> ActiveEnemies = new List<Enemy>();

    public static void Register(Enemy enemy)
    {
        if (!ActiveEnemies.Contains(enemy))
            ActiveEnemies.Add(enemy);
    }

    public static void Unregister(Enemy enemy)
    {
        ActiveEnemies.Remove(enemy);
    }
}
