using System;
using System.IO.Compression;
using Unity.VisualScripting;
using UnityEngine;

public class CameraMount : MonoBehaviour
{
    [SerializeField] private TrainController train;

    [SerializeField] private Transform cameraMount;
    [SerializeField] private Vector3 cameraPosition;
    [SerializeField] private float diffZ = 0;



    void Update ()
    {
        float targetZ = train.SpeedKmH < 0.01f ? 0f : train.CurrentAccelerationMS2 / 35f;
        diffZ = Mathf.MoveTowards(diffZ, targetZ, Time.deltaTime * 0.25f);
        cameraMount.localPosition = new Vector3(cameraPosition.x, cameraPosition.y, cameraPosition.z - diffZ);
    }

}