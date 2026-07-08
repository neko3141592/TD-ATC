using UnityEngine;

public partial class TrainController
{
    [SerializeField, Min(0f)] private float brakeHoldSpeedThresholdMS = 0.05f;
    [SerializeField, Min(0f)] private float brakeHoldForceMarginN = 1f;

    private float preAcceleration = 0f;

    /// <summary>
    /// 役割: ApplyPhysics の処理を適用します。
    /// </summary>
    /// <remarks>返り値はありません。</remarks>
    void ApplyPhysics()
    {
        float massKg = GetCurrentConsistMassKg();
        float brakeForceN = GetBrakeCylinderForceN();
        UpdateBrakeCylinderOutputs(brakeForceN, massKg);

        float externalForceN = GetExternalResistanceForceN(massKg);
        float tractionForceN = GetTractionForceN();
        currentTractionForceN = tractionForceN;
        float nonBrakeForceN = tractionForceN + externalForceN;

        if (TryApplyBrakeHold(brakeForceN, nonBrakeForceN))
        {
            return;
        }

        float brakeForceSignedN = GetBrakeForceN(brakeForceN, nonBrakeForceN);

        // 符号付き速度系: 正方向は CurrentDirection、負方向はその反対。
        float netForceN = nonBrakeForceN + brakeForceSignedN;
        float acceleration = netForceN / massKg;

        // ジャークの更新
        currentJerkMS3 = (acceleration - preAcceleration) / Time.deltaTime;
        preAcceleration = acceleration;

        IntegrateMotion(acceleration);
    }

    /// <summary>
    /// 役割: GetCurrentConsistMassKg の処理を取得します。
    /// </summary>
    /// <returns>計算または参照した値を返します。</returns>
    private float GetCurrentConsistMassKg()
    {
        if (TryGetCurrentLoadedConsistMassKg(out float loadedConsistMassKg))
        {
            return loadedConsistMassKg;
        }

        if (brakeSystem != null && brakeSystem.CurrentConsistMassKg > 0f)
        {
            return brakeSystem.CurrentConsistMassKg;
        }

        if (tractionSystem != null && tractionSystem.CurrentConsistMassKg > 0f)
        {
            return tractionSystem.CurrentConsistMassKg;
        }

        return Mathf.Max(1f, trainSpec.massKg);
    }

    private bool TryGetCurrentLoadedConsistMassKg(out float massKg)
    {
        massKg = 0f;
        ConsistDefinition resolvedConsist = ResolveConsistDefinition();
        if (resolvedConsist == null || resolvedConsist.CarCount <= 0)
        {
            return false;
        }

        for (int i = 0; i < resolvedConsist.CarCount; i++)
        {
            massKg += GetCurrentCarMassKg(resolvedConsist, i);
        }

        massKg = Mathf.Max(1f, massKg);
        return true;
    }

    private float GetBrakeCylinderForceN()
    {
        if (brakeCylinders == null || brakeCylinders.Length == 0)
        {
            RefreshBrakeCylindersFromChildren();
        }

        if (brakeCylinders == null)
        {
            return 0f;
        }

        float totalBrakeForceN = 0f;
        for (int i = 0; i < brakeCylinders.Length; i++)
        {
            BrakeCylinder cylinder = brakeCylinders[i];
            if (cylinder == null)
            {
                continue;
            }

            totalBrakeForceN += Mathf.Max(0f, cylinder.BrakeForceN);
        }

        return totalBrakeForceN;
    }

    private void UpdateBrakeCylinderOutputs(float brakeForceN, float massKg)
    {
        currentBrakeForceN = Mathf.Max(0f, brakeForceN);
        currentAirBrakeForceN = currentBrakeForceN;

        float safeMassKg = Mathf.Max(1f, massKg);
        currentBrakeDecelMS2 = currentBrakeForceN / safeMassKg;
        currentAirBrakeDecelMS2 = currentBrakeDecelMS2;

        currentBCPressureKPa = 0f;
        currentTargetBCPressureKPa = 0f;
        if (brakeCylinders == null)
        {
            return;
        }

        for (int i = 0; i < brakeCylinders.Length; i++)
        {
            BrakeCylinder cylinder = brakeCylinders[i];
            if (cylinder == null)
            {
                continue;
            }

            currentBCPressureKPa = Mathf.Max(currentBCPressureKPa, cylinder.CurrentPressureKPa);
            currentTargetBCPressureKPa = Mathf.Max(currentTargetBCPressureKPa, cylinder.TargetPressureKPa);
        }
    }

