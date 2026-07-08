using System.Collections.Generic;
using UnityEngine;

public partial class TrainController : MonoBehaviour
{
    // ref
    [SerializeField] private TrainSpec trainSpec;
    [SerializeField] private TimsSystem tims;
    [SerializeField] private BrakeSystemController brakeSystem;
    [SerializeField] private TractionSystemController tractionSystem;
    [SerializeField] private VVVFController[] vvvfControllers;
    [SerializeField] private BrakeCylinder[] brakeCylinders;
    [SerializeField] private LoadWeightController[] loadWeightControllers;
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
    private float currentBrakeForceN = 0f;
    private float currentAirBrakeForceN = 0f;
    private float currentBrakeDecelMS2 = 0f;
    private float currentAirBrakeDecelMS2 = 0f;
    private float currentBCPressureKPa = 0f;
    private float currentTargetBCPressureKPa = 0f;

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
    public ReverserPosition Reverser => GetTimsReverserPosition();
    public float DistanceM => distance;
    public string TrainId => string.IsNullOrWhiteSpace(trainId) ? name : trainId;
    public TrackGraph Graph => trackGraph;
    public string CurrentEdgeId => currentEdgeId;
    public float DistanceOnEdgeM => distanceOnEdgeM;
    public TrainSpec Spec => trainSpec;
    public bool AcceptPlayerInput => acceptPlayerInput;
    public int PowerNotch => GetTimsMasterInt(new TimsTagKey("Notch", "PowerNotch"));
    public int BrakeStep => GetTimsMasterInt(new TimsTagKey("Notch", "BrakeStep"));
    public int BrakeNotch => ConvertBrakeStepToNotch(BrakeStep);
    public int ManualPowerNotch => GetTimsMasterInt(new TimsTagKey("Notch", "ManualPowerNotch"));
    public int ManualBrakeStep => GetTimsMasterInt(new TimsTagKey("Notch", "ManualBrakeStep"));
    public int ManualBrakeNotch => ConvertBrakeStepToNotch(ManualBrakeStep);
    public int ATCBrakeStep => GetTimsMasterInt(new TimsTagKey("Notch", "ATCBrakeStep"));
    public int ATCBrakeNotch => ConvertBrakeStepToNotch(ATCBrakeStep);
    public int TASCBrakeStep => 0;
    public int EmergencyBrakeNotch => trainSpec != null ? trainSpec.GetEmergencyBrakeNotch() : 9;
    public bool IsEmergencyBrakeActive =>
        GetTimsMasterBool(new TimsTagKey("Brake", "EmergencyBrake")) ||
        BrakeNotch >= EmergencyBrakeNotch;
    public float CurrentBrakeDecelMS2 => currentBrakeDecelMS2;
    public float CurrentRegenBrakeDecelMS2 => brakeSystem != null ? brakeSystem.CurrentRegenDecelMS2 : 0f;
    public float CurrentAirBrakeDecelMS2 => currentAirBrakeDecelMS2;
    public float CurrentBrakeForceN => currentBrakeForceN;
    public float CurrentRegenBrakeForceN => brakeSystem != null ? brakeSystem.CurrentRegenForceN : 0f;
    public float CurrentAirBrakeForceN => currentAirBrakeForceN;
    public float CurrentTractionForceN => currentTractionForceN;
    public float CurrentBCPressureKPa => currentBCPressureKPa;
    public float CurrentTargetBCPressureKPa => currentTargetBCPressureKPa;
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
    public BrakeCylinder[] BrakeCylinders => brakeCylinders;
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
    }

    void Update()
    {
        // TIMS更新後の司令値を使って物理と線路上姿勢を更新する。
        UpdateReverserFromTims();
        ApplyPhysics();
        MoveTrain();
    }

    /// <summary>
    /// 同じ GameObject / 親階層から運転制御に必要な参照を補完します。
    /// </summary>
    private void ResolveControllerReferences()
    {
        if (tims == null)
        {
            tims = GetComponent<TimsSystem>();
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

        if (brakeCylinders == null || brakeCylinders.Length == 0)
        {
            RefreshBrakeCylindersFromChildren();
        }

        if (loadWeightControllers == null || loadWeightControllers.Length == 0)
        {
            RefreshLoadWeightControllersFromChildren();
        }
    }

    public void SetVVVFControllers(VVVFController[] controllers)
    {
        vvvfControllers = controllers ?? System.Array.Empty<VVVFController>();
    }

    public void SetBrakeCylinders(BrakeCylinder[] cylinders)
    {
        brakeCylinders = cylinders ?? System.Array.Empty<BrakeCylinder>();
    }

    public void RefreshVVVFControllersFromChildren()
    {
        vvvfControllers = GetComponentsInChildren<VVVFController>(true);
    }

    public void RefreshBrakeCylindersFromChildren()
    {
        brakeCylinders = GetComponentsInChildren<BrakeCylinder>(true);
    }

    public void RefreshLoadWeightControllersFromChildren()
    {
        loadWeightControllers = GetComponentsInChildren<LoadWeightController>(true);
    }

    private int GetTimsMasterInt(TimsTagKey key)
    {
        if (tims == null)
        {
            tims = GetComponent<TimsSystem>();
        }

        return tims != null && tims.MasterBus.TryGetInt(key, out int value)
            ? value
            : 0;
    }

    private bool GetTimsMasterBool(TimsTagKey key)
    {
        if (tims == null)
        {
            tims = GetComponent<TimsSystem>();
        }

        return tims != null && tims.MasterBus.TryGetBool(key, out bool value) && value;
    }

    private ReverserPosition GetTimsReverserPosition()
    {
        if (tims == null)
        {
            tims = GetComponent<TimsSystem>();
        }

        if (tims != null &&
            tims.MasterBus.TryGetInt(new TimsTagKey("Notch", "ReverserPosition"), out int rawReverserPosition))
        {
            return (ReverserPosition)rawReverserPosition;
        }

        return reverserPosition;
    }

    private void UpdateReverserFromTims()
    {
        reverserPosition = GetTimsReverserPosition();
    }

    private int ConvertBrakeStepToNotch(int brakeStep)
    {
        if (brakeStep <= 0)
        {
            return 0;
        }

        int substeps = trainSpec != null
            ? trainSpec.GetTascBrakeSubstepsPerNotch()
            : 1;

        TimsNotchHelper.ToSubStepBrakeNotch(
            brakeStep,
            Mathf.Max(1, substeps),
            out int brakeNotch,
            out _
        );

        return Mathf.Max(0, brakeNotch);
    }
}
