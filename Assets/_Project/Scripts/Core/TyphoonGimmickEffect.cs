using UnityEngine;

// 기믹 "태풍": 화면에 강풍 파티클(+사운드가 있으면 재생)을 띄운다. 전용 텍스처/사운드가 아직
// 없어 기본 파티클 모양을 옆으로 늘려 바람처럼 보이게 한다. 실제 몬스터 이동/애니메이션 가속은
// StageGimmickManager.EnemySpeedMultiplier를 Enemy/AI 스크립트들이 직접 읽어 처리하므로
// 이 컴포넌트는 연출만 담당한다.
public class TyphoonGimmickEffect : MonoBehaviour
{
    public AudioClip windSound;

    private ParticleSystem windParticles;
    private AudioSource audioSource;

    void Awake()
    {
        windParticles = CreateWindParticles();

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
        Camera cam = Camera.main;
        if (cam == null || windParticles == null) return;
        windParticles.transform.position = new Vector3(cam.transform.position.x - 8f, cam.transform.position.y, 0f);
    }

    private ParticleSystem CreateWindParticles()
    {
        GameObject go = new GameObject("WindParticles");
        go.transform.SetParent(transform, false);

        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop = true;
        main.startLifetime = 1f;
        main.startSpeed = 18f;
        main.startSize = 0.08f;
        main.startColor = new Color(0.9f, 0.9f, 0.9f, 0.35f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 300;

        var emission = ps.emission;
        emission.rateOverTime = 60f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(0.5f, 10f, 1f);

        // x/y/z 커브는 전부 같은 모드(TwoConstants)로 맞춰야 한다 - 하나만 설정하고 나머지를
        // 기본값(Constant)으로 두면 "Particle Velocity curves must all be in the same mode" 에러가 난다.
        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.x = new ParticleSystem.MinMaxCurve(16f, 20f);
        vel.y = new ParticleSystem.MinMaxCurve(0f, 0f);
        vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        ParticleSystemRenderer rend = ps.GetComponent<ParticleSystemRenderer>();
        rend.renderMode = ParticleSystemRenderMode.Stretch;
        rend.velocityScale = 0.05f;
        rend.lengthScale = 3f;
        rend.material = new Material(Shader.Find("Sprites/Default"));

        ps.Stop();
        go.SetActive(false);
        return ps;
    }

    private void HandleStart(GimmickType type)
    {
        if (type != GimmickType.Typhoon) return;
        windParticles.gameObject.SetActive(true);
        windParticles.Play();
        if (windSound != null) { audioSource.clip = windSound; audioSource.Play(); }
    }

    private void HandleEnd(GimmickType type)
    {
        if (type != GimmickType.Typhoon) return;
        windParticles.Stop();
        windParticles.gameObject.SetActive(false);
        audioSource.Stop();
    }
}
