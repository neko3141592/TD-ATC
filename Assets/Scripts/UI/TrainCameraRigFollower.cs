using UnityEngine;

public class TrainCameraRigFollower : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 localPositionOffset = Vector3.zero;
    [SerializeField] private Vector3 localEulerOffset = Vector3.zero;

    public Transform Target => target;

    private void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        transform.position = target.TransformPoint(localPositionOffset);
        transform.rotation = target.rotation * Quaternion.Euler(localEulerOffset);
    }

    public void SetTarget(Transform nextTarget)
    {
        target = nextTarget;
    }
}
