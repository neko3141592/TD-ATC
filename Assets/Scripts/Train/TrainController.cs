using System.Collections.Generic;
using UnityEngine;

public class TrainController : MonoBehaviour
{
    [SerializeField] private TrainSpec trainSpec;
    [SerializeField] private NotchManager notchManager;
    [SerializeField] private BrakeSystemController brakeSystem;
    [SerializeField] private TractionSystemController tractionSystem;
    [SerializeField] private TrackGraph trackGraph;
    [SerializeField] private string currentEdgeId;
    [SerializeField] private float speedMS = 0f; // 現在速度 (m/s)
    [SerializeField, Min(0f)] private float distanceOnEdgeM = 0f;
    private float distance = 0f;   // スタートからの累計走行距離 (m)

    private float currentAccelerationMS2 = 0f;

    private TrackRuntimeResolver resolver;

    private readonly List<TrackEdge> activeEdges = new List<TrackEdge>();
    
    [Header("Consist Configuration")]
    [SerializeField] private ConsistDefinition consistDefinition;
    [SerializeField, Min(1f)] private float defaultCarLengthM = 20f;

    [SerializeField]
    private List<CarTrackState> carTrackStates = new List<CarTrackState>();


    public float SpeedKmH => speedMS * 3.6f;
    public float SpeedMS => speedMS;
    public float DistanceM => distance;
    public TrackGraph Graph => trackGraph;
    public string CurrentEdgeId => currentEdgeId;
    public float DistanceOnEdgeM => distanceOnEdgeM;
    public TrainSpec Spec => trainSpec;
    public int PowerNotch => notchManager != null ? notchManager.ResolvedPowerNotch : 0;
    public int BrakeNotch => notchManager != null ? notchManager.ResolvedBrakeNotch : 0;
    public int ManualPowerNotch => notchManager != null ? notchManager.ManualPowerNotch : 0;
    public int ManualBrakeNotch => notchManager != null ? notchManager.ManualBrakeNotch : 0;
    public int ATCBrakeNotch => notchManager != null ? notchManager.ATCBrakeNotch : 0;
    public int EmergencyBrakeNotch => trainSpec != null ? trainSpec.GetEmergencyBrakeNotch() : 9;
    public bool IsEmergencyBrakeActive => BrakeNotch >= EmergencyBrakeNotch;
    public float CurrentBrakeDecelMS2 => brakeSystem != null ? brakeSystem.TotalBrakeDecelMS2 : 0f;
    public float CurrentRegenBrakeDecelMS2 => brakeSystem != null ? brakeSystem.CurrentRegenDecelMS2 : 0f;
    public float CurrentAirBrakeDecelMS2 => brakeSystem != null ? brakeSystem.CurrentAirDecelMS2 : 0f;
    public float CurrentBrakeForceN => brakeSystem != null ? brakeSystem.TotalBrakeForceN : 0f;
    public float CurrentRegenBrakeForceN => brakeSystem != null ? brakeSystem.CurrentRegenForceN : 0f;
    public float CurrentAirBrakeForceN => brakeSystem != null ? brakeSystem.CurrentAirForceN : 0f;
    public float CurrentTractionForceN => tractionSystem != null ? tractionSystem.CurrentTotalTractionForceN : 0f;
    public float CurrentBCPressureKPa => brakeSystem != null ? brakeSystem.CurrentBCPressureKPa : 0f;
    public float CurrentAccelerationMS2 => currentAccelerationMS2;
    public IReadOnlyList<CarBrakeState> CurrentCarBrakeStates => brakeSystem != null ? brakeSystem.CarBrakeStates : null;
    public IReadOnlyList<CarTractionState> CurrentCarTractionStates => tractionSystem != null ? tractionSystem.CarTractionStates : null;
    public IReadOnlyList<TrackEdge> ActiveEdges => activeEdges;
    public IReadOnlyList<CarTrackState> CarTrackStates => carTrackStates;
    public ConsistDefinition ConsistDefinition => ResolveConsistDefinition();

