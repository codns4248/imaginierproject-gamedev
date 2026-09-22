using UnityEngine;

// 기믹 "호우": 화면에 비 내리는 파티클(+사운드가 있으면 재생)을 띄운다. 전용 텍스처/사운드가 아직
// 없어 기본 파티클 모양을 늘려 빗줄기처럼 보이게 한다. 실제 이동속도 감소는
// StageGimmickManager.PlayerMoveSpeedMultiplier를 Player_Movement가 직접 읽어 처리하므로
// 이 컴포넌트는 연출만 담당한다.
public class HeavyRainGimmickEffect : MonoBehaviour
{
    public AudioClip rainSound; // 전용 사운드가 생기면 여기에 꽂아주면 된다 (비어있어도 에러 없이 무시됨)

    private ParticleSystem rainParticles;
    private AudioSource audioSource;

    void Awake()
    {
        rainParticles = CreateRainParticles();

        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.loop = true;
        audioSource.playOnAwake = false;

        StageGimmickManager.OnGimmickStart += HandleStart;
        StageGimmickManager.OnGimmickEnd += HandleEnd;
    }

    void OnDestroy()
    {
        StageGimmickManager.OnGimmickStart -= HandleStart;
        StageGimmickManager.OnGimmickEnd -= HandleEnd;
    }

    void LateUpdate()
    {
        // 카메라를 계속 따라다니게 해서 화면 전체에 비가 오는 것처럼 보이게 한다.
        Camera cam = Camera.main;
        if (cam == null || rainParticles == null) return;
        rainParticles.transform.position = new Vector3(cam.transform.position.x, cam.transform.position.y + 6f, 0f);

        // 화면 가로 크기(해상도/종횡비에 따라 달라짐)에 맞춰 박스 폭을 매 프레임 갱신한다.
        // 고정폭으로 두면 화면이 넓을 때 양옆에 비가 안 내리는 빈 구간이 생긴다.
        if (cam.orthographic)
        {
            var shape = rainParticles.shape;
            float halfWidth = cam.orthographicSize * cam.aspect;
            shape.scale = new Vector3(halfWidth * 2f + 4f, 0.5f, 1f); // 여유분 4유닛 추가
        }
    }

    private ParticleSystem CreateRainParticles()
    {
        GameObject go = new GameObject("RainParticles");
        go.transform.SetParent(transform, false);

        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop = true;
        main.startLifetime = 1.2f;
        main.startSpeed = 12f;
        main.startSize = 0.05f;
        main.startColor = new Color(0.6f, 0.7f, 0.9f, 0.6f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 500;

        var emission = ps.emission;
        emission.rateOverTime = 200f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(14f, 0.5f, 1f);

        // x/y/z 커브는 전부 같은 모드(TwoConstants)로 맞춰야 한다 - 하나만 설정하고 나머지를
        // 기본값(Constant)으로 두면 "Particle Velocity curves must all be in the same mode" 에러가 난다.
        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.x = new ParticleSystem.MinMaxCurve(-1.5f, -0.5f);
        vel.y = new ParticleSystem.MinMaxCurve(-12f, -12f);
        vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        ParticleSystemRenderer rend = ps.GetComponent<ParticleSystemRenderer>();
        rend.renderMode = ParticleSystemRenderMode.Stretch;
        rend.velocityScale = 0.08f;
        rend.lengthScale = 2f;
        rend.material = new Material(Shader.Find("Sprites/Default"));

        ps.Stop();
        go.SetActive(false);
        return ps;
    }

    private void HandleStart(GimmickType type)
    {
        if (type != GimmickType.HeavyRain) return;
        rainParticles.gameObject.SetActive(true);
        rainParticles.Play();
        if (rainSound != null) { audioSource.clip = rainSound; audioSource.Play(); }
    }

    private void HandleEnd(GimmickType type)
    {
        if (type != GimmickType.HeavyRain) return;
        rainParticles.Stop();
        rainParticles.gameObject.SetActive(false);
        audioSource.Stop();
    }
}
