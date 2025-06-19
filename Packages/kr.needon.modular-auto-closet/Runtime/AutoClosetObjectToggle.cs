using System;
using nadena.dev.modular_avatar.core;
using UnityEngine;

[Serializable]
public class AutoClosetToggleTarget
{
    public GameObject target;
    public bool active = true;
}

/// <summary>
/// Replacement for ModularAvatarObjectToggle providing simple on/off control
/// over one or more GameObjects. Data is processed during build to generate
/// animator layers/clips similarly to ModularAvatarObjectToggle.
/// </summary>
public class AutoClosetObjectToggle : AvatarTagComponent
{
    public AutoClosetToggleTarget[] targets;
}
