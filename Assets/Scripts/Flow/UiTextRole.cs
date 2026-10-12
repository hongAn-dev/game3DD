using UnityEngine;

/// <summary>Stable semantic role for UI text styling; builders set roles instead of relying on display names.</summary>
public enum UiRole {
	GameTitle,
	MenuSubtitle,
	EndingCaption,
	CompletionTitle,
	CompletionBody,
	ZoneLabel
}

[DisallowMultipleComponent]
public sealed class UiTextRole : MonoBehaviour {
	public UiRole role;
}