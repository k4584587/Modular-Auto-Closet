#if UNITY_EDITOR
using System.Collections;
using nadena.dev.modular_avatar.core;
using UnityEngine;

public class AutoCloset : AvatarTagComponent

{
    private static Texture2D _icon;

    /// <summary>
    /// Name of the toggle root object created by the ToggleCreator.
    /// </summary>
    public string toggleRootName = "Toggle";

    public enum ClosetLanguage
    {
        English,
        Korean,
        Japanese
    }

    /// <summary>
    /// Editor UI language for AutoCloset related inspectors.
    /// </summary>
    public ClosetLanguage language = ClosetLanguage.Korean;

    /// <summary>
    /// Write Defaults setting for generated animator states.
    /// When true, animator states use WD ON; when false, WD OFF (VRChat recommended).
    /// </summary>
    public bool writeDefaults = false;

    public IEnumerable Clothes;
}
#endif
