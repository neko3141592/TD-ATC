using UnityEngine;

/// <summary>
/// 編成内の各車両にアタッチし、TrainControllerが計算した配置結果を
/// もらって自身の3Dモデルを線路上に配置するコンポーネントです。
/// </summary>
public class TrainCar : MonoBehaviour
{
    [Tooltip("編成の先頭にあるTrainController")]
    public TrainController mainController;
    
    [Tooltip("自分が編成の中で何両目か (0が先頭)")]
    public int myCarIndex = 0;

    [SerializeField] private CarSpec carSpec;
    [SerializeField] private float generatedYawOffsetDegrees = 0f;

    [Tooltip("3Dモデルの向きを補正するための角度")]
    public Vector3 modelRotationOffset = new Vector3(-90f, 180f, 0f);

    [Tooltip("3Dモデルの位置を補正するためのオフセット（例: 少し上に浮かすなら Y=2.07 など）")]
    public Vector3 modelPositionOffset = new Vector3(0f, 2.07f, 0f);

    public CarSpec Spec => carSpec;
    public bool HasCab => carSpec != null && carSpec.carRole == CarRole.Cab;
    public CabEnd CabEnd => carSpec != null ? carSpec.cabEnd : CabEnd.None;

    public void Configure(TrainController controller, int carIndex, CarSpec spec, float yawOffsetDegrees)
    {
        mainController = controller;
        myCarIndex = carIndex;
        carSpec = spec;
        generatedYawOffsetDegrees = yawOffsetDegrees;
    }

    void Update()
    {
        if (mainController == null || mainController.CarTrackStates == null) return;
        if (myCarIndex < 0 || myCarIndex >= mainController.CarTrackStates.Count) return;
        CarTrackState myState = mainController.CarTrackStates[myCarIndex];
        
        if (myState.tangent.sqrMagnitude > 0.000001f)
        {
            Quaternion logicalRotation = Quaternion.LookRotation(myState.tangent);
            Quaternion modelRotation = Quaternion.Euler(0f, generatedYawOffsetDegrees, 0f) * Quaternion.Euler(modelRotationOffset);
            
            transform.position = myState.position + (logicalRotation * modelPositionOffset);
            transform.rotation = logicalRotation * modelRotation;
        }
        else
        {
            transform.position = myState.position + modelPositionOffset;
        }
    }
}
