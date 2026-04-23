using System.Collections.Generic;
using UnityEngine;

public class TrainController : MonoBehaviour
{
    [SerializeField] private TrainSpec trainSpec;
    [SerializeField] private NotchManager notchManager;
    [SerializeField] private BrakeSystemController brakeSystem;
    [SerializeField] private TractionSystemController tractionSystem;
    [SerializeField] private TrackGraph trackGraph;
    [SerializeField] private string trainId = "PlayerTrain";
    [SerializeField] private string currentEdgeId;
    [SerializeField] private float speedMS = 0f; 
    [SerializeField, Min(0f)] private float distanceOnEdgeM = 0f;
    private float distance = 0f;

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
    public string TrainId => string.IsNullOrWhiteSpace(trainId) ? name : trainId;
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
    public IReadOnlyList<CarTrackState> CarTrackStates => carTrackStates;
    public ConsistDefinition ConsistDefinition => ResolveConsistDefinition();


    /// <summary>
    /// 役割: コンポーネント初期化時の準備を行います。
    /// </summary>
    /// <remarks>返り値はありません。</remarks>
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

    /// <summary>
    /// 役割: 毎フレームの更新処理を行います。
    /// </summary>
    /// <remarks>返り値はありません。</remarks>
    void Update()
    {
        HandleInput();    
        ApplyPhysics();  
        MoveTrain(); 
    }


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
        notchManager.ConfigureLimits(trainSpec.maxPowerNotch, EmergencyBrakeNotch);

        int powerNotch = PowerNotch; 
        int brakeNotch = BrakeNotch; 
        bool isEmergencyBrake = brakeNotch >= EmergencyBrakeNotch;

        if (brakeSystem != null)
        {
            brakeSystem.UpdateBrake(brakeNotch, speedMS, Time.deltaTime, isEmergencyBrake);
        }

        float massKg = GetCurrentConsistMassKg();
        GetBrakeOutputs(brakeNotch, massKg, out float brakeDeceleration, out float brakeForceN);

        float externalForceN = GetExternalResistanceForceN(powerNotch, brakeDeceleration);
        float tractionForceN = GetTractionForceN(powerNotch, massKg, externalForceN);
        float vehicleForceN = tractionForceN - brakeForceN;

        float netForceN = vehicleForceN - externalForceN; 
        float acceleration = netForceN / massKg; 