    /// <summary>
    /// 役割: GetExternalResistanceForceN の処理を取得します。
    /// </summary>
    /// <returns>計算または参照した値を返します。</returns>
    private float GetExternalResistanceForceN(float massKg)
    {
        float runningResistanceForceN = ExternalForceCalculator.GetRunningResistanceForceN(trainSpec, Mathf.Abs(speedMS));
        float headGradientPermille = GetCurrentGradientPermilleForPhysics();
        currentGradeResistanceForceN = GetConsistGradeForceN(massKg, headGradientPermille);

        return GetOpposingVelocityForceN(runningResistanceForceN) + currentGradeResistanceForceN;
    }

    private bool TryApplyBrakeHold(float brakeForceN, float nonBrakeForceN)
    {
        if (brakeForceN <= 0f || Mathf.Abs(speedMS) > brakeHoldSpeedThresholdMS)
        {
            return false;
        }

        if (Mathf.Abs(nonBrakeForceN) > brakeForceN + brakeHoldForceMarginN)
        {
            return false;
        }

        speedMS = 0f;
        currentAccelerationMS2 = 0f;
        currentJerkMS3 = 0f;
        preAcceleration = 0f;
        return true;
    }

    private float GetBrakeForceN(float brakeForceN, float nonBrakeForceN)
    {
        float safeBrakeForceN = Mathf.Max(0f, brakeForceN);
        if (safeBrakeForceN <= 0f)
        {
            return 0f;
        }

        if (Mathf.Abs(speedMS) <= brakeHoldSpeedThresholdMS && Mathf.Abs(nonBrakeForceN) > 0.001f)
        {
            return -Mathf.Sign(nonBrakeForceN) * safeBrakeForceN;
        }

        return GetOpposingVelocityForceN(safeBrakeForceN);
    }

    private float GetConsistGradeForceN(float consistMassKg, float fallbackGradientPermille)
    {
        EnsureRuntimeResolver();
        SyncCarTrackStatesWithConsist();
        UpdateCarTrackStates();

        if (resolver == null || trackGraph == null || carTrackStates == null || carTrackStates.Count == 0)
        {
            return GetSignedGradeForceN(consistMassKg, fallbackGradientPermille, CurrentDirection);
        }

        ConsistDefinition resolvedConsist = ResolveConsistDefinition();
        float totalGradeResistanceForceN = 0f;

        for (int i = 0; i < carTrackStates.Count; i++)
        {
            CarTrackState state = carTrackStates[i];
            float carMassKg = GetCurrentCarMassKg(resolvedConsist, i);
            float gradientPermille = fallbackGradientPermille;
            EdgeTravelDirection frontDirection = CurrentDirection;

            if (state != null)
            {
                frontDirection = state.frontDirection;
                if (!string.IsNullOrEmpty(state.edgeId))
                {
                    resolver.TryGetGradientPermille(trackGraph, state.edgeId, state.distanceOnEdgeM, out gradientPermille);
                }
            }

            totalGradeResistanceForceN += GetSignedGradeForceN(carMassKg, gradientPermille, frontDirection);
        }

        return totalGradeResistanceForceN;
    }

    private float GetCurrentCarMassKg(ConsistDefinition resolvedConsist, int index)
    {
        if (TryGetLoadedCarMassKg(index, out float loadedMassKg))
        {
            return loadedMassKg;
        }

        return GetSpecCarMassKg(resolvedConsist, index);
    }

    private bool TryGetLoadedCarMassKg(int index, out float massKg)
    {
        massKg = 0f;

        if (loadWeightControllers == null || loadWeightControllers.Length == 0)
        {
            RefreshLoadWeightControllersFromChildren();
        }

        if (loadWeightControllers == null)
        {
            return false;
        }

        for (int i = 0; i < loadWeightControllers.Length; i++)
        {
            LoadWeightController load = loadWeightControllers[i];
            if (load == null || load.CarIndex != index)
            {
                continue;
            }

            massKg = Mathf.Max(1f, load.MassKg);
            return true;
        }

        return false;
    }

