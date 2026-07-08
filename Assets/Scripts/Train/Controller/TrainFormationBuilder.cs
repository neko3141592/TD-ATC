using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class TrainFormationBuilder : MonoBehaviour
{
    [Header("Sources")]
    [SerializeField] private TrainController trainController;
    [SerializeField] private ConsistDefinition consistDefinition;

    [Header("Output")]
    [SerializeField] private Transform carsRoot;
    [SerializeField] private bool clearCarsRootBeforeRebuild = true;

    [Header("Traction Equipments")]
    [SerializeField] private Transform tractionEquipmentsRoot;
    [SerializeField] private bool buildTractionEquipmentsOnAwake = true;
    [SerializeField] private bool clearTractionEquipmentsRootBeforeRebuild = true;

    [Header("Cab")]
    [SerializeField] private float rearCabYawOffsetDegrees = 180f;
    [SerializeField] private GameObject cabModulePrefab;
    [SerializeField] private string cabModuleMountName = "CabMount";
    [SerializeField] private Vector3 frontCabModuleLocalEuler = Vector3.zero;
    [SerializeField] private Vector3 rearCabModuleLocalEuler = new Vector3(0f, 180f, 0f);

    [Header("Visual Root")]
    [SerializeField] private string visualRootName = "VisualRoot";
    [SerializeField] private Vector3 rearCabVisualRootLocalPositionOffset = Vector3.zero;

    private const string CarsRootName = "Cars";
    private const string TractionEquipmentsRootName = "TractionEquipments";

    private void Awake()
    {
        if (buildTractionEquipmentsOnAwake)
        {
            RebuildTractionEquipments();
        }
    }

    /// <summary>
    /// 役割: Reset の処理を実行します。
    /// </summary>
    /// <remarks>返り値はありません。</remarks>
    private void Reset()
    {
        trainController = GetComponent<TrainController>();
        carsRoot = transform.Find(CarsRootName);
        tractionEquipmentsRoot = transform.Find(TractionEquipmentsRootName);
    }

    [ContextMenu("Rebuild Train Cars")]
    /// <summary>
    /// 役割: Rebuild の処理を実行します。
    /// </summary>
    /// <remarks>返り値はありません。</remarks>
    public void Rebuild()
    {
        ConsistDefinition resolvedConsist = ResolveConsistDefinition();
        if (trainController == null)
        {
            Debug.LogError($"{nameof(TrainFormationBuilder)} on {name}: TrainController is not assigned.", this);
            return;
        }

        if (resolvedConsist == null || !resolvedConsist.HasCars)
        {
            Debug.LogError($"{nameof(TrainFormationBuilder)} on {name}: ConsistDefinition has no cars.", this);
            return;
        }

        EnsureCarsRoot();
        if (carsRoot == null)
        {
            Debug.LogError($"{nameof(TrainFormationBuilder)} on {name}: carsRoot is not assigned.", this);
            return;
        }

        if (clearCarsRootBeforeRebuild)
        {
            ClearCarsRoot();
        }

        for (int i = 0; i < resolvedConsist.CarCount; i++)
        {
            BuildCar(resolvedConsist, i);
        }

        RebuildTractionEquipments(resolvedConsist);
    }

    [ContextMenu("Rebuild Traction Equipments")]
    public void RebuildTractionEquipments()
    {
        RebuildTractionEquipments(ResolveConsistDefinition());
    }

    private void RebuildTractionEquipments(ConsistDefinition resolvedConsist)
    {
        if (trainController == null)
        {
            trainController = GetComponent<TrainController>();
        }

        if (trainController == null)
        {
            Debug.LogError($"{nameof(TrainFormationBuilder)} on {name}: TrainController is not assigned.", this);
            return;
        }

        EnsureTractionEquipmentsRoot();
        if (tractionEquipmentsRoot == null)
        {
            Debug.LogError($"{nameof(TrainFormationBuilder)} on {name}: tractionEquipmentsRoot is not assigned.", this);
            return;
        }

        if (clearTractionEquipmentsRootBeforeRebuild)
        {
            ClearRoot(tractionEquipmentsRoot);
        }

        List<VVVFController> generatedVvvfs = new List<VVVFController>();
        if (resolvedConsist != null && resolvedConsist.HasCars)
        {
            for (int i = 0; i < resolvedConsist.CarCount; i++)
            {
                BuildTractionEquipmentsForCar(resolvedConsist, i, generatedVvvfs);
            }
        }

        RegisterGeneratedVVVFControllers(generatedVvvfs);
    }

    /// <summary>
    /// 役割: ResolveConsistDefinition の処理を実行します。
    /// </summary>
    /// <returns>処理結果を返します。</returns>
    private ConsistDefinition ResolveConsistDefinition()
    {
        if (consistDefinition != null && consistDefinition.HasCars)
        {
            return consistDefinition;
        }

        return trainController != null ? trainController.ConsistDefinition : consistDefinition;
    }

    /// <summary>
    /// 役割: EnsureCarsRoot の処理を実行します。
    /// </summary>
    /// <remarks>返り値はありません。</remarks>
    private void EnsureCarsRoot()
    {
        if (carsRoot != null)
        {
            return;
        }

        Transform existingRoot = transform.Find(CarsRootName);
        if (existingRoot != null)
        {
            carsRoot = existingRoot;
            return;
        }

        GameObject root = new GameObject(CarsRootName);
        root.transform.SetParent(transform, false);
        carsRoot = root.transform;
    }

    private void EnsureTractionEquipmentsRoot()
    {
        if (tractionEquipmentsRoot != null)
        {
            return;
        }

        Transform existingRoot = transform.Find(TractionEquipmentsRootName);
        if (existingRoot != null)
        {
            tractionEquipmentsRoot = existingRoot;
            return;
        }

        GameObject root = new GameObject(TractionEquipmentsRootName);
        root.transform.SetParent(transform, false);
        tractionEquipmentsRoot = root.transform;
    }

    /// <summary>
    /// 役割: ClearCarsRoot の処理を実行します。
    /// </summary>
    /// <remarks>返り値はありません。</remarks>
    private void ClearCarsRoot()
    {
        ClearRoot(carsRoot);
    }

    private void ClearRoot(Transform root)
    {
        if (root == null)
        {
            return;
        }

        for (int i = root.childCount - 1; i >= 0; i--)
        {
            Transform child = root.GetChild(i);
            if (Application.isPlaying)
            {
                Destroy(child.gameObject);
            }
            else
            {
                DestroyImmediate(child.gameObject);
            }
        }
    }

    private void BuildTractionEquipmentsForCar(ConsistDefinition resolvedConsist, int carIndex, List<VVVFController> generatedVvvfs)
    {
        if (generatedVvvfs == null ||
            !resolvedConsist.TryGetCar(carIndex, out CarSpec carSpec) ||
            carSpec == null ||
            carSpec.carType != CarType.Motor ||
            carSpec.motorCount <= 0 ||
            carSpec.vvvfPrefab == null)
        {
            return;
        }

        int unitCount = Mathf.Max(1, carSpec.vvvfUnitCount);
        for (int unitIndex = 0; unitIndex < unitCount; unitIndex++)
        {
            VVVFController vvvf = InstantiateVVVFPrefab(carSpec.vvvfPrefab);
            if (vvvf == null)
            {
                Debug.LogWarning($"{nameof(TrainFormationBuilder)} on {name}: failed to instantiate VVVF prefab. carIndex={carIndex}, spec={carSpec.name}", carSpec);
                continue;
            }

            vvvf.name = CreateVVVFObjectName(carIndex, unitIndex, unitCount);
            ResetGeneratedEquipmentTransform(vvvf.transform);
            vvvf.Bind(trainController, carIndex);
            generatedVvvfs.Add(vvvf);
        }
    }

    private VVVFController InstantiateVVVFPrefab(VVVFController prefab)
    {
        if (prefab == null)
        {
            return null;
        }

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            GameObject instance = PrefabUtility.InstantiatePrefab(prefab.gameObject, tractionEquipmentsRoot) as GameObject;
            return instance != null ? instance.GetComponent<VVVFController>() : null;
        }
#endif

        return Instantiate(prefab, tractionEquipmentsRoot);
    }

    private string CreateVVVFObjectName(int carIndex, int unitIndex, int unitCount)
    {
        string suffix = unitCount > 1 ? $"_{unitIndex + 1:00}" : string.Empty;
        return $"VVVF_Car{carIndex + 1:00}{suffix}";
    }

    private void ResetGeneratedEquipmentTransform(Transform equipmentTransform)
    {
        equipmentTransform.SetParent(tractionEquipmentsRoot, false);
        equipmentTransform.localPosition = Vector3.zero;
        equipmentTransform.localRotation = Quaternion.identity;
        equipmentTransform.localScale = Vector3.one;
    }

    private void RegisterGeneratedVVVFControllers(List<VVVFController> generatedVvvfs)
    {
        VVVFController[] controllers = generatedVvvfs != null
            ? generatedVvvfs.ToArray()
            : System.Array.Empty<VVVFController>();

        trainController.SetVVVFControllers(controllers);

        TimsTractionForceController ntim = trainController.GetComponentInChildren<TimsTractionForceController>(true);
        if (ntim != null)
        {
            ntim.SetVVVFControllers(controllers);
        }
    }

    /// <summary>
    /// 役割: BuildCar の処理を実行します。
    /// </summary>
    /// <param name="resolvedConsist">resolvedConsist を指定します。</param>
    /// <param name="carIndex">carIndex を指定します。</param>
    /// <remarks>返り値はありません。</remarks>
    private void BuildCar(ConsistDefinition resolvedConsist, int carIndex)
    {
        if (!resolvedConsist.TryGetCar(carIndex, out CarSpec carSpec) || carSpec == null)
        {
            Debug.LogWarning($"{nameof(TrainFormationBuilder)} on {name}: car spec at index {carIndex} is missing.", this);
            return;
        }

        if (carSpec.carPrefab == null)
        {
            Debug.LogWarning($"{nameof(TrainFormationBuilder)} on {name}: car prefab is not assigned. index={carIndex}, spec={carSpec.name}", carSpec);
            return;
        }

        GameObject instance = InstantiateCarPrefab(carSpec);
        if (instance == null)
        {
            Debug.LogWarning($"{nameof(TrainFormationBuilder)} on {name}: failed to instantiate car prefab. index={carIndex}, spec={carSpec.name}", carSpec);
            return;
        }

        instance.name = CreateCarObjectName(carIndex, carSpec);
        ResetGeneratedTransform(instance.transform);
        ConfigureTrainCar(instance, carIndex, carSpec);
        ApplyRearCabVisualRootOffset(instance.transform, carSpec);
        AddCabModuleIfNeeded(instance.transform, carSpec);
    }

    /// <summary>
    /// 役割: InstantiateCarPrefab の処理を実行します。
    /// </summary>
    /// <param name="carSpec">carSpec を指定します。</param>
    /// <returns>処理結果を返します。</returns>
    private GameObject InstantiateCarPrefab(CarSpec carSpec)
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            return PrefabUtility.InstantiatePrefab(carSpec.carPrefab, carsRoot) as GameObject;
        }
