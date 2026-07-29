// =============================================================================
// DetRng — 결정론 난수(번호표 뽑는 기계)
// =============================================================================
// [담당] Core Gameplay (윤호영)  ※ 날씨 재현용 도구
//
// [역할] 보통 Random은 굴릴 때마다 다른 값이 나온다(주사위).
//        DetRng는 "씨앗(seed)"만 같으면 항상 똑같은 값이 순서대로 나온다(번호표).
//        → 게임을 껐다 켜도, 같은 씨앗을 넣으면 같은 구름을 그대로 재현할 수 있다.
//
// [원리] xorshift라는 아주 가벼운 섞기 공식. 씨앗을 계속 섞어서 다음 값을 만든다.
//        같은 씨앗 → 같은 섞기 결과 → 같은 수열. (컴퓨터가 달라도 결과 동일)
//
// [쓰는 법]
//        var rng = new DetRng(DetRng.Seed(worldSeed, simStep, 0));  // 이 순간의 번호표 뭉치
//        float s = rng.Range(0.7f, 1.4f);   // 크기
//        int   d = rng.Range(0, 4);         // 방향(0~3)
//        ※ 뽑는 "순서"만 지키면 항상 같은 값이 나온다.
// =============================================================================

/// <summary>씨앗이 같으면 항상 같은 수열이 나오는 결정론 난수(구름 재현용).</summary>
public struct DetRng
{
    private uint _state;   // 현재 상태(뽑을 때마다 섞여서 바뀜)

    /// <summary>씨앗으로 번호표 뭉치를 만든다. (씨앗 0은 xorshift가 멈추므로 보정)</summary>
    public DetRng(uint seed)
    {
        _state = seed == 0u ? 2166136261u : seed;
    }

    /// <summary>다음 상태로 섞는다(xorshift32). 내부용.</summary>
    private uint Next()
    {
        _state ^= _state << 13;
        _state ^= _state >> 17;
        _state ^= _state << 5;
        return _state;
    }

    /// <summary>0.0 ~ 1.0 사이 값 하나(주사위의 Random.value 대체).</summary>
    public float Value()
    {
        // 하위 24비트만 써서 0~1로 정규화(부동소수 정밀도 안전 범위)
        return (Next() & 0xFFFFFFu) / (float)0x1000000;
    }

    /// <summary>a ~ b 사이 실수 하나(Random.Range(float,float) 대체).</summary>
    public float Range(float a, float b)
    {
        return a + (b - a) * Value();
    }

    /// <summary>minInclusive ~ maxExclusive 사이 정수 하나(Random.Range(int,int) 대체).</summary>
    public int Range(int minInclusive, int maxExclusive)
    {
        if (maxExclusive <= minInclusive) return minInclusive;
        return minInclusive + (int)(Value() * (maxExclusive - minInclusive));
    }

    // ------------------------------------------------------------------ 씨앗 만들기

    /// <summary>
    /// 여러 재료(월드씨앗 · 몇 번째 순간 · 용도구분)를 섞어 하나의 씨앗을 만든다.
    /// 순간(step)마다·용도(salt)마다 다른 씨앗 → 서로 겹치지 않는 번호표.
    /// </summary>
    public static uint Seed(uint worldSeed, long step, int salt)
    {
        const uint prime = 16777619u;
        uint h = worldSeed == 0u ? 2166136261u : worldSeed;
        h = (h ^ (uint)(step & 0xFFFFFFFF)) * prime;   // step 하위 32비트
        h = (h ^ (uint)(step >> 32)) * prime;          // step 상위 32비트(긴 여행 대비)
        h = (h ^ (uint)salt) * prime;                  // 용도 구분
        return h;
    }
}