        IntegrateMotion(acceleration);
    }
    /// <summary>
    /// 役割: MoveTrain の処理を移動処理を行います。
    /// </summary>
    /// <remarks>返り値はありません。</remarks>
    void MoveTrain()
    {
        SyncCarTrackStatesWithConsist();
        EnsureRuntimeResolver();

        if (!TryResolveHeadPose(out Vector3 pos, out Vector3 tan))
        {
            return;
        }

        ApplyHeadPose(pos, tan);

        float requiredHistoryLengthM = GetRequiredHistoryLengthM();
        EnsureActiveEdgeHistory(requiredHistoryLengthM);
        TrimActiveEdgeHistory(requiredHistoryLengthM);
        UpdateCarTrackStates();
    }

    /// <summary>
    /// 役割: AdvanceEdgeTransitionIfNeeded の処理を進めます。
    /// </summary>
    /// <remarks>返り値はありません。</remarks>
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

            float remainDistanceM = distanceOnEdgeM - edgeLengthM;
            string nextEdgeId = trackGraph.ResolveNextEdgeId(currentEdge.toNodeId, currentEdgeId);

            if (string.IsNullOrEmpty(nextEdgeId))
            {
                
                distanceOnEdgeM = edgeLengthM;
                speedMS = 0f;
                currentAccelerationMS2 = 0f;
                break;
            }

            TrackEdge newEdge = trackGraph.FindEdge(nextEdgeId);
            if (newEdge == null)
            {
                distanceOnEdgeM = edgeLengthM;
                speedMS = 0f;
                currentAccelerationMS2 = 0f;
                Debug.LogWarning(
                    $"{nameof(TrainController)} on {name}: resolved next edge '{nextEdgeId}' was not found. Stopping at end of edge '{currentEdgeId}'.",
                    this
                );
                break;
            }

            currentEdgeId = nextEdgeId;
            distanceOnEdgeM = remainDistanceM;
            SetCurrentActiveEdge(newEdge);
        }

        if (guard >= maxTransitionsPerFrame)
        {
            Debug.LogWarning($"{nameof(TrainController)} on {name}: edge transition loop reached guard limit.", this);
        }
    }

    /// <summary>
    /// 役割: TryGetPositionBehind の処理を取得を試みます。
    /// </summary>
    /// <param name="offsetM">offsetM を指定します。</param>
    /// <param name="edgeId">出力結果を受け取る edgeId です。</param>
    /// <param name="distOnEdge">出力結果を受け取る distOnEdge です。</param>
    /// <returns>処理が成功した場合は true、それ以外は false を返します。</returns>
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

    /// <summary>
    /// 先頭基準点より前に実際の車体がどれだけ張り出しているかを返します。
    /// TrainController の位置は先頭車の中心として扱っているため、
    /// 先頭車長の半分が前方張り出し量になります。
    /// </summary>
    public float GetHeadForwardExtentM()
    {
        ConsistDefinition consist = ResolveConsistDefinition();
        return 0.5f * GetCarLengthM(consist, 0);
    }

    /// <summary>
    /// 先頭基準点から実際の最後尾端までの距離を返します。
    /// carTrackStates には最後尾車の中心位置までのオフセットが入っているため、
    /// そこに最後尾車長の半分を足すと編成の実際の後端位置になります。
    /// </summary>
    public float GetTailEndOffsetFromHeadM()
    {
        SyncCarTrackStatesWithConsist();

        if (carTrackStates == null || carTrackStates.Count == 0)
        {
            return 0f;
        }

        ConsistDefinition consist = ResolveConsistDefinition();
        int tailIndex = carTrackStates.Count - 1;
        CarTrackState tailState = carTrackStates[tailIndex];
        float tailCarLengthM = GetCarLengthM(consist, tailIndex);

        return tailState.offsetFromHeadM + 0.5f * tailCarLengthM;
    }

    /// <summary>
    /// 編成の実際の最後尾端が線路上のどこにあるかを解決します。
    /// これは、最後尾が完全に抜けるまで前の閉塞を保持したい
    /// 閉塞在線管理のような仕組みで使う想定です。
    /// </summary>
    public bool TryGetTailEndTrackPosition(out string edgeId, out float distanceOnEdgeM)
    {
        float tailOffsetM = GetTailEndOffsetFromHeadM();
        return TryGetPositionBehind(tailOffsetM, out edgeId, out distanceOnEdgeM);
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
    /// <param name="massKg">massKg を指定します。</param>
    /// <param name="brakeDecelerationMS2">出力結果を受け取る brakeDecelerationMS2 です。</param>
    /// <param name="brakeForceN">出力結果を受け取る brakeForceN です。</param>
    /// <remarks>返り値はありません。</remarks>
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
            // 既存の惰行フィールを維持するため、暫定で追加抵抗として扱う
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

        float deltaDistanceM = speedMS * Time.deltaTime;
        distance += deltaDistanceM;
        distanceOnEdgeM += deltaDistanceM;
        AdvanceEdgeTransitionIfNeeded();
    }

    /// <summary>
    /// 役割: SyncCarTrackStatesWithConsist の処理を同期します。
    /// </summary>
    /// <remarks>返り値はありません。</remarks>
    private void SyncCarTrackStatesWithConsist()
    {
        ConsistDefinition resolvedConsistDefinition = ResolveConsistDefinition();
        EnsureCarTrackStateCount(GetTargetCarCount(resolvedConsistDefinition));
        RefreshCarOffsets(resolvedConsistDefinition);
    }

    /// <summary>
    /// 役割: ResolveControllerReferences の処理を解決します。
    /// </summary>
    /// <remarks>返り値はありません。</remarks>
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

    /// <summary>
    /// 役割: EnsureRuntimeResolver の処理を必要な状態を保証します。
    /// </summary>
    /// <remarks>返り値はありません。</remarks>
    private void EnsureRuntimeResolver()
    {
        if (resolver == null)
        {
            resolver = new TrackRuntimeResolver();
        }
    }

    /// <summary>
    /// 役割: InitializeTrackState の処理を初期化します。
    /// </summary>
    /// <remarks>返り値はありません。</remarks>
    private void InitializeTrackState()
    {
        if (trackGraph != null && string.IsNullOrEmpty(currentEdgeId) && trackGraph.edges != null && trackGraph.edges.Count > 0)
        {
            currentEdgeId = trackGraph.edges[0].edgeId;
            distanceOnEdgeM = 0f;
        }

        activeEdges.Clear();

        if (trackGraph == null || string.IsNullOrEmpty(currentEdgeId))
        {
            return;
        }

        TrackEdge initialEdge = trackGraph.FindEdge(currentEdgeId);
        if (initialEdge != null)
        {
            activeEdges.Add(initialEdge);
        }
    }

    /// <summary>
    /// 役割: TryResolveHeadPose の処理を行います。
    /// </summary>
    /// <param name="pos">出力結果を受け取る pos です。</param>
    /// <param name="tan">出力結果を受け取る tan です。</param>
    /// <returns>処理が成功した場合は true、それ以外は false を返します。</returns>
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

    /// <summary>
    /// 役割: ApplyHeadPose の処理を適用します。
    /// </summary>
    /// <param name="pos">pos を指定します。</param>
    /// <param name="tan">tan を指定します。</param>
    /// <remarks>返り値はありません。</remarks>
    private void ApplyHeadPose(Vector3 pos, Vector3 tan)
    {
        transform.position = pos;
        if (tan.sqrMagnitude > 0.000001f)
        {
            transform.rotation = Quaternion.LookRotation(tan);
        }
    }

    /// <summary>
    /// 役割: UpdateCarTrackStates の処理を更新します。
    /// </summary>
    /// <remarks>返り値はありません。</remarks>
    private void UpdateCarTrackStates()
    {
        for (int i = 0; i < carTrackStates.Count; i++)
        {
            UpdateCarTrackState(i);
        }
    }

    /// <summary>
    /// 役割: UpdateCarTrackState の処理を更新します。
    /// </summary>
    /// <param name="index">index を指定します。</param>
    /// <remarks>返り値はありません。</remarks>
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

    /// <summary>
    /// 役割: EnsureCarTrackStateCount の処理を必要な状態を保証します。
    /// </summary>
    /// <param name="targetCarCount">targetCarCount を指定します。</param>
    /// <remarks>返り値はありません。</remarks>
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

    /// <summary>
    /// 役割: RefreshCarOffsets の処理を再計算します。
    /// </summary>
    /// <param name="resolvedConsistDefinition">resolvedConsistDefinition を指定します。</param>
    /// <remarks>返り値はありません。</remarks>
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

    /// <summary>
    /// 役割: GetOrCreateCarTrackState の処理を取得します。
    /// </summary>
    /// <param name="index">index を指定します。</param>
    /// <returns>処理結果を返します。</returns>
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

    /// <summary>
    /// 役割: ResolveConsistDefinition の処理を解決します。
    /// </summary>
    /// <returns>処理結果を返します。</returns>
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

    /// <summary>
    /// 役割: GetTargetCarCount の処理を取得します。
    /// </summary>
    /// <param name="resolvedConsistDefinition">resolvedConsistDefinition を指定します。</param>
    /// <returns>計算または参照した値を返します。</returns>
    private int GetTargetCarCount(ConsistDefinition resolvedConsistDefinition)
    {
        if (resolvedConsistDefinition != null && resolvedConsistDefinition.HasCars)
        {
            return resolvedConsistDefinition.CarCount;
        }

        return Mathf.Max(1, carTrackStates.Count);
    }

    /// <summary>
    /// 役割: GetCarLengthM の処理を取得します。
    /// </summary>
    /// <param name="resolvedConsistDefinition">resolvedConsistDefinition を指定します。</param>
    /// <param name="index">index を指定します。</param>
    /// <returns>計算または参照した値を返します。</returns>
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

    /// <summary>
    /// 役割: GetRequiredHistoryLengthM の処理を取得します。
    /// </summary>
    /// <returns>計算または参照した値を返します。</returns>
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

    /// <summary>
    /// 役割: EnsureActiveEdgeHistory の処理を必要な状態を保証します。
    /// </summary>
    /// <param name="requiredOffsetM">requiredOffsetM を指定します。</param>
    /// <remarks>返り値はありません。</remarks>
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
            if (previousEdge == null)
            {
                break;
            }

            float previousEdgeLengthM = Mathf.Max(0f, previousEdge.lengthM);
            if (previousEdgeLengthM <= Mathf.Epsilon)
            {
                break;
            }

            activeEdges.Add(previousEdge);
            coveredDistanceM += previousEdgeLengthM;
        }

        if (guard >= maxBackfillEdges)
        {
            Debug.LogWarning($"{nameof(TrainController)} on {name}: active edge history backfill reached guard limit.", this);
        }
    }

    /// <summary>
    /// 役割: SetCurrentActiveEdge の処理を設定します。
    /// </summary>
    /// <param name="currentEdge">currentEdge を指定します。</param>
    /// <remarks>返り値はありません。</remarks>
    private void SetCurrentActiveEdge(TrackEdge currentEdge)
    {
        if (currentEdge == null)
        {
            return;
        }

        if (activeEdges.Count > 0 && activeEdges[0] != null && activeEdges[0].edgeId == currentEdge.edgeId)
        {
            activeEdges[0] = currentEdge;
            return;
        }

        activeEdges.Insert(0, currentEdge);
    }

    /// <summary>
    /// 役割: TrimActiveEdgeHistory の処理を不要分を削減します。
    /// </summary>
    /// <param name="requiredOffsetM">requiredOffsetM を指定します。</param>
    /// <remarks>返り値はありません。</remarks>
    private void TrimActiveEdgeHistory(float requiredOffsetM)
    {
        if (activeEdges.Count <= 1)
        {
            return;
        }

        float coveredDistanceM = Mathf.Max(0f, distanceOnEdgeM);
        int keepCount = 1;
        while (keepCount < activeEdges.Count && coveredDistanceM < requiredOffsetM)
        {
            TrackEdge trackedEdge = activeEdges[keepCount];
            if (trackedEdge != null)
            {
                coveredDistanceM += Mathf.Max(0f, trackedEdge.lengthM);
            }

            keepCount++;
        }

        if (keepCount < activeEdges.Count)
        {
            activeEdges.RemoveRange(keepCount, activeEdges.Count - keepCount);
        }
    }

    /// <summary>
    /// 役割: GetTrackedHistoryLengthM の処理を取得します。
    /// </summary>
    /// <returns>計算または参照した値を返します。</returns>
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
}
