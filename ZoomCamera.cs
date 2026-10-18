using HarmonyLib;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace SilverpineMods.CameraZoom;

// Runs before SaltExtraDebug's whole-tile camera controller (10000).
[DefaultExecutionOrder(9000)]
internal sealed class ZoomCamera : MonoBehaviour
{
    private Camera cameraComponent = null!;
    private float originalSize, originalAspect;
    private Matrix4x4 originalProjection;
    private bool controlling, originalForceIntoRenderTexture;
    private ZoomWeather? weather;
    private readonly List<Behaviour> suspendedPixelPerfect = new();
    private static Func<bool>? overviewActive;
    private static bool checkedOverview;
    internal static bool IsGameplay => Player.Instance != null &&
        (MainMenuUI.Instance == null || !MainMenuUI.Instance.IsOpen());

    internal static bool OverviewActive
    {
        get
        {
            if (!checkedOverview)
            {
                checkedOverview = true;
                Type? type = AccessTools.TypeByName("SaltExtraDebug.WorldTileOverview");
                if (type != null)
                {
                    var getter = AccessTools.PropertyGetter(type, "IsActive");
                    if (getter != null) overviewActive = (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), getter);
                }
            }
            return overviewActive?.Invoke() == true;
        }
    }

    internal static void ApplyMainCamera()
    {
        Camera camera = Camera.main;
        if (camera == null || Player.Instance == null || !camera.orthographic) return;
        ZoomCamera controller = camera.GetComponent<ZoomCamera>();
        if (controller == null) controller = camera.gameObject.AddComponent<ZoomCamera>();
        controller.cameraComponent = camera;
        controller.Apply();
    }

    private void LateUpdate()
    {
        Apply();
        if (cameraComponent == Camera.main && IsGameplay && !OverviewActive &&
            ZoomFog.RefreshBounds(cameraComponent) && FOVManager.Instance != null && FOVSquare.all.Count > 0)
            FOVManager.Instance.Initialize();
    }

    private void OnPreCull() => Apply();

    private void Apply()
    {
        if (cameraComponent == null) return;
        // The overview snapshots/restores the current zoom itself. Leave that snapshot intact.
        if (OverviewActive) return;
        if (cameraComponent != Camera.main || !IsGameplay || !cameraComponent.orthographic || Plugin.Zoom == 1f)
        {
            Restore();
            return;
        }
        if (!controlling)
        {
            originalSize = cameraComponent.orthographicSize;
            originalAspect = cameraComponent.aspect;
            originalProjection = cameraComponent.projectionMatrix;
            originalForceIntoRenderTexture = cameraComponent.forceIntoRenderTexture;
            Rect viewport = cameraComponent.rect;
            foreach (Behaviour behaviour in cameraComponent.GetComponents<Behaviour>())
            {
                if (behaviour != null && behaviour.enabled && behaviour.GetType().FullName == "UnityEngine.U2D.PixelPerfectCamera")
                {
                    suspendedPixelPerfect.Add(behaviour);
                    behaviour.enabled = false;
                }
            }
            // PixelPerfectCamera.OnDisable changes the viewport and render state.
            cameraComponent.rect = viewport;
            cameraComponent.forceIntoRenderTexture = originalForceIntoRenderTexture;
            weather ??= new ZoomWeather(cameraComponent);
            controlling = true;
        }
        float size = originalSize / Plugin.Zoom;
        Matrix4x4 projection = Projection(Plugin.Zoom);
        if (cameraComponent.orthographicSize != size) cameraComponent.orthographicSize = size;
        if (cameraComponent.projectionMatrix != projection) cameraComponent.projectionMatrix = projection;
        weather?.Fit(cameraComponent);
    }

    private Matrix4x4 Projection(float zoom)
    {
        Matrix4x4 result = originalProjection;
        float horizontalScale = zoom * originalAspect / cameraComponent.aspect;
        // Scale the complete X/Y rows, including the game's oblique shear and offsets.
        // Always derive from the snapshot: repeated frames never compound zoom.
        for (int column = 0; column < 4; column++)
        {
            result[0, column] *= horizontalScale;
            result[1, column] *= zoom;
        }
        return result;
    }

    private void Restore()
    {
        if (!controlling || cameraComponent == null) return;
        weather?.Restore();
        cameraComponent.orthographicSize = originalSize;
        cameraComponent.projectionMatrix = Projection(1f);
        cameraComponent.forceIntoRenderTexture = originalForceIntoRenderTexture;
        foreach (Behaviour behaviour in suspendedPixelPerfect)
            if (behaviour != null) behaviour.enabled = true;
        suspendedPixelPerfect.Clear();
        controlling = false;
    }

    private void OnDisable() => Restore();
}

[HarmonyPatch(typeof(Player), "Start")]
internal static class AttachZoomPatch
{
    private static void Postfix() => ZoomCamera.ApplyMainCamera();
}
