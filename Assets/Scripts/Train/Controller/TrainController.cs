using System.Collections.Generic;
using UnityEngine;

public partial class TrainController : MonoBehaviour
{
    // ref
    [SerializeField] private TrainSpec trainSpec;
    [SerializeField] private NotchManager notchManager;
    [SerializeField] private BrakeSystemController brakeSystem;
    [SerializeField] private TractionSystemController tractionSystem;
    [SerializeField] private VVVFController[] vvvfControllers;
    [SerializeField] private TrackGraph trackGraph;

    // train info
    [SerializeField] private bool acceptPlayerInput = true;
    [SerializeField] private string trainId = "PlayerTrain";
    [SerializeField] private string currentEdgeId;
    [SerializeField] private string startNodeId;
    [SerializeField] private ReverserPosition reverserPosition = ReverserPosition.Forward;

    // physics
    [SerializeField] private float speedMS = 0f;
    [SerializeField, Min(0f)] private float distanceOnEdgeM = 0f;
    private float distance = 0f;

    private float currentAccelerationMS2 = 0f;
    private float currentJerkMS3 = 0f;
    private float currentGradientPermille = 0f;
    private float currentCantMm = 0f;
    private float currentGradeResistanceForceN = 0f;
    private float currentTractionForceN = 0f;

    // graph
    private TrackRuntimeResolver resolver;

    private readonly List<TrackEdge> activeEdges = new List<TrackEdge>();
    private readonly List<TrackTraceSegment> positionBehindSegments = new List<TrackTraceSegment>();

    [Header("Consist Configuration")]
    [SerializeField] private ConsistDefinition consistDefinition;
    [SerializeField, Min(1f)] private float defaultCarLengthM = 20f;

    [SerializeField]
    private List<CarTrackState> carTrackStates = new List<CarTrackState>();

    // direction
    public enum CabEnd
    {
        Front, Rear
    }

    public enum ReverserPosition
    {
        Reverse = -1,
        Neutral = 0,
        Forward = 1
    }

    

    public float SpeedKmH => Mathf.Abs(speedMS) * 3.6f;
    public float SpeedMS => Mathf.Abs(speedMS);
    public float SignedSpeedKmH => speedMS * 3.6f;
    public float SignedSpeedMS => speedMS;
    // CurrentDirection は車両前頭が向いている線路上の基準方向です。
    // 実際に動く向きは符号付き速度を反映した CurrentMovementDirection を使います。
    public EdgeTravelDirection CurrentDirection { get; private set; }
    public EdgeTravelDirection CurrentMovementDirection => GetMovementDirection();
    public ReverserPosition Reverser => reverserPosition;
    public float DistanceM => distance;
    public string TrainId => string.IsNullOrWhiteSpace(trainId) ? name : trainId;
    public TrackGraph Graph => trackGraph;
    public string CurrentEdgeId => currentEdgeId;
    public float DistanceOnEdgeM => distanceOnEdgeM;
    public TrainSpec Spec => trainSpec;
    public bool AcceptPlayerInput => acceptPlayerInput;
    public int PowerNotch => notchManager != null ? notchManager.ResolvedPowerNotch : 0;
    public int BrakeNotch => notchManager != null ? notchManager.ResolvedBrakeNotch : 0;
    public int ManualPowerNotch => notchManager != null ? notchManager.ManualPowerNotch : 0;
    public int ManualBrakeNotch => notchManager != null ? notchManager.ManualBrakeNotch : 0;
    public int ATCBrakeNotch => notchManager != null ? notchManager.ATCBrakeNotch : 0;
    public int TASCBrakeStep => notchManager != null ? notchManager.TASCBrakeStep : 0;
    public int EmergencyBrakeNotch => trainSpec != null ? trainSpec.GetEmergencyBrakeNotch() : 9;
    public bool IsEmergencyBrakeActive => BrakeNotch >= EmergencyBrakeNotch;
    public float CurrentBrakeDecelMS2 => brakeSystem != null ? brakeSystem.TotalBrakeDecelMS2 : 0f;
    public float CurrentRegenBrakeDecelMS2 => brakeSystem != null ? brakeSystem.CurrentRegenDecelMS2 : 0f;
    public float CurrentAirBrakeDecelMS2 => brakeSystem != null ? brakeSystem.CurrentAirDecelMS2 : 0f;
    public float CurrentBrakeForceN => brakeSystem != null ? brakeSystem.TotalBrakeForceN : 0f;
    public float CurrentRegenBrakeForceN => brakeSystem != null ? brakeSystem.CurrentRegenForceN : 0f;
    public float CurrentAirBrakeForceN => brakeSystem != null ? brakeSystem.CurrentAirForceN : 0f;
    public float CurrentTractionForceN => currentTractionForceN;
    public float CurrentBCPressureKPa => brakeSystem != null ? brakeSystem.CurrentBCPressureKPa : 0f;
    public float CurrentTargetBCPressureKPa => brakeSystem != null ? brakeSystem.CurrentTargetBCPressureKPa : 0f;
    public bool IsRollingPreventionActive => brakeSystem != null && brakeSystem.IsRollingPreventionActive;
    public bool IsGradientStart => brakeSystem != null && brakeSystem.IsGradientStart;
    public float CurrentAccelerationMS2 => currentAccelerationMS2;
    public float CurrentJerkMS3 => currentJerkMS3;
    public float CurrentConsistMassKg => GetCurrentConsistMassKg();
    public float CurrentGradientPermille => currentGradientPermille;
    public float CurrentCantMm => currentCantMm;
    public float CurrentGradeResistanceForceN => currentGradeResistanceForceN;
    public IReadOnlyList<CarBrakeState> CurrentCarBrakeStates => brakeSystem != null ? brakeSystem.CarBrakeStates : null;
    public IReadOnlyList<CarTractionState> CurrentCarTractionStates => tractionSystem != null ? tractionSystem.CarTractionStates : null;
    public VVVFController[] VVVFControllers => vvvfControllers;
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
        notchManager.ConfigureLimits(trainSpec.maxPowerNotch, EmergencyBrakeNotch, trainSpec.GetTascBrakeSubstepsPerNotch());
    }

    void Update()
    {
        // 入力、物理、線路上姿勢更新の順に固定する。
        // ここを崩すと、ノッチ入力や逆転器変更が1フレーム遅れて姿勢計算へ反映される。
        HandleInput();
        ApplyPhysics();
        MoveTrain();
    }

    /// <summary>
    /// 同じ GameObject / 親階層から運転制御に必要な参照を補完します。
    /// </summary>
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

        if (vvvfControllers == null || vvvfControllers.Length == 0)
        {
            RefreshVVVFControllersFromChildren();
        }
    }

    public void SetVVVFControllers(VVVFController[] controllers)
    {
        vvvfControllers = controllers ?? System.Array.Empty<VVVFController>();
    }

    public void RefreshVVVFControllersFromChildren()
    {
        vvvfControllers = GetComponentsInChildren<VVVFController>(true);
    }
}
