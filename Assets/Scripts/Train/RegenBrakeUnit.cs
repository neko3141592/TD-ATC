using UnityEngine;

/// <summary>
/// 回生ブレーキ計算ユニット:
/// 1両分の回生の立ち上がり/立ち下がり、失効、揺らぎを担当する。
/// </summary>
public class RegenBrakeUnit
{
    private const float RegenHardCutOutSpeedMS = 0.1f; // 停止域では回生を強制失効させる閾値[m/s]

    public void InitializeCarState(CarBrakeState state)
    {
        // 車両ごとの回生ノイズ初期化（未設定時のみ）
        if (state == null)
        {
            return;
        }

        if (state.regenNoiseSeed <= 0f)
        {
            state.regenNoiseSeed = Random.Range(1f, 10000f);
        }
    }

    public void ResetCarState(CarBrakeState state)
    {
        // その車両の回生状態だけをリセット
        if (state == null)
        {
            return;
        }

        state.regenForceN = 0f;
        state.regenBrakeApplicationActive = false;
        state.regenLatchedForCurrentBrake = false;
        state.regenNoiseTime = 0f;
    }

    public void UpdateBrakeApplicationState(TrainSpec trainSpec, CarSpec carSpec, CarBrakeState state, bool hasBrakeCommand, float speedMS, bool forceDisableRegen)
    {
        // ブレーキ開始時の速度で回生可否をラッチする（ブレーキ中は保持）
        if (trainSpec == null || state == null)
        {
            return;
        }

        float safeSpeedMS = Mathf.Max(0f, speedMS);
        bool isMotorCar = carSpec != null && carSpec.carType == CarType.Motor;

        if (forceDisableRegen || !isMotorCar)
        {
            ResetCarState(state);
            return;
        }

        if (!hasBrakeCommand)
        {
            state.regenBrakeApplicationActive = false;
            state.regenLatchedForCurrentBrake = false;
            state.regenNoiseTime = 0f;
        }
        else if (!state.regenBrakeApplicationActive)
        {
            state.regenBrakeApplicationActive = true;
            state.regenLatchedForCurrentBrake = safeSpeedMS > trainSpec.regenCutOutEndSpeedMS;
            state.regenNoiseSeed = Random.Range(1f, 10000f);
            state.regenNoiseTime = 0f;
        }
    }

    public float GetRegenCapForceN(TrainSpec trainSpec, CarSpec carSpec, CarBrakeState state, float speedMS)
    {
        // その車両の回生上限力[N]（速度カーブcapと車両capの小さい方）
        if (trainSpec == null || carSpec == null || state == null)
        {
            return 0f;
        }

        if (carSpec.carType != CarType.Motor || !state.regenLatchedForCurrentBrake)
        {
            return 0f;
        }

        float safeSpeedMS = Mathf.Max(0f, speedMS);
        float curveCapDecel = trainSpec.GetRegenCurveCapDeceleration(safeSpeedMS);
        float carCapDecel = Mathf.Max(0f, carSpec.maxRegenDecelMS2);
        float regenCapDecel = Mathf.Min(curveCapDecel, carCapDecel);
        float carMassKg = Mathf.Max(1f, carSpec.massKg);
        return Mathf.Max(0f, regenCapDecel * carMassKg);
    }

    public float UpdateRegenForceN(TrainSpec trainSpec, CarSpec carSpec, CarBrakeState state, float targetRegenForceN, float speedMS, float deltaTime, bool forceDisableRegen)
    {
        // 回生目標力に対して、立上/立下遅れ・揺らぎ・停止域失効を適用した実回生力[N]を返す
        if (trainSpec == null || state == null)
        {
            return 0f;
        }

        float safeSpeedMS = Mathf.Max(0f, speedMS);
        float safeDeltaTime = Mathf.Max(0f, deltaTime);

        if (forceDisableRegen || carSpec == null || carSpec.carType != CarType.Motor)
        {
            state.regenForceN = Mathf.MoveTowards(
                state.regenForceN,
                0f,
                Mathf.Max(0f, trainSpec.regenFallRateMS3) * Mathf.Max(1f, carSpec != null ? carSpec.massKg : trainSpec.massKg) * safeDeltaTime
            );
            state.regenLatchedForCurrentBrake = false;
            return state.regenForceN;
        }

        float regenCapForceN = GetRegenCapForceN(trainSpec, carSpec, state, safeSpeedMS);
        float clampedTargetForceN = Mathf.Clamp(targetRegenForceN, 0f, regenCapForceN);

        // 回生の効き揺らぎ（低周波ノイズ）
        if (trainSpec.enableRegenFluctuation &&
            state.regenLatchedForCurrentBrake &&
            clampedTargetForceN > 0f &&
            trainSpec.regenFluctuationAmplitude > 0f &&
            trainSpec.regenFluctuationFrequencyHz > 0f)
        {
            state.regenNoiseTime += safeDeltaTime * trainSpec.regenFluctuationFrequencyHz;
            float noise01 = Mathf.PerlinNoise(state.regenNoiseSeed, state.regenNoiseTime);
            float noiseSigned = (noise01 - 0.5f) * 2f;
            float fluctuationMultiplier = 1f + (noiseSigned * trainSpec.regenFluctuationAmplitude);
            clampedTargetForceN = Mathf.Clamp(clampedTargetForceN * fluctuationMultiplier, 0f, regenCapForceN);
        }

        float carMassKg = Mathf.Max(1f, carSpec.massKg);
        float regenRateNPerSec = clampedTargetForceN >= state.regenForceN
            ? trainSpec.regenRiseRateMS3 * carMassKg
            : trainSpec.regenFallRateMS3 * carMassKg;
        state.regenForceN = Mathf.MoveTowards(
            state.regenForceN,
            clampedTargetForceN,
            Mathf.Max(0f, regenRateNPerSec) * safeDeltaTime
        );

        // 停止域では回生を使わない（0km/hで回生が残らないように強制0）
        if (safeSpeedMS <= RegenHardCutOutSpeedMS)
        {
            state.regenForceN = 0f;
            state.regenLatchedForCurrentBrake = false;
        }

        return state.regenForceN;
    }
}
