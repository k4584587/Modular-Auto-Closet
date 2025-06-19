using System;
using nadena.dev.modular_avatar.core;
using UnityEngine;

[Serializable]
public class AutoClosetToggleTarget
{
    public GameObject target;
    public bool active = true;
}


public class AutoClosetObjectToggle : AvatarTagComponent
{
    public AutoClosetToggleTarget[] targets;
}
