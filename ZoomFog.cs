using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using UnityEngine;

namespace SilverpineMods.CameraZoom;

internal static class ZoomFog
{
    private static FogBounds bounds = FogBounds.Native;
    private static Camera? measuredCamera;
    private static Matrix4x4 measuredProjection;
    private static float measuredDepth;
    internal static int OffsetX() => -bounds.MinX;
    internal static int OffsetY() => -bounds.MinY;
    internal static int Width() => bounds.MaxX - bounds.MinX + 1;
    internal static int Height() => bounds.MaxY - bounds.MinY + 1;

    internal static bool RefreshBounds(Camera camera)
    {
        FogBounds next = FogBounds.Native;
        if (camera != null && camera.orthographic && ZoomCamera.IsGameplay && !ZoomCamera.OverviewActive)
        {
            Vector3 center = camera.transform.position;
            // The native fog sprites lie at world Z=-3. Include the oblique projection.
            float depth = -3f - center.z;
            Matrix4x4 projection = camera.projectionMatrix;
            // Following the player does not change the footprint. Avoid world-point
            // conversions and fog rebuilding on every movement or rendered frame.
            if (measuredCamera == camera && measuredProjection == projection && measuredDepth == depth)
                return false;
            measuredCamera = camera;
            measuredProjection = projection;
            measuredDepth = depth;
            Vector3 lower = camera.ViewportToWorldPoint(new Vector3(0f, 0f, depth)) - center;
            Vector3 upper = camera.ViewportToWorldPoint(new Vector3(1f, 1f, depth)) - center;
            next = FogBounds.Cover(lower.x, upper.x, lower.y, upper.y);
        }
        else measuredCamera = null;
        if (next.Equals(bounds)) return false;
        bounds = next;
        return true;
    }
}

[HarmonyPatch(typeof(FOVManager), nameof(FOVManager.Initialize))]
internal static class ExpandFogPatch
{
    private static void Prefix()
    {
        ZoomCamera.ApplyMainCamera();
        ZoomFog.RefreshBounds(Camera.main);
    }

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        int replacements = 0;
        foreach (CodeInstruction instruction in instructions)
        {
            // Native Initialize builds a 20x12 grid at offsets -10,-5.
            // Change its dimensions only; keep native fog, raycasts, events and cleanup.
            string? method = instruction.LoadsConstant(20) ? nameof(ZoomFog.Width) :
                instruction.LoadsConstant(12) ? nameof(ZoomFog.Height) :
                instruction.LoadsConstant(10) ? nameof(ZoomFog.OffsetX) :
                instruction.LoadsConstant(5) ? nameof(ZoomFog.OffsetY) : null;
            if (method != null)
            {
                instruction.opcode = OpCodes.Call;
                instruction.operand = AccessTools.Method(typeof(ZoomFog), method);
                replacements++;
            }
            yield return instruction;
        }
        if (replacements != 4) throw new InvalidOperationException("Camera Zoom: unsupported FOVManager.Initialize grid layout.");
    }
}
