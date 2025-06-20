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

    public IEnumerable Clothes;
}
#endif