#endif

        return Instantiate(carSpec.carPrefab, carsRoot);
    }

    /// <summary>
    /// 役割: CreateCarObjectName の処理を実行します。
    /// </summary>
    /// <param name="carIndex">carIndex を指定します。</param>
    /// <param name="carSpec">carSpec を指定します。</param>
    /// <returns>処理結果を返します。</returns>
    private string CreateCarObjectName(int carIndex, CarSpec carSpec)
    {
        string role = carSpec.carRole == CarRole.Cab ? carSpec.cabEnd.ToString() : carSpec.carRole.ToString();
        return $"Car_{carIndex + 1:00}_{carSpec.carType}_{role}";
    }

    /// <summary>
    /// 役割: ResetGeneratedTransform の処理を実行します。
    /// </summary>
    /// <param name="carTransform">carTransform を指定します。</param>
    /// <remarks>返り値はありません。</remarks>
    private void ResetGeneratedTransform(Transform carTransform)
    {
        carTransform.SetParent(carsRoot, false);
        carTransform.localPosition = Vector3.zero;
        carTransform.localRotation = Quaternion.identity;
        carTransform.localScale = Vector3.one;
    }

    /// <summary>
    /// 役割: ConfigureTrainCar の処理を実行します。
    /// </summary>
    /// <param name="instance">instance を指定します。</param>
    /// <param name="carIndex">carIndex を指定します。</param>
    /// <param name="carSpec">carSpec を指定します。</param>
    /// <remarks>返り値はありません。</remarks>
    private void ConfigureTrainCar(GameObject instance, int carIndex, CarSpec carSpec)
    {
        TrainCar trainCar = instance.GetComponent<TrainCar>();
        if (trainCar == null)
        {
            trainCar = instance.AddComponent<TrainCar>();
        }

        trainCar.Configure(trainController, carIndex, carSpec, GetCarYawOffsetDegrees(carSpec));
    }

    /// <summary>
    /// 役割: GetCarYawOffsetDegrees の処理を実行します。
    /// </summary>
    /// <param name="carSpec">carSpec を指定します。</param>
    /// <returns>処理結果を返します。</returns>
    private float GetCarYawOffsetDegrees(CarSpec carSpec)
    {
        if (carSpec != null && carSpec.carRole == CarRole.Cab && carSpec.cabEnd == CabEnd.Rear)
        {
            return rearCabYawOffsetDegrees;
        }

        return 0f;
    }

    private void ApplyRearCabVisualRootOffset(Transform carRoot, CarSpec carSpec)
    {
        if (carRoot == null ||
            carSpec == null ||
            carSpec.carRole != CarRole.Cab ||
            carSpec.cabEnd != CabEnd.Rear ||
            rearCabVisualRootLocalPositionOffset == Vector3.zero)
        {
            return;
        }

        Transform visualRoot = FindVisualRoot(carRoot);
        if (visualRoot == null)
        {
            Debug.LogWarning($"{nameof(TrainFormationBuilder)} on {name}: rear cab has no visual root '{visualRootName}'. spec={carSpec.name}", carSpec);
            return;
        }

        visualRoot.localPosition += rearCabVisualRootLocalPositionOffset;
    }

    private Transform FindVisualRoot(Transform carRoot)
    {
        if (carRoot == null || string.IsNullOrEmpty(visualRootName))
        {
            return null;
        }

        return carRoot.Find(visualRootName);
    }

    /// <summary>
    /// 役割: AddCabModuleIfNeeded の処理を実行します。
    /// </summary>
    /// <param name="carRoot">carRoot を指定します。</param>
    /// <param name="carSpec">carSpec を指定します。</param>
    /// <remarks>返り値はありません。</remarks>
    private void AddCabModuleIfNeeded(Transform carRoot, CarSpec carSpec)
    {
        if (carSpec == null || carSpec.carRole != CarRole.Cab || cabModulePrefab == null)
        {
            return;
        }

        Transform mount = FindCabModuleMount(carRoot);
        GameObject cabModule = Instantiate(cabModulePrefab, mount);
        cabModule.name = cabModulePrefab.name;
        cabModule.transform.localPosition = Vector3.zero;
        cabModule.transform.localRotation = Quaternion.Euler(GetCabModuleEuler(carSpec.cabEnd));
        cabModule.transform.localScale = Vector3.one;
    }

    /// <summary>
    /// 役割: FindCabModuleMount の処理を実行します。
    /// </summary>
    /// <param name="carRoot">carRoot を指定します。</param>
    /// <returns>処理結果を返します。</returns>
    private Transform FindCabModuleMount(Transform carRoot)
    {
        if (!string.IsNullOrEmpty(cabModuleMountName))
        {
            Transform mount = carRoot.Find(cabModuleMountName);
            if (mount != null)
            {
                return mount;
            }
        }

        return carRoot;
    }

    /// <summary>
    /// 役割: GetCabModuleEuler の処理を実行します。
    /// </summary>
    /// <param name="cabEnd">cabEnd を指定します。</param>
    /// <returns>処理結果を返します。</returns>
    private Vector3 GetCabModuleEuler(CabEnd cabEnd)
    {
        return cabEnd == CabEnd.Rear ? rearCabModuleLocalEuler : frontCabModuleLocalEuler;
    }
}
