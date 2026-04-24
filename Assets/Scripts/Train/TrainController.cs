using System.Collections.Generic;
using UnityEngine;

public partial class TrainController : MonoBehaviour
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
        // 入力→物理→配置の順序を固定し、1フレーム内の依存関係を明確化する。
        HandleInput();
        ApplyPhysics();
        MoveTrain();
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
}
