using UnityEngine;
using System.Collections;

/// <summary>
/// The mobile canvas (joystick, look area, pause button) stays visible on every platform: Android plays with it, and
/// in the Editor/PC it works with the mouse while WASD/arrow keys also drive the robot (spec §3.1). It used to hide
/// itself off handheld devices, which made the joystick look missing in the Editor.
/// </summary>
public class MobileCanvasControl : MonoBehaviour {
}