    private float GetSpecCarMassKg(ConsistDefinition resolvedConsist, int index)
    {
        if (resolvedConsist != null &&
            resolvedConsist.TryGetCar(index, out CarSpec carSpec) &&
            carSpec != null)
        {
            return Mathf.Max(1f, carSpec.massKg);
        }

        return Mathf.Max(1f, trainSpec != null ? trainSpec.massKg : 1f);
    }

    private EdgeTravelDirection GetCarMovementDirection(CarTrackState state)
    {
        EdgeTravelDirection frontDirection = state != null ? state.frontDirection : CurrentDirection;
        return speedMS < -0.001f
            ? TrackGraphUndirectedHelpers.GetOppositeDirection(frontDirection)
            : frontDirection;
    }

    private float GetSignedGradeForceN(float massKg, float gradientPermille, EdgeTravelDirection frontDirection)
    {
        float directionalGradientPermille = frontDirection == EdgeTravelDirection.BtoA
            ? -gradientPermille
            : gradientPermille;

        return -ExternalForceCalculator.GetGradeResistanceForceN(massKg, directionalGradientPermille);
    }

    private float GetOpposingVelocityForceN(float forceMagnitudeN)
    {
        float safeForceMagnitudeN = Mathf.Max(0f, forceMagnitudeN);
        if (safeForceMagnitudeN <= 0f || Mathf.Abs(speedMS) <= 0.001f)
        {
            return 0f;
        }

        return speedMS > 0f ? -safeForceMagnitudeN : safeForceMagnitudeN;
    }

    private float GetCurrentGradientPermilleForPhysics()
    {
        EnsureRuntimeResolver();

        if (resolver == null || trackGraph == null || string.IsNullOrEmpty(currentEdgeId))
        {
            currentGradientPermille = 0f;
            return currentGradientPermille;
        }

        resolver.TryGetGradientPermille(trackGraph, currentEdgeId, distanceOnEdgeM, out currentGradientPermille);
        return currentGradientPermille;
    }

    /// <summary>
    /// 役割: GetTractionForceN の処理を取得します。
    /// </summary>
    /// <returns>計算または参照した値を返します。</returns>
    private float GetTractionForceN()
    {
        int forceSign = GetReverserForceSign();
        if (forceSign == 0)
        {
            if (tractionSystem != null)
            {
                tractionSystem.ClearTractionOutputs();
            }

            return 0f;
        }

        if (vvvfControllers == null || vvvfControllers.Length == 0)
        {
            RefreshVVVFControllersFromChildren();
        }

        if (vvvfControllers == null || vvvfControllers.Length == 0)
        {
            if (tractionSystem != null)
            {
                tractionSystem.ClearTractionOutputs();
            }

            return 0f;
        }

        float totalMotorTractionForceN = 0f;

        for (int i = 0; i < vvvfControllers.Length; i++)
        {
            VVVFController vvvf = vvvfControllers[i];
            if (vvvf == null)
            {
                continue;
            }

            totalMotorTractionForceN += vvvf.TotalMotorTractionForceN;
        }

        if (tractionSystem != null)
        {
            tractionSystem.ApplyExternalTractionForce(Mathf.Abs(totalMotorTractionForceN));
            tractionSystem.ApplyExternalMotorCurrents(vvvfControllers);
        }

        return totalMotorTractionForceN * forceSign;
    }

    private int GetReverserForceSign()
    {
        switch (reverserPosition)
        {
            case ReverserPosition.Forward:
                return 1;
            case ReverserPosition.Reverse:
                return -1;
            default:
                return 0;
        }
    }

    /// <summary>
    /// 役割: IntegrateMotion の処理を積分して状態を更新します。
    /// </summary>
    /// <param name="acceleration">acceleration を指定します。</param>
    /// <remarks>返り値はありません。</remarks>
    private void IntegrateMotion(float acceleration)
    {
        currentAccelerationMS2 = acceleration;
        speedMS += acceleration * Time.deltaTime;
        if (Mathf.Abs(speedMS) < 0.0001f)
        {
            speedMS = 0f;
        }

        float signedDeltaDistanceM = speedMS * Time.deltaTime;
        distance += Mathf.Abs(signedDeltaDistanceM);

        if (CurrentDirection == EdgeTravelDirection.AtoB)
        {
            distanceOnEdgeM += signedDeltaDistanceM;
        }
        else
        {
            distanceOnEdgeM -= signedDeltaDistanceM;
        }
        
        AdvanceEdgeTransitionIfNeeded();
    }
}
