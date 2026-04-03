using System.Text;
using TMPro;
using UnityEngine;

public class TrainHUD : MonoBehaviour
{
    [SerializeField] private TrainController train;
    [SerializeField] private ATCController atc;
    [SerializeField] private TMP_Text hudText;

    void Update()
    {
        if (train == null || hudText == null)
        {
            return;
        }

        string atcLimitText = "--";
        if (atc != null)
        {
            atcLimitText = $"{atc.CurrentLimitSpeedKmH:0.0} km/h";
        }

        float totalBrakeForceKN = train.CurrentBrakeForceN / 1000f;
        float regenBrakeForceKN = train.CurrentRegenBrakeForceN / 1000f;
        float airBrakeForceKN = train.CurrentAirBrakeForceN / 1000f;
        var carBrakeStates = train.CurrentCarBrakeStates;

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

        hudText.text =
            "[Train]\n" +
            $"Speed: {train.SpeedKmH:0.0} km/h\n" +
            $"Distance: {train.DistanceM:0.0} m\n" +
            $"Accel: {train.CurrentAccelerationMS2:+0.00;-0.00;0.00} m/s^2\n" +
            "\n" +
            "[Control]\n" +
            $"Applied: P{train.PowerNotch} / B{train.BrakeNotch}\n" +
            $"Manual: P{train.ManualPowerNotch} / B{train.ManualBrakeNotch}\n" +
            $"ATC Cmd: B{train.ATCBrakeNotch}\n" +
            "\n" +
            "[Safety]\n" +
            $"ATC Limit: {atcLimitText}\n" +
            "\n" +
            "[Brake]\n" +
            $"Total: {totalBrakeForceKN:0.0} kN | {train.CurrentBrakeDecelMS2:0.00} m/s^2\n" +
            $"Regen: {regenBrakeForceKN:0.0} kN | {train.CurrentRegenBrakeDecelMS2:0.00} m/s^2\n" +
            $"Air: {airBrakeForceKN:0.0} kN | {train.CurrentAirBrakeDecelMS2:0.00} m/s^2\n" +
            $"BC: {train.CurrentBCPressureKPa:0.0} kPa\n" +
            "\n" +
            carBrakeSection.ToString();
    }
}
