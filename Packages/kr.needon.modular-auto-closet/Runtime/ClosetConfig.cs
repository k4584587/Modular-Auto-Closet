#if UNITY_EDITOR
using System;
using nadena.dev.modular_avatar.core;
using UnityEngine;

/// <summary>
/// Parameter driver item for VRC Avatar Parameter Driver.
/// Supports Set, Add, Random, and Copy operations.
/// </summary>
[Serializable]
public class ClosetParameterDriverItem
{
    public enum ChangeType
    {
        Set,
        Add,
        Random,
        Copy
    }

    public ChangeType type = ChangeType.Set;
    public string name = "";
    public float value = 0f;
    public float valueMin = 0f;
    public float valueMax = 1f;
    public float chance = 1f;
    public string source = "";
    public string destName = "";
}

/// <summary>
/// Unified component for Closet item configuration.
/// Combines object toggles, blendshape settings, and parameter drivers into a single attachable component.
/// </summary>
public class ClosetConfig : AvatarTagComponent
{
    public ClosetToggleItem[] toggles;           // Optional object toggles
    public ClosetBlendshapeItem[] shapes;        // Optional blendshape settings
    public ClosetParameterDriverItem[] drivers;  // Optional parameter drivers
}
#endif

