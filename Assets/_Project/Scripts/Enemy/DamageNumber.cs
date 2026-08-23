using UnityEngine;
using UnityEngine.UI;

// 적이 데미지를 받았을 때 그 위에 잠깐 떠올랐다 사라지는 데미지 숫자.
// 평소엔 흰색/작게, 치명타면 연노랑/크게 표시된다. Enemy가 Hit()에서 Instantiate해서 사용한다.
// TextMesh(3D 텍스트)는 URP의 기본 폰트 셰이더와 호환 문제로 안 보이는 경우가 있어서,
// 이 프로젝트의 다른 텍스트(타이머 등)와 동일하게 World Space Canvas + UI.Text로 만든다.
public class DamageNumber : MonoBehaviour
{
    public float floatSpeed = 1f;   // 위로 떠오르는 속도
    public float lifetime = 0.6f;   // 떠 있다가 사라지기까지 걸리는 시간

    private static readonly Color NormalColor = Color.white;
    private static readonly Color CritColor = new Color(1f, 0.85f, 0.1f); // 노랑

    private Text text;
    private float elapsed;
    private Color baseColor;

    void Awake()
    {
        text = GetComponentInChildren<Text>();
    }

    // Enemy.Hit()에서 생성 직후(Instantiate 바로 다음 줄) 호출하기 때문에, Awake()가 아직
    // 실행되기 전일 수도 있다. text가 비어있으면 여기서 직접 가져와서 항상 안전하게 만든다.
    public void Setup(float damage, bool isCrit)
    {
        if (text == null) text = GetComponentInChildren<Text>();

        text.text = Mathf.RoundToInt(damage).ToString();
        baseColor = isCrit ? CritColor : NormalColor;
        text.color = baseColor;
        text.fontSize = isCrit ? 40 : 28; // 치명타는 더 크게

        // 연속으로 여러 번 맞을 때 숫자끼리 겹치지 않도록 좌우로 살짝 랜덤 오프셋을 준다.
        transform.position += new Vector3(Random.Range(-0.2f, 0.2f), 0f, 0f);
    }

    void Update()
    {
        elapsed += Time.deltaTime;
        transform.position += Vector3.up * floatSpeed * Time.deltaTime;

        float t = Mathf.Clamp01(elapsed / lifetime);
        Color c = baseColor;
        c.a = Mathf.Lerp(1f, 0f, t); // 서서히 투명해지다가 사라짐
        if (text != null) text.color = c;

        if (elapsed >= lifetime) Destroy(gameObject);
    }
}
