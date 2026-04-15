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

    [Header("Cab")]
    [SerializeField] private float rearCabYawOffsetDegrees = 180f;
    [SerializeField] private GameObject cabModulePrefab;
    [SerializeField] private string cabModuleMountName = "CabMount";
    [SerializeField] private Vector3 frontCabModuleLocalEuler = Vector3.zero;
    [SerializeField] private Vector3 rearCabModuleLocalEuler = new Vector3(0f, 180f, 0f);

    private const string CarsRootName = "Cars";

    private void Reset()
    {
        trainController = GetComponent<TrainController>();
        carsRoot = transform.Find(CarsRootName);
    }

    [ContextMenu("Rebuild Train Cars")]
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
    }

    private ConsistDefinition ResolveConsistDefinition()
    {
        if (consistDefinition != null && consistDefinition.HasCars)
        {
            return consistDefinition;
        }

        return trainController != null ? trainController.ConsistDefinition : consistDefinition;
    }

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

    private void ClearCarsRoot()
    {
        for (int i = carsRoot.childCount - 1; i >= 0; i--)
        {
            Transform child = carsRoot.GetChild(i);
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
        AddCabModuleIfNeeded(instance.transform, carSpec);
    }

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

    private string CreateCarObjectName(int carIndex, CarSpec carSpec)
    {
        string role = carSpec.carRole == CarRole.Cab ? carSpec.cabEnd.ToString() : carSpec.carRole.ToString();
        return $"Car_{carIndex + 1:00}_{carSpec.carType}_{role}";
    }

    private void ResetGeneratedTransform(Transform carTransform)
    {
        carTransform.SetParent(carsRoot, false);
        carTransform.localPosition = Vector3.zero;
        carTransform.localRotation = Quaternion.identity;
        carTransform.localScale = Vector3.one;
    }

    private void ConfigureTrainCar(GameObject instance, int carIndex, CarSpec carSpec)
    {
        TrainCar trainCar = instance.GetComponent<TrainCar>();
        if (trainCar == null)
        {
            trainCar = instance.AddComponent<TrainCar>();
        }

        trainCar.Configure(trainController, carIndex, carSpec, GetCarYawOffsetDegrees(carSpec));
    }

    private float GetCarYawOffsetDegrees(CarSpec carSpec)
    {
        if (carSpec != null && carSpec.carRole == CarRole.Cab && carSpec.cabEnd == CabEnd.Rear)
        {
            return rearCabYawOffsetDegrees;
        }

        return 0f;
    }

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

    private Vector3 GetCabModuleEuler(CabEnd cabEnd)
    {
        return cabEnd == CabEnd.Rear ? rearCabModuleLocalEuler : frontCabModuleLocalEuler;
    }
}
