using System.Text;
using TMPro;
using UnityEngine;

public class TrainHUD : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TrainController train;
    [SerializeField] private ATCController atc;
    [SerializeField] private TASCController tasc;
    [SerializeField] private NTIMController ntim;
    [SerializeField] private VVVFController[] vvvfControllers;
    [SerializeField] private StationStopController stationStop;
    [SerializeField] private BlockOccupancyManager blockOccupancyManager;
    [SerializeField] private TMP_Text hudText;

    /// <summary>
    /// 役割: FormatBrakeNotchLabel の処理を表示用に整形します。
    /// </summary>
    /// <param name="notch">notch を指定します。</param>
    /// <returns>文字列結果を返します。</returns>
    private string FormatBrakeNotchLabel(int notch)
    {
        if (train != null && notch >= train.EmergencyBrakeNotch)
        {
            return "EB";
        }

        return $"B{notch}";
    }

    /// <summary>
    /// 役割: 毎フレームの更新処理を行います。
    /// </summary>
    /// <remarks>返り値はありません。</remarks>
    void Update()
    {
        ResolveReferences();

        if (train == null || hudText == null)
        {
            return;
        }

        if (vvvfControllers == null || vvvfControllers.Length == 0)
        {
            vvvfControllers = train.GetComponentsInChildren<VVVFController>();
        }

        string atcLimitText = "--";
        string atcSourceText = "--";
        string atcStateText = "--";
        string atcPatternText = "--";
        string atcEmergencyPatternText = "--";
        string atcTargetDistanceText = "--";
        string atcBrakeLatchedText = "--";
        if (atc != null)
        {
            atcLimitText = $"{atc.CurrentLimitSpeedKmH:0.0} km/h";
            atcSourceText = atc.CurrentPatternSourceLabel;
            atcStateText = atc.CurrentAtcStateLabel;
            atcPatternText = $"{atc.CurrentPatternAllowSpeedKmH:0.0} km/h";
            atcEmergencyPatternText = $"{atc.CurrentPatternEmergencyAllowSpeedKmH:0.0} km/h";
            atcTargetDistanceText = $"{atc.CurrentPatternTargetDistanceM:0.0} m";
            atcBrakeLatchedText = atc.IsAtcBrakeLatched ? "On" : "Off";
        }

        float totalBrakeForceKN = train.CurrentBrakeForceN / 1000f;
        float regenBrakeForceKN = train.CurrentRegenBrakeForceN / 1000f;
        float airBrakeForceKN = train.CurrentAirBrakeForceN / 1000f;
        float tractionForceKN = train.CurrentTractionForceN / 1000f;
        var carBrakeStates = train.CurrentCarBrakeStates;

        StringBuilder tascSection = new StringBuilder();
        tascSection.Append("[TASC]\n");
        if (tasc == null)
        {
            tascSection.Append("Status: --\n");
            tascSection.Append("Step: --\n");
            tascSection.Append("Target Step: --\n");
            tascSection.Append("Base Step: --\n");
            tascSection.Append("Pattern: --\n");
            tascSection.Append("Speed Error: --\n");
            tascSection.Append("Target Distance: --\n");
        }
        else
        {
            tascSection.Append($"Status: {tasc.CurrentControlModeLabel}\n");
            tascSection.Append($"Step: B{tasc.CurrentTascBrakeStep} ({FormatBrakeNotchLabel(tasc.CurrentTascBrakeNotch)})\n");
            tascSection.Append($"Target Step: B{tasc.CurrentTargetTascBrakeStep}\n");
            tascSection.Append($"Base Step: B{tasc.CurrentBaseTascBrakeStep}\n");
            tascSection.Append($"Pattern: {tasc.CurrentPatternAllowSpeedKmH:0.0} km/h\n");
            tascSection.Append($"Speed Error: {tasc.CurrentSpeedErrorKmH:+0.0;-0.0;0.0} km/h\n");
            tascSection.Append($"Target Distance: {tasc.CurrentTargetDistanceM:0.0} m\n");
        }

        // 駅関連の表示は独立したブロックにまとめ、運転中でも手動確認しやすくしています。
        // これにより、対象駅の更新、通過駅のスキップ、過走挙動を一か所で追えます。
        StringBuilder stationSection = new StringBuilder();
        stationSection.Append("[Station]\n");
        if (stationStop == null)
        {
            stationSection.Append("Controller: --\n");
        }
        else
        {
            stationSection.Append($"Next: {stationStop.CurrentTargetStationName}\n");
            stationSection.Append(
                stationStop.HasTargetStation
                    ? $"Distance: {stationStop.DistanceToStopM:+0.0;-0.0;0.0} m\n"
                    : "Distance: --\n"
            );
            stationSection.Append(
                stationStop.HasTargetStation
                    ? $"Error: {stationStop.StopErrorM:+0.0;-0.0;0.0} m\n"
                    : "Error: --\n"
            );
            stationSection.Append($"Judge: {stationStop.JudgeStateLabel}\n");
            stationSection.Append($"Hold: {stationStop.StopHoldTimer:0.0} / {stationStop.StopHoldSeconds:0.0} s\n");
            stationSection.Append($"Index: {stationStop.CurrentStopIndex} -> {stationStop.ResolvedStopIndex}\n");
            stationSection.Append(
                stationStop.LastCompletedStationName != "--"
                    ? $"Last: {stationStop.LastCompletedStationName} ({stationStop.LastCompletedStopErrorM:+0.0;-0.0;0.0} m)\n"
                    : "Last: --\n"
            );
        }

        StringBuilder blockSection = new StringBuilder();
        blockSection.Append("[Block]\n");
        if (blockOccupancyManager == null)
        {
            blockSection.Append("Occupied: --\n");
            blockSection.Append("Ahead: --\n");
            blockSection.Append("Ahead Distance: --\n");
        }
        else
        {
            blockSection.Append($"Occupied: {blockOccupancyManager.GetOccupiedBlocksLabel(train)}\n");

            if (blockOccupancyManager.TryFindFirstOccupiedBlockAhead(
                train,
                out string occupiedBlockId,
                out float distanceToBlockM
            ))
            {
                blockSection.Append($"Ahead: {occupiedBlockId}\n");
                blockSection.Append($"Ahead Distance: {distanceToBlockM:0.0} m\n");
            }
            else
            {
                blockSection.Append("Ahead: --\n");
                blockSection.Append("Ahead Distance: --\n");
            }
        }

        StringBuilder carBrakeSection = new StringBuilder();
        carBrakeSection.Append("[Brake Cars]\n");
        if (carBrakeStates == null || carBrakeStates.Count == 0)
        {
            carBrakeSection.Append("No per-car brake data\n");
        }
        else
        {
            for (int i = 0; i < carBrakeStates.Count; i++)
            {
                CarBrakeState state = carBrakeStates[i];
                if (state == null)
                {
                    carBrakeSection.Append($"C{i + 1:00}: --\n");
                    continue;
                }

                float carRegenKN = state.regenForceN / 1000f;
                float carAirKN = state.airForceN / 1000f;
                float carTotalKN = (state.regenForceN + state.airForceN) / 1000f;
                carBrakeSection.Append(
                    $"C{i + 1:00}: {carTotalKN:0.0} kN (R {carRegenKN:0.0} / A {carAirKN:0.0}) | BC {state.bcPressureKPa:0.0} kPa\n"
                );
            }
        }

        float totalVvvfTargetForceKN = 0f;
        float totalVvvfMotorForceKN = 0f;
        float firstTargetTorqueNm = 0f;
        float firstMotorTorqueNm = 0f;
        float firstSlipPercent = 0f;
        float firstLineVoltageV = 0f;
        float firstFrequencyHz = 0f;
        float firstRotorBaseFrequencyHz = 0f;
        float firstSlipFrequencyHz = 0f;
        float firstPowerMaxSlipFrequencyHz = 0f;
        float firstRegenMaxSlipFrequencyHz = 0f;
        string firstDriveModeLabel = "--";
        int vvvfCount = 0;
        if (vvvfControllers != null)
        {
            for (int i = 0; i < vvvfControllers.Length; i++)
            {
                VVVFController vvvf = vvvfControllers[i];
                if (vvvf == null)
                {
                    continue;
                }

                vvvfCount++;
                totalVvvfTargetForceKN += vvvf.TargetTractionForceN / 1000f;
                totalVvvfMotorForceKN += vvvf.TotalMotorTractionForceN / 1000f;

                if (vvvfCount == 1)
                {
                    MotorModel motor = vvvf.PrimaryMotor;
                    firstTargetTorqueNm = vvvf.TargetMotorTorqueNm;
                    firstMotorTorqueNm = motor != null ? motor.MotorTorqueNm : 0f;
                    firstSlipPercent = vvvf.SlipRatio * 100f;
                    firstLineVoltageV = vvvf.LineVoltageRmsV;
                    firstFrequencyHz = vvvf.FrequencyHz;
                    firstRotorBaseFrequencyHz = vvvf.RotorBaseFrequencyHz;
                    firstSlipFrequencyHz = vvvf.SlipFrequencyHz;
                    firstPowerMaxSlipFrequencyHz = vvvf.PowerMaxSlipFrequencyHz;
                    firstRegenMaxSlipFrequencyHz = vvvf.RegenMaxSlipFrequencyHz;
                    firstDriveModeLabel = vvvf.DriveModeLabel;
                }
            }
        }

        StringBuilder ntimSection = new StringBuilder();
        ntimSection.Append("[NTIM]\n");
        if (ntim == null)
        {
            ntimSection.Append("Controller: --\n");
        }
        else
        {
            ntimSection.Append($"Region: {ntim.CurrentRegionLabel}\n");
            ntimSection.Append($"Target Force: {ntim.TargetForceN / 1000f:0.0} kN\n");
            ntimSection.Append($"Per VVVF: {ntim.TargetForcePerVvvfN / 1000f:0.0} kN\n");
            ntimSection.Append($"Target Accel: {ntim.TargetAccelerationMS2:0.000} m/s^2\n");
            ntimSection.Append($"Rated Power: {ntim.CurrentRatedConsistPowerW / 1000f:0.0} kW\n");
            ntimSection.Append($"Active VVVF: {ntim.ActiveVvvfCount}\n");
        }

        StringBuilder speedHoldSection = new StringBuilder();
        speedHoldSection.Append("[Speed Hold]\n");
        if (ntim == null)
        {
            speedHoldSection.Append("State: --\n");
            speedHoldSection.Append("Target: --\n");
            speedHoldSection.Append("Error: --\n");
        }
        else
        {
            speedHoldSection.Append($"State: {ntim.SpeedHoldStateLabel}\n");
            if (ntim.SpeedHoldActive)
            {
                float targetSpeedKmH = ntim.SpeedHoldTargetMS * 3.6f;
                float speedErrorKmH = targetSpeedKmH - train.SpeedKmH;
                speedHoldSection.Append($"Target: {targetSpeedKmH:0.0} km/h\n");
                speedHoldSection.Append($"Error: {speedErrorKmH:+0.0;-0.0;0.0} km/h\n");
            }
            else
            {
                speedHoldSection.Append("Target: --\n");
                speedHoldSection.Append("Error: --\n");
            }
        }

        StringBuilder driveSection = new StringBuilder();
        driveSection.Append("[Drive Debug]\n");
        driveSection.Append($"VVVF Count: {vvvfCount}\n");
        driveSection.Append($"Target Sum: {totalVvvfTargetForceKN:0.0} kN\n");
        driveSection.Append($"Motor Sum: {totalVvvfMotorForceKN:0.0} kN\n");
        driveSection.Append($"First Target T: {firstTargetTorqueNm:0.0} Nm\n");
        driveSection.Append($"First Motor T: {firstMotorTorqueNm:0.0} Nm\n");
        driveSection.Append($"First Mode: {firstDriveModeLabel}\n");
        driveSection.Append($"First Slip: {firstSlipPercent:0.00}%\n");
        driveSection.Append($"First Freq: {firstFrequencyHz:0.0} Hz\n");
        driveSection.Append($"First Base F: {firstRotorBaseFrequencyHz:0.0} Hz\n");
        driveSection.Append($"First Slip F: {firstSlipFrequencyHz:+0.00;-0.00;0.00} Hz\n");
        driveSection.Append($"Slip Max P/R: {firstPowerMaxSlipFrequencyHz:0.00} / {firstRegenMaxSlipFrequencyHz:0.00} Hz\n");
        driveSection.Append($"First Line V: {firstLineVoltageV:0.0} V\n");
        if (vvvfControllers != null)
        {
            for (int i = 0; i < vvvfControllers.Length; i++)
            {
                VVVFController vvvf = vvvfControllers[i];
                if (vvvf == null)
                {
                    continue;
                }

                MotorModel[] motors = vvvf.Motors;
                if (motors == null || motors.Length == 0)
                {
                    driveSection.Append($"VVVF {i + 1}: no motors\n");
                    continue;
                }

                driveSection.Append($"VVVF {i + 1} I:");
                for (int motorIndex = 0; motorIndex < motors.Length; motorIndex++)
                {
                    MotorModel motor = motors[motorIndex];
                    float currentA = motor != null ? motor.MotorCurrentRmsA : 0f;
                    driveSection.Append($" M{motorIndex + 1} {currentA:0}");
                }
                driveSection.Append(" A\n");
            }
        }

        hudText.text =
            "[Train]\n" +
            $"Speed: {train.SpeedKmH:0.0} km/h\n" +
            $"Distance: {train.DistanceM:0.0} m\n" +
            $"Accel: {train.CurrentAccelerationMS2:+0.00;-0.00;0.00} m/s^2\n" +
            $"Gradient: {train.CurrentGradientPermille:+0.0;-0.0;0.0} permille\n" +
            $"Cant: {train.CurrentCantMm:+0.0;-0.0;0.0} mm\n" +
            $"Traction Force: {tractionForceKN:0.0} kN\n" +
            $"Grade Force: {train.CurrentGradeResistanceForceN / 1000f:+0.0;-0.0;0.0} kN\n" +
            "\n" +
            "[Control]\n" +
            $"Reverser: {train.Reverser}\n" +
            $"Applied: P{train.PowerNotch} / {FormatBrakeNotchLabel(train.BrakeNotch)}\n" +
            $"Manual: P{train.ManualPowerNotch} / {FormatBrakeNotchLabel(train.ManualBrakeNotch)}\n" +
            $"ATC Cmd: {FormatBrakeNotchLabel(train.ATCBrakeNotch)}\n" +
            "\n" +
            "[Safety]\n" +
            $"ATC Source: {atcSourceText}\n" +
            $"ATC State: {atcStateText}\n" +
            $"ATC Limit: {atcLimitText}\n" +
            $"ATC Pattern: {atcPatternText}\n" +
            $"ATC Emergency Pattern: {atcEmergencyPatternText}\n" +
            $"ATC Target Distance: {atcTargetDistanceText}\n" +
            $"ATC Brake Latched: {atcBrakeLatchedText}\n" +
            "\n" +
            tascSection.ToString() +
            "\n" +
            stationSection.ToString() +
            "\n" +
            speedHoldSection.ToString() +
            "\n" +
            ntimSection.ToString() +
            "\n" +
            driveSection.ToString() +
            "\n" +
            blockSection.ToString() +
            "\n" +
            "[Brake]\n" +
            $"Total: {totalBrakeForceKN:0.0} kN | {train.CurrentBrakeDecelMS2:0.00} m/s^2\n" +
            $"Regen: {regenBrakeForceKN:0.0} kN | {train.CurrentRegenBrakeDecelMS2:0.00} m/s^2\n" +
            $"Air: {airBrakeForceKN:0.0} kN | {train.CurrentAirBrakeDecelMS2:0.00} m/s^2\n" +
            $"BC: {train.CurrentBCPressureKPa:0.0} kPa | Target: {train.CurrentTargetBCPressureKPa:0.0} kPa\n" +
            "\n" +
            carBrakeSection.ToString();
    }

    private void ResolveReferences()
    {
        train = CabReferenceResolver.ResolveTrain(this, train);
        atc = CabReferenceResolver.ResolveTrainComponent(this, train, atc);
        tasc = CabReferenceResolver.ResolveTrainComponent(this, train, tasc);
        ntim = CabReferenceResolver.ResolveTrainComponent(this, train, ntim);
    }
}
