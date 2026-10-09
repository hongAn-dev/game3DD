using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Full-screen dim behind a menu (pause): it keeps catching taps so nothing behind it (HUD pause button, look area)
/// reacts while the menu is open. Being an event handler also tells ControlSetup it is meant to block rays.
/// </summary>
public class ModalBackdrop : MonoBehaviour, IPointerClickHandler {
	public void OnPointerClick(PointerEventData eventData) {
	}
}
