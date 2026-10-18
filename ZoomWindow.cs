using Silverpine.ModdingTools;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SilverpineMods.CameraZoom;

internal sealed class ZoomWindow : ModToolBehaviour
{
    private string entry = "", status = "Changes apply immediately and are saved automatically.";
    private Rect window = new(28f, 300f, 480f, 322f);
    private GUIStyle? labelStyle, titleStyle, buttonStyle, fieldStyle, windowStyle;
    private readonly List<Texture2D> textures = new();

    internal static void Open(ModToolSession session)
    {
        GameObject? root = null;
        try
        {
            // IMGUI consuming an event does not block uGUI's independent raycaster.
            // A framework-owned overlay catches native scroll/drag/click input too.
            root = ModUi.CreateOverlayCanvas("Camera Zoom Controls", sortingOrder: 10000);
            Image inputShield = root.AddComponent<Image>();
            inputShield.color = new Color(0f, 0f, 0f, 0.01f);
            inputShield.raycastTarget = true;
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            ZoomWindow view = root.AddComponent<ZoomWindow>();
            view.AttachSession(session);
            view.entry = Plugin.Zoom.ToString("0.0", CultureInfo.CurrentCulture);
        }
        catch
        {
            if (root != null) Destroy(root);
            session.Close();
            throw;
        }
    }

    private void OnGUI()
    {
        using ModGuiScope scope = ModGui.BeginScaled(1280f, 720f);
        GUI.depth = -1000;
        GUI.enabled = true;
        GUI.color = GUI.backgroundColor = GUI.contentColor = Color.white;
        if (windowStyle == null) CreateStyles();
        if (Event.current.type == EventType.MouseDown && window.Contains(Event.current.mousePosition))
            GUI.FocusWindow(GetInstanceID());
        GUI.BringWindowToFront(GetInstanceID());
        window = GUI.Window(GetInstanceID(), window, DrawWindow, "Camera Zoom", windowStyle!);
        window.x = Mathf.Clamp(window.x, 0f, 1280f - window.width);
        window.y = Mathf.Clamp(window.y, 0f, 720f - window.height);
    }

    private void DrawWindow(int id)
    {
        GUI.Label(new Rect(20, 30, 440, 35), $"World zoom  {Plugin.Zoom:0.0}x", titleStyle);
        GUI.Label(new Rect(20, 74, 440, 28), "0.2x  Zoom out                  Zoom in  2.0x", labelStyle);
        float step = GUI.HorizontalSlider(new Rect(24, 112, 432, 24),
            Mathf.Round((Plugin.Zoom - ZoomMath.Minimum) * 10f), 0f, ZoomMath.SliderSteps);
        float selected = ZoomMath.Normalize(ZoomMath.Minimum + Mathf.Round(step) / 10f);
        if (selected != Plugin.Zoom) SetZoom(selected);

        GUI.enabled = Plugin.Zoom > ZoomMath.Minimum;
        if (GUI.Button(new Rect(20, 145, 70, 34), "- 0.1", buttonStyle)) SetZoom(Plugin.Zoom - 0.1f);
        GUI.enabled = true;
        GUI.SetNextControlName("ZoomEntry");
        entry = GUI.TextField(new Rect(103, 145, 105, 34), entry, 16, fieldStyle);
        bool submit = Event.current.type == EventType.KeyDown &&
            (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter) &&
            GUI.GetNameOfFocusedControl() == "ZoomEntry";
        if (GUI.Button(new Rect(220, 145, 140, 34), "Apply value", buttonStyle) || submit)
        {
            if (ZoomMath.TryParse(entry, out float parsed)) SetZoom(parsed);
            else status = "Enter a number from 0.2 to 2.0 (for example, 0.8).";
            if (submit) Event.current.Use();
        }
        GUI.enabled = Plugin.Zoom < ZoomMath.Maximum;
        if (GUI.Button(new Rect(372, 145, 88, 34), "+ 0.1", buttonStyle)) SetZoom(Plugin.Zoom + 0.1f);
        GUI.enabled = true;
        GUI.Label(new Rect(20, 190, 440, 52), status, labelStyle);
        if (GUI.Button(new Rect(20, 261, 235, 36), "Reset to normal (1.0x)", buttonStyle)) SetZoom(1f);
        if (GUI.Button(new Rect(275, 261, 185, 36), "Close", buttonStyle))
        {
            ReleaseSession();
            gameObject.SetActive(false);
            Destroy(gameObject);
        }
        GUI.DragWindow(new Rect(0, 0, window.width, 25));
    }

    private void SetZoom(float value)
    {
        Plugin.SetZoom(value);
        entry = Plugin.Zoom.ToString("0.0", CultureInfo.CurrentCulture);
        status = ZoomCamera.OverviewActive ? "Saved. Applies when the whole-tile debug view closes." :
            !ZoomCamera.IsGameplay ? "Saved. Applies when you enter the world." :
            "Zoom applied and saved. Direct entries round to the nearest 0.1.";
    }

    private void CreateStyles()
    {
        Color text = new(0.93f, 0.94f, 0.95f, 1f);
        Texture2D charcoal = Solid(new Color(0.14f, 0.15f, 0.16f, 1f));
        Texture2D button = Solid(new Color(0.23f, 0.25f, 0.27f, 1f));
        Texture2D hover = Solid(new Color(0.31f, 0.34f, 0.37f, 1f));
        Texture2D field = Solid(new Color(0.08f, 0.09f, 0.10f, 1f));
        windowStyle = Surface(GUI.skin.window, charcoal, charcoal, text);
        windowStyle.fontSize = 16;
        labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 17, wordWrap = true };
        labelStyle.normal.textColor = text;
        titleStyle = new GUIStyle(labelStyle) { fontSize = 24, fontStyle = FontStyle.Bold };
        buttonStyle = Surface(GUI.skin.button, button, hover, text);
        buttonStyle.fontSize = 16;
        fieldStyle = Surface(GUI.skin.textField, field, button, text);
        fieldStyle.fontSize = 18;
    }

    private static GUIStyle Surface(GUIStyle source, Texture2D normal, Texture2D active, Color text)
    {
        GUIStyle style = new(source) { border = new RectOffset(0, 0, 0, 0) };
        foreach (GUIStyleState state in new[] { style.normal, style.onNormal, style.hover, style.onHover,
            style.active, style.onActive, style.focused, style.onFocused })
        {
            state.background = normal;
            state.textColor = text;
        }
        style.hover.background = style.active.background = style.focused.background = active;
        return style;
    }

    private Texture2D Solid(Color color)
    {
        Texture2D texture = new(1, 1, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
        texture.SetPixel(0, 0, color);
        texture.Apply(false, true);
        textures.Add(texture);
        return texture;
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        foreach (Texture2D texture in textures) Destroy(texture);
        textures.Clear();
    }
}
