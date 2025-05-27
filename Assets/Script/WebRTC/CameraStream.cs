using System;
using UnityEngine;
using UnityEngine.UI;

public class WebRTCCameraStreamer : MonoBehaviour
{
    [SerializeField] private RawImage displayImage;
    [SerializeField] private AspectRatioFitter aspectRatioFitter;
    [SerializeField] private Camera gameCamera;

    private RenderTexture cameraRenderTexture;

    private void Start()
    {
        InitializeCamera();
    }

    private void InitializeCamera()
    {
        if (!gameCamera)
        {
            Debug.LogError("No camera assigned");
            return;
        }

        Debug.Log("Camera detected");
        Debug.Log(gameCamera.name);

        // Create a render texture for the camera
        cameraRenderTexture = new RenderTexture(1280, 720, 24);
        gameCamera.targetTexture = cameraRenderTexture;

        // Apply texture to the RawImage
        if (displayImage != null)
        {
            displayImage.texture = cameraRenderTexture;
            displayImage.material.mainTexture = cameraRenderTexture;
        }

        // Set the aspect ratio
        if (aspectRatioFitter != null)
        {
            aspectRatioFitter.aspectRatio = (float)cameraRenderTexture.width / cameraRenderTexture.height;
        }
    }

    private void OnDestroy()
    {
        if (cameraRenderTexture != null)
        {
            if (gameCamera != null)
                gameCamera.targetTexture = null;

            cameraRenderTexture.Release();
            Destroy(cameraRenderTexture);
        }
    }
}