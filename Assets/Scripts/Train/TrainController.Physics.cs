using UnityEngine;

public partial class TrainController
{
    /// <summary>
    /// 役割: HandleInput の処理を入力や状態を処理します。
    /// </summary>
    /// <remarks>返り値はありません。</remarks>
    void HandleInput()
    {
        if (notchManager == null)
        {
            return;
        }

        if (!acceptPlayerInput)
        {
            notchManager.SetManualNotches(0, 0);
            return;
        }

        int powerNotch = notchManager.ManualPowerNotch;
        int brakeNotch = notchManager.ManualBrakeNotch;
        int emergencyBrakeNotch = EmergencyBrakeNotch;

        if (Input.GetKeyDown(KeyCode.Z))
        {
            if (brakeNotch > 0) brakeNotch--;
            else if (powerNotch < trainSpec.maxPowerNotch) powerNotch++;
        }

        if (Input.GetKeyDown(KeyCode.A))
        {
            while (powerNotch > 0) powerNotch--;
        }

        if (Input.GetKeyDown(KeyCode.Q))
        {
            if (powerNotch > 0) powerNotch--;
            else if (brakeNotch < emergencyBrakeNotch) brakeNotch++;
        }

        notchManager.SetManualNotches(powerNotch, brakeNotch);
    }

    /// <summary>
    /// 役割: ApplyPhysics の処理を適用します。
    /// </summary>
    /// <remarks>返り値はありません。</remarks>
    void ApplyPhysics()
    {
        notchManager.ConfigureLimits(trainSpec.maxPowerNotch, EmergencyBrakeNotch, trainSpec.GetTascBrakeSubstepsPerNotch());

        int powerNotch = PowerNotch;
        int brakeNotch = BrakeNotch;
        bool isEmergencyBrake = brakeNotch >= EmergencyBrakeNotch;
        bool useTascBrakeStep = notchManager.IsTASCBrakeSelected;
        int tascBrakeStep = notchManager.TASCBrakeStep;

        if (brakeSystem != null)
        {
            brakeSystem.UpdateBrake(brakeNotch, speedMS, Time.deltaTime, isEmergencyBrake, useTascBrakeStep, tascBrakeStep, ManualPowerNotch);
        }

        float massKg = GetCurrentConsistMassKg();
        GetBrakeOutputs(brakeNotch, useTascBrakeStep, tascBrakeStep, massKg, out float brakeDeceleration, out float brakeForceN);

        float externalForceN = GetExternalResistanceForceN(powerNotch, brakeDeceleration);
        float tractionForceN = GetTractionForceN(powerNotch, massKg, externalForceN);
        float vehicleForceN = tractionForceN - brakeForceN;

        // 運動方程式: F_net = (F_traction - F_brake) - F_external
        float netForceN = vehicleForceN - externalForceN;
        float acceleration = netForceN / massKg;

        IntegrateMotion(acceleration);
    }

    /// <summary>
    /// 役割: GetCurrentConsistMassKg の処理を取得します。
    /// </summary>
    /// <returns>計算または参照した値を返します。</returns>
    private float GetCurrentConsistMassKg()
    {
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

    /// <summary>
    /// 役割: GetBrakeOutputs の処理を取得します。
    /// </summary>
    /// <param name="brakeNotch">brakeNotch を指定します。</param>
    /// <param name="useTascBrakeStep">TASC の連続ブレーキ段を使う場合は true を指定します。</param>
    /// <param name="tascBrakeStep">TASC の連続ブレーキ段を指定します。</param>
    /// <param name="massKg">massKg を指定します。</param>
    /// <param name="brakeDecelerationMS2">出力結果を受け取る brakeDecelerationMS2 です。</param>
    /// <param name="brakeForceN">出力結果を受け取る brakeForceN です。</param>
    /// <remarks>返り値はありません。</remarks>
    private void GetBrakeOutputs(int brakeNotch, bool useTascBrakeStep, int tascBrakeStep, float massKg, out float brakeDecelerationMS2, out float brakeForceN)
    {
        brakeDecelerationMS2 = 0f;
        brakeForceN = 0f;

        if (brakeSystem != null)
        {
            brakeDecelerationMS2 = brakeSystem.TotalBrakeDecelMS2;
            brakeForceN = brakeSystem.TotalBrakeForceN;
            return;
        }

        if (brakeNotch <= 0 && (!useTascBrakeStep || tascBrakeStep <= 0))
        {
            return;
        }

        brakeDecelerationMS2 = useTascBrakeStep
            ? trainSpec.GetTascBrakeStepDeceleration(tascBrakeStep)
            : trainSpec.GetBrakeDeceleration(brakeNotch);
        brakeForceN = Mathf.Max(0f, brakeDecelerationMS2) * massKg;
    }

    /// <summary>
    /// 役割: GetExternalResistanceForceN の処理を取得します。
    /// </summary>
    /// <param name="powerNotch">powerNotch を指定します。</param>
    /// <param name="brakeDecelerationMS2">brakeDecelerationMS2 を指定します。</param>
    /// <returns>計算または参照した値を返します。</returns>
    private float GetExternalResistanceForceN(int powerNotch, float brakeDecelerationMS2)
    {
        float runningResistanceForceN = ExternalForceCalculator.GetRunningResistanceForceN(trainSpec, speedMS);
        float coastResistanceForceN = 0f;
        if (powerNotch <= 0 && brakeDecelerationMS2 <= 0f)
        {
            // 惰行フィール維持: 力行も制動もない時だけ追加抵抗を加算する。
            coastResistanceForceN = ExternalForceCalculator.GetCoastExtraResistanceForceN(trainSpec, speedMS);
        }

        return runningResistanceForceN + coastResistanceForceN;
    }

    /// <summary>
    /// 役割: GetTractionForceN の処理を取得します。
    /// </summary>
    /// <param name="powerNotch">powerNotch を指定します。</param>
    /// <param name="massKg">massKg を指定します。</param>
    /// <param name="externalForceN">externalForceN を指定します。</param>
    /// <returns>計算または参照した値を返します。</returns>
    private float GetTractionForceN(int powerNotch, float massKg, float externalForceN)
    {
        if (tractionSystem != null)
        {
            tractionSystem.UpdateTraction(powerNotch, speedMS, externalForceN);
            return tractionSystem.CurrentTotalTractionForceN;
        }

        return trainSpec.GetTractionDemandForceN(
            powerNotch,
            speedMS,
            massKg,
            externalForceN
        );
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
        speedMS = Mathf.Clamp(speedMS, 0f, trainSpec.maxSpeedMS);

        // 速度更新後の値で距離を進めることで、停止直前の負速度混入を避ける。
        float deltaDistanceM = speedMS * Time.deltaTime;
        distance += deltaDistanceM;
        distanceOnEdgeM += deltaDistanceM;
        AdvanceEdgeTransitionIfNeeded();
    }
}
