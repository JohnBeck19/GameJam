using UnityEngine;
using Unity.Netcode;

public class OwnerCameraFollow2D : NetworkBehaviour
{
    [Header("Camera Settings")]
    [SerializeField] private float smoothTime = 0.12f;
    [SerializeField] private Vector3 cameraOffset = new Vector3(0f, 0f, -10f);
    [SerializeField] private float orthographicSize = 6f;

    private Camera mainCamera;
    private Vector3 velocity;
    private float initialCameraZ;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        if (!IsOwner)
        {
            enabled = false;
            return;
        }

        mainCamera = Camera.main;
        if (mainCamera == null)
        {
            Debug.LogWarning("OwnerCameraFollow2D: No MainCamera found.");
            return;
        }

        if (!mainCamera.orthographic)
        {
            mainCamera.orthographic = true;
        }
        mainCamera.orthographicSize = orthographicSize;
        initialCameraZ = mainCamera.transform.position.z;

        // Snap on spawn
        Vector3 target = transform.position + cameraOffset;
        target.z = initialCameraZ + cameraOffset.z; // keep a stable z
        mainCamera.transform.position = target;
    }

    void LateUpdate()
    {
        if (!IsOwner || mainCamera == null)
            return;

        Vector3 target = transform.position + cameraOffset;
        target.z = initialCameraZ + cameraOffset.z;
        mainCamera.transform.position = Vector3.SmoothDamp(
            mainCamera.transform.position,
            target,
            ref velocity,
            smoothTime
        );
    }
}


