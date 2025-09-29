#if UNITY_EDITOR
using System;
using nadena.dev.modular_avatar.core;
using UnityEngine;

/// <summary>
/// Unified component for Closet item configuration.
/// Combines object toggles and blendshape settings into a single attachable component.
/// </summary>
public class ClosetConfig : AvatarTagComponent
{
    public ClosetToggleItem[] toggles;           // Optional object toggles
    public ClosetBlendshapeItem[] shapes;        // Optional blendshape settings
}
#endif

