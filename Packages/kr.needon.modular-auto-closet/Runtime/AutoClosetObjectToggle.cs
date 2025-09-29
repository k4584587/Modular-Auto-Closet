#if UNITY_EDITOR
using System;
using nadena.dev.modular_avatar.core;
using UnityEngine;

[Serializable]
public class AutoClosetToggleTarget
{
    private static Texture2D _icon;
    public GameObject target;
    
    [HideInInspector]
    public bool active = true;
}


public class AutoClosetObjectToggle : AvatarTagComponent
{
    public AutoClosetToggleTarget[] targets;
}
#endif