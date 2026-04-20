using UnityEngine;

/// <summary>
/// 空気ブレーキ計算ユニット:
/// 1両分のBC圧遅れと、BC圧から空気ブレーキ力(N)への変換を担当する。
/// </summary>
internal class AirBrakeUnit
{
    public float GetAirBrakeCapForceN(TrainSpec trainSpec, CarSpec carSpec, float speedMS)
    {
        // 現在速度における「その車両が出せる空気ブレーキ上限力[N]」
        if (trainSpec == null)
        {
            return 0f;
        }

        float maxBCPressureKPa = GetMaxBCPressureKPa(trainSpec, carSpec);
        float forcePerKPa = GetForcePerKPa(trainSpec, carSpec, speedMS);
        return Mathf.Max(0f, maxBCPressureKPa * forcePerKPa);
    }

    public float GetTargetBCPressureKPa(TrainSpec trainSpec, CarSpec carSpec, float targetAirForceN, float speedMS, bool hasBrakeCommand)
    {
        // 目標空気力[N]を実現するための目標BC圧[kPa]を逆算する
        if (trainSpec == null)
        {
            return 0f;
        }

        float maxBCPressureKPa = GetMaxBCPressureKPa(trainSpec, carSpec);
        float safeTargetAirForceN = Mathf.Max(0f, targetAirForceN);
        float forcePerKPa = GetForcePerKPa(trainSpec, carSpec, speedMS);
        float targetBCPressureKPa = forcePerKPa > 0f ? safeTargetAirForceN / forcePerKPa : 0f;
        targetBCPressureKPa = Mathf.Clamp(targetBCPressureKPa, 0f, maxBCPressureKPa);

        if (hasBrakeCommand)
        {
            float minCooperativePressure = Mathf.Clamp(trainSpec.bcMinPressureDuringCooperativeKPa, 0f, maxBCPressureKPa);
            targetBCPressureKPa = Mathf.Max(targetBCPressureKPa, minCooperativePressure);
        }

        return targetBCPressureKPa;
    }

    public float UpdateBCPressureKPa(TrainSpec trainSpec, CarSpec carSpec, float currentBCPressureKPa, float targetBCPressureKPa, float deltaTime)
    {
        // BC圧の応答遅れ（込め/抜き速度）を通して次フレーム値を計算
        if (trainSpec == null)
        {
            return 0f;
        }

        float maxBCPressureKPa = GetMaxBCPressureKPa(trainSpec, carSpec);
        float safeDeltaTime = Mathf.Max(0f, deltaTime);
        float nextBCPressureKPa = Mathf.Clamp(currentBCPressureKPa, 0f, maxBCPressureKPa);
        float clampedTargetBCPressureKPa = Mathf.Clamp(targetBCPressureKPa, 0f, maxBCPressureKPa);

        // BC圧の動特性（遅れ）
        if (nextBCPressureKPa < clampedTargetBCPressureKPa)
        {
            // 目標より低い ＝ ブレーキを強める（空気を込める）
            nextBCPressureKPa = Mathf.MoveTowards(
                nextBCPressureKPa,
                clampedTargetBCPressureKPa,
                trainSpec.bcFillRateKPaPerSec * safeDeltaTime
            );
        }
        else if (nextBCPressureKPa > clampedTargetBCPressureKPa)
        {
            // 目標より高い ＝ ブレーキを緩める（空気を抜く）
            nextBCPressureKPa = Mathf.MoveTowards(
                nextBCPressureKPa,
                clampedTargetBCPressureKPa,
                trainSpec.bcReleaseRateKPaPerSec * safeDeltaTime
            );
        }

        return Mathf.Clamp(nextBCPressureKPa, 0f, maxBCPressureKPa);
    }

    public float GetAirBrakeForceN(TrainSpec trainSpec, CarSpec carSpec, float bcPressureKPa, float speedMS)
    {
        // 実BC圧[kPa] -> 実空気ブレーキ力[N] 変換
        if (trainSpec == null)
        {
            return 0f;
        }

        float maxBCPressureKPa = GetMaxBCPressureKPa(trainSpec, carSpec);
        float safeBCPressureKPa = Mathf.Clamp(bcPressureKPa, 0f, maxBCPressureKPa);
        float forcePerKPa = GetForcePerKPa(trainSpec, carSpec, speedMS);
        return Mathf.Max(0f, safeBCPressureKPa * forcePerKPa);
    }

    private float GetForcePerKPa(TrainSpec trainSpec, CarSpec carSpec, float speedMS)
    {
        // 1kPaあたり何N出るかを車両パラメータと速度依存μから算出
        if (trainSpec == null)
        {
            return 0f;
        }

        // 現在は車両ごとのCarSpec前提。未設定時は0として扱う。
        if (carSpec == null)
        {
            return 0f;
        }

        float mu = trainSpec.GetBrakeFrictionCoefficientMu(speedMS);
        float areaM2 = Mathf.Max(0f, carSpec.bcCylinderAreaM2);
        int cylinderCount = Mathf.Max(1, carSpec.bcCylinderCount);
        float leverage = Mathf.Max(0f, carSpec.brakeLeverageRatio);
        float efficiency = Mathf.Clamp01(carSpec.brakeMechanicalEfficiency);

        // F = P[Pa] * A[m^2] * cylinderCount * leverage * efficiency * μ
        // 1kPa = 1000Pa
        return Mathf.Max(0f, 1000f * areaM2 * cylinderCount * leverage * efficiency * mu);
    }

    private float GetMaxBCPressureKPa(TrainSpec trainSpec, CarSpec carSpec)
    {
        if (carSpec == null)
        {
            return 0f;
        }

        return Mathf.Max(0f, carSpec.bcMaxPressureKPa);
    }
}
