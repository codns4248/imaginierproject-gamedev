using UnityEngine;

// 기믹 연출용 원형 텍스처를 런타임에 직접 생성하는 공용 헬퍼. 전용 아트가 아직 없어도
// 안개/한파 화면 마스크나 벼락 경고 장판 같은 효과를 바로 만들 수 있게 해준다
// (ResourcePickup.GetFallbackSprite와 같은 "런타임 생성 폴백 텍스처" 패턴).
public static class GimmickTextureUtil
{
    // 중심에서 innerRadiusFrac까지는 완전 투명, outerRadiusFrac 밖은 edgeColor의 알파 그대로,
    // 그 사이는 부드럽게 보간되는 정사각형 텍스처. 화면 전체를 덮는 UI 마스크(안개/한파)에 쓴다.
    public static Sprite CreateRadialMask(int size, Color edgeColor, float innerRadiusFrac, float outerRadiusFrac)
    {
        Color[] pixels = BuildRadialPixels(size, edgeColor, innerRadiusFrac, outerRadiusFrac, invert: false);
        return BuildSprite(size, pixels, 100f);
    }

    // 중심에서 radiusFrac까지는 fillColor로 꽉 차 있고, 그 밖으로 나가면서 부드럽게 투명해지는
    // 원형 텍스처. 월드 스페이스에 그려서 딱 1유닛 크기가 되도록 pixelsPerUnit을 size로 맞춘다
    // (벼락 경고 장판처럼 실제 피해 반경과 시각적 크기를 스케일 하나로 맞추기 위함).
    public static Sprite CreateFilledCircle(int size, Color fillColor, float radiusFrac)
    {
        Color[] pixels = BuildRadialPixels(size, fillColor, radiusFrac - 0.08f, radiusFrac, invert: true);
        return BuildSprite(size, pixels, size);
    }

    private static Color[] BuildRadialPixels(int size, Color color, float innerFrac, float outerFrac, bool invert)
    {
        Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
        float maxDist = size * 0.5f;
        float innerDist = maxDist * Mathf.Max(0f, innerFrac);
        float outerDist = maxDist * Mathf.Max(innerFrac + 0.001f, outerFrac);

        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                float t = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(innerDist, outerDist, dist));
                if (invert) t = 1f - t;
                pixels[y * size + x] = new Color(color.r, color.g, color.b, color.a * t);
            }
        }
        return pixels;
    }

    private static Sprite BuildSprite(int size, Color[] pixels, float pixelsPerUnit)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), pixelsPerUnit);
    }
}