    void Awake()
    {
        if (trainSpec == null)
        {
            Debug.LogError($"{nameof(TrainController)} on {name}: TrainSpec is not assigned.", this);
            enabled = false;
            return;
        }

        ResolveControllerReferences();
        EnsureRuntimeResolver();
        InitializeTrackState();

        SyncCarTrackStatesWithConsist();
        notchManager.ConfigureLimits(trainSpec.maxPowerNotch, EmergencyBrakeNotch);
    }

    void Update()
    {
        HandleInput();    // 1. 入力を処理
        ApplyPhysics();   // 2. 物理計算（速度と距離）
        MoveTrain(); 
    }


    void HandleInput()
    {
        if (notchManager == null)
        {
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

    void ApplyPhysics()
    {
        notchManager.ConfigureLimits(trainSpec.maxPowerNotch, EmergencyBrakeNotch);

        int powerNotch = PowerNotch; // 高位優先解決後の力行ノッチ
        int brakeNotch = BrakeNotch; // 高位優先解決後の制動ノッチ
        bool isEmergencyBrake = brakeNotch >= EmergencyBrakeNotch;

        if (brakeSystem != null)
        {
            brakeSystem.UpdateBrake(brakeNotch, speedMS, Time.deltaTime, isEmergencyBrake);
        }

        float massKg = GetCurrentConsistMassKg();
        GetBrakeOutputs(brakeNotch, massKg, out float brakeDeceleration, out float brakeForceN);

        float externalForceN = GetExternalResistanceForceN(powerNotch, brakeDeceleration);
        float tractionForceN = GetTractionForceN(powerNotch, massKg, externalForceN);
        float vehicleForceN = tractionForceN - brakeForceN; // 車両側で作る合成力[N]

        float netForceN = vehicleForceN - externalForceN; // 外力込みの正味合力[N]
        float acceleration = netForceN / massKg; // 正味加速度[m/s^2]

        IntegrateMotion(acceleration);
    }
    void MoveTrain()
    {
        SyncCarTrackStatesWithConsist();
        EnsureRuntimeResolver();

        if (!TryResolveHeadPose(out Vector3 pos, out Vector3 tan))
        {
            return;
        }

        ApplyHeadPose(pos, tan);

        EnsureActiveEdgeHistory(GetRequiredHistoryLengthM());
        UpdateCarTrackStates();
    }

    private void AdvanceEdgeTransitionIfNeeded()
    {
        if (trackGraph == null || string.IsNullOrEmpty(currentEdgeId))
        {
            return;
        }

        const int maxTransitionsPerFrame = 256;
        int guard = 0;

        while (guard < maxTransitionsPerFrame)
        {
            guard++;

            TrackEdge currentEdge = trackGraph.FindEdge(currentEdgeId);
            if (currentEdge == null)
            {
                break;
            }

            float edgeLengthM = Mathf.Max(0f, currentEdge.lengthM);
            if (distanceOnEdgeM <= edgeLengthM)
            {
                break;
            }

            // 端を超えた分を次エッジへ繰り越す
            float remainDistanceM = distanceOnEdgeM - edgeLengthM;
            string nextEdgeId = trackGraph.ResolveNextEdgeId(currentEdge.toNodeId, currentEdgeId);

            if (string.IsNullOrEmpty(nextEdgeId))
            {
                // 先が無ければ端で止める
                distanceOnEdgeM = edgeLengthM;
                break;
            }
            
            currentEdgeId = nextEdgeId;
            distanceOnEdgeM = remainDistanceM;

            TrackEdge newEdge = trackGraph.FindEdge(nextEdgeId);
            if (newEdge != null)
            {
                activeEdges.Insert(0, newEdge);
            }
        }

        if (guard >= maxTransitionsPerFrame)
        {
            Debug.LogWarning($"{nameof(TrainController)} on {name}: edge transition loop reached guard limit.", this);
        }
    }

    public bool TryGetPositionBehind(float offsetM, out string edgeId, out float distOnEdge)
    {
        edgeId = currentEdgeId;
        distOnEdge = distanceOnEdgeM;

        if (activeEdges.Count == 0)
        {
            return false;
        }

        float currentOffset = offsetM;

        for (int i = 0; i < activeEdges.Count; i++)
        {
            TrackEdge edge = activeEdges[i];
            if (edge == null)
            {
                continue;
            }

            float currentEdgeLength = (i == 0) ? distanceOnEdgeM : edge.lengthM;

            if (currentOffset <= currentEdgeLength)
            {
                edgeId = edge.edgeId;
                distOnEdge = currentEdgeLength - currentOffset;
                return true;
            }
            else
            {
                currentOffset -= currentEdgeLength;
            }
        }

        edgeId = activeEdges[activeEdges.Count - 1].edgeId;
        distOnEdge = 0f;
        return true;
    }

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

    private void GetBrakeOutputs(int brakeNotch, float massKg, out float brakeDecelerationMS2, out float brakeForceN)
    {
        brakeDecelerationMS2 = 0f;
        brakeForceN = 0f;

        if (brakeSystem != null)
        {
            brakeDecelerationMS2 = brakeSystem.TotalBrakeDecelMS2;
            brakeForceN = brakeSystem.TotalBrakeForceN;
            return;
        }

        if (brakeNotch <= 0)
        {
            return;
        }

        brakeDecelerationMS2 = trainSpec.GetBrakeDeceleration(brakeNotch);
        brakeForceN = Mathf.Max(0f, brakeDecelerationMS2) * massKg;
    }

    private float GetExternalResistanceForceN(int powerNotch, float brakeDecelerationMS2)
    {
        float runningResistanceForceN = ExternalForceCalculator.GetRunningResistanceForceN(trainSpec, speedMS);
        float coastResistanceForceN = 0f;
        if (powerNotch <= 0 && brakeDecelerationMS2 <= 0f)
        {
            // 既存の惰行フィールを維持するため、暫定で追加抵抗として扱う
            coastResistanceForceN = ExternalForceCalculator.GetCoastExtraResistanceForceN(trainSpec, speedMS);
        }

        return runningResistanceForceN + coastResistanceForceN;
    }

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

    private void IntegrateMotion(float acceleration)
    {
        currentAccelerationMS2 = acceleration;
        speedMS += acceleration * Time.deltaTime;
        speedMS = Mathf.Clamp(speedMS, 0f, trainSpec.maxSpeedMS);

        float deltaDistanceM = speedMS * Time.deltaTime;
        distance += deltaDistanceM;
        distanceOnEdgeM += deltaDistanceM;
        AdvanceEdgeTransitionIfNeeded();
    }

    private void SyncCarTrackStatesWithConsist()
    {
        ConsistDefinition resolvedConsistDefinition = ResolveConsistDefinition();
        EnsureCarTrackStateCount(GetTargetCarCount(resolvedConsistDefinition));
        RefreshCarOffsets(resolvedConsistDefinition);
    }

    private void ResolveControllerReferences()
    {
        if (notchManager == null)
        {
            notchManager = GetComponent<NotchManager>();
        }

        if (notchManager == null)
        {
            notchManager = gameObject.AddComponent<NotchManager>();
        }

        if (brakeSystem == null)
        {
            brakeSystem = GetComponent<BrakeSystemController>();
        }

        if (tractionSystem == null)
        {
            tractionSystem = GetComponent<TractionSystemController>();
        }
    }

    private void EnsureRuntimeResolver()
    {
        if (resolver == null)
        {
            resolver = new TrackRuntimeResolver();
        }
    }

    private void InitializeTrackState()
    {
        if (trackGraph != null && string.IsNullOrEmpty(currentEdgeId) && trackGraph.edges != null && trackGraph.edges.Count > 0)
        {
            currentEdgeId = trackGraph.edges[0].edgeId;
            distanceOnEdgeM = 0f;
        }

        if (trackGraph == null || string.IsNullOrEmpty(currentEdgeId) || activeEdges.Count > 0)
        {
            return;
        }

        TrackEdge initialEdge = trackGraph.FindEdge(currentEdgeId);
        if (initialEdge != null)
        {
            activeEdges.Add(initialEdge);
        }
    }

    private bool TryResolveHeadPose(out Vector3 pos, out Vector3 tan)
    {
        pos = default;
        tan = default;

        if (trackGraph == null)
        {
            Debug.LogError($"{nameof(TrainController)} on {name}: TrackGraph is not assigned.", this);
            return false;
        }

        if (string.IsNullOrEmpty(currentEdgeId))
        {
            Debug.LogError($"{nameof(TrainController)} on {name}: currentEdgeId is empty.", this);
            return false;
        }

        if (!resolver.TryResolvePose(trackGraph, currentEdgeId, distanceOnEdgeM, out pos, out tan))
        {
            Debug.LogError(
                $"{nameof(TrainController)} on {name}: failed to resolve pose. edgeId={currentEdgeId}, distanceOnEdgeM={distanceOnEdgeM:0.###}",
                this
            );
            return false;
        }

        return true;
    }

    private void ApplyHeadPose(Vector3 pos, Vector3 tan)
    {
        transform.position = pos;
        if (tan.sqrMagnitude > 0.000001f)
        {
            transform.rotation = Quaternion.LookRotation(tan);
        }
    }

    private void UpdateCarTrackStates()
    {
        for (int i = 0; i < carTrackStates.Count; i++)
        {
            UpdateCarTrackState(i);
        }
    }

    private void UpdateCarTrackState(int index)
    {
        CarTrackState state = carTrackStates[index];
        if (state == null)
        {
            return;
        }

        if (!TryGetPositionBehind(state.offsetFromHeadM, out string edgeId, out float distOnEdge))
        {
            return;
        }

        state.edgeId = edgeId;
        state.distanceOnEdgeM = distOnEdge;

        if (resolver.TryResolvePose(trackGraph, edgeId, distOnEdge, out Vector3 carPos, out Vector3 carTan))
        {
            state.position = carPos;
            state.tangent = carTan;
        }
    }

    private void EnsureCarTrackStateCount(int targetCarCount)
    {
        while (carTrackStates.Count < targetCarCount)
        {
            carTrackStates.Add(new CarTrackState());
        }

        while (carTrackStates.Count > targetCarCount)
        {
            carTrackStates.RemoveAt(carTrackStates.Count - 1);
        }
    }

    private void RefreshCarOffsets(ConsistDefinition resolvedConsistDefinition)
    {
        float accumulatedOffsetM = 0f;
        float previousCarLengthM = GetCarLengthM(resolvedConsistDefinition, 0);

        for (int i = 0; i < carTrackStates.Count; i++)
        {
            CarTrackState state = GetOrCreateCarTrackState(i);
            state.carIndex = i;
            if (i == 0)
            {
                state.offsetFromHeadM = 0f;
                previousCarLengthM = GetCarLengthM(resolvedConsistDefinition, i);
                continue;
            }

            float currentCarLengthM = GetCarLengthM(resolvedConsistDefinition, i);
            accumulatedOffsetM += 0.5f * (previousCarLengthM + currentCarLengthM);
            state.offsetFromHeadM = accumulatedOffsetM;
            previousCarLengthM = currentCarLengthM;
        }
    }

    private CarTrackState GetOrCreateCarTrackState(int index)
    {
        CarTrackState state = carTrackStates[index];
        if (state == null)
        {
            state = new CarTrackState();
            carTrackStates[index] = state;
        }

        return state;
    }

    private ConsistDefinition ResolveConsistDefinition()
    {
        if (consistDefinition != null && consistDefinition.HasCars)
        {
            return consistDefinition;
        }

        if (brakeSystem != null && brakeSystem.ConsistDefinition != null && brakeSystem.ConsistDefinition.HasCars)
        {
            return brakeSystem.ConsistDefinition;
        }

        if (tractionSystem != null && tractionSystem.ConsistDefinition != null && tractionSystem.ConsistDefinition.HasCars)
        {
            return tractionSystem.ConsistDefinition;
        }

        return consistDefinition;
    }

    private int GetTargetCarCount(ConsistDefinition resolvedConsistDefinition)
    {
        if (resolvedConsistDefinition != null && resolvedConsistDefinition.HasCars)
        {
            return resolvedConsistDefinition.CarCount;
        }

        return Mathf.Max(1, carTrackStates.Count);
    }

    private float GetCarLengthM(ConsistDefinition resolvedConsistDefinition, int index)
    {
        if (resolvedConsistDefinition != null &&
            resolvedConsistDefinition.TryGetCar(index, out CarSpec carSpec) &&
            carSpec != null)
        {
            return Mathf.Max(1f, carSpec.lengthM);
        }

        return Mathf.Max(1f, defaultCarLengthM);
    }

    private float GetRequiredHistoryLengthM()
    {
        if (carTrackStates == null || carTrackStates.Count == 0)
        {
            return 0f;
        }

        CarTrackState tailState = carTrackStates[carTrackStates.Count - 1];
        if (tailState == null)
        {
            return 0f;
        }

        return Mathf.Max(0f, tailState.offsetFromHeadM);
    }

    private void EnsureActiveEdgeHistory(float requiredOffsetM)
    {
        if (trackGraph == null || activeEdges.Count == 0 || requiredOffsetM <= 0f)
        {
            return;
        }

        float coveredDistanceM = GetTrackedHistoryLengthM();
        const int maxBackfillEdges = 512;
        int guard = 0;
        while (coveredDistanceM < requiredOffsetM && guard < maxBackfillEdges)
        {
            guard++;

            TrackEdge oldestTrackedEdge = activeEdges[activeEdges.Count - 1];
            if (oldestTrackedEdge == null || string.IsNullOrEmpty(oldestTrackedEdge.fromNodeId))
            {
                break;
            }

            string previousEdgeId = trackGraph.ResolvePreviousEdgeId(oldestTrackedEdge.fromNodeId, oldestTrackedEdge.edgeId);
            if (string.IsNullOrEmpty(previousEdgeId))
            {
                break;
            }

            TrackEdge previousEdge = trackGraph.FindEdge(previousEdgeId);
            if (previousEdge == null || ContainsTrackedEdge(previousEdge.edgeId))
            {
                break;
            }

            activeEdges.Add(previousEdge);
            coveredDistanceM += Mathf.Max(0f, previousEdge.lengthM);
        }

        if (guard >= maxBackfillEdges)
        {
            Debug.LogWarning($"{nameof(TrainController)} on {name}: active edge history backfill reached guard limit.", this);
        }
    }

    private float GetTrackedHistoryLengthM()
    {
        float coveredDistanceM = Mathf.Max(0f, distanceOnEdgeM);
        for (int i = 1; i < activeEdges.Count; i++)
        {
            TrackEdge trackedEdge = activeEdges[i];
            if (trackedEdge != null)
            {
                coveredDistanceM += Mathf.Max(0f, trackedEdge.lengthM);
            }
        }

        return coveredDistanceM;
    }

    private bool ContainsTrackedEdge(string edgeId)
    {
        if (string.IsNullOrEmpty(edgeId))
        {
            return false;
        }

        for (int i = 0; i < activeEdges.Count; i++)
        {
            TrackEdge trackedEdge = activeEdges[i];
            if (trackedEdge != null && trackedEdge.edgeId == edgeId)
            {
                return true;
            }
        }

        return false;
    }
}
