using UnityEngine;
using UnityEngine.UI;

public class CrosshairController : MonoBehaviour
{
    public TPSCameraController cameraController;
    private Image crosshair;

    void Start()
    {
        crosshair = GetComponent<Image>();
        if (crosshair == null)
            crosshair = gameObject.AddComponent<Image>();
        crosshair.raycastTarget = false;
        crosshair.enabled = false;
    }

    void Update()
    {
        if (cameraController != null)
            crosshair.enabled = cameraController.IsAiming;
    }
}
