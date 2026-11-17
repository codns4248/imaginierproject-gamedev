using System;
using System.Collections.Generic;

// ESC로 닫을 수 있는 팝업 목록. 팝업이 열릴 때 Push로 올려두면, ESC를 눌렀을 때
// 설정 창 대신 가장 나중에 연 팝업 하나만 닫힌다 (PauseManager.Update 참고).
//  - 열린 팝업이 하나도 없을 때만 ESC가 설정 창을 연다.
//  - 레벨업 카드 창, 사망 결과 화면은 여기에 올리지 않는다 (ESC로 닫히면 안 되는 창).
// 닫는 쪽에서 따로 빼 줄 필요는 없다: ESC를 누를 때 이미 닫혔거나(isOpen이 false) 사라진 팝업은 건너뛰고 버린다.
public static class EscClose
{
    private struct Entry
    {
        public UnityEngine.Object owner;
        public Func<bool> isOpen;
        public Action close;
    }

    private static readonly List<Entry> stack = new List<Entry>();

    // Domain Reload를 꺼둔 채 플레이를 반복해도 이전 플레이의 팝업이 남지 않게 시작할 때 비운다.
    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => stack.Clear();

    /// <summary>팝업이 열릴 때 부른다. 같은 owner가 이미 있으면 맨 위로 올린다.</summary>
    public static void Push(UnityEngine.Object owner, Func<bool> isOpen, Action close)
    {
        stack.RemoveAll(e => ReferenceEquals(e.owner, owner));
        stack.Add(new Entry { owner = owner, isOpen = isOpen, close = close });
    }

    /// <summary>맨 위에 열려 있는 팝업 하나를 닫는다. 닫을 팝업이 없으면 false.</summary>
    public static bool TryCloseTop()
    {
        for (int i = stack.Count - 1; i >= 0; i--)
        {
            Entry e = stack[i];
            stack.RemoveAt(i);
            if (e.owner == null || !e.isOpen()) continue; // 이미 닫혔거나 파괴된 팝업
            e.close();
            return true;
        }
        return false;
    }
}
