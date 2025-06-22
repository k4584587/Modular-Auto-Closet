#if UNITY_EDITOR
using System;
using nadena.dev.modular_avatar.core;
using UnityEngine;

[Serializable]
public class StandaloneToggleItem
{
    public GameObject target;
    public bool active = true;
}

public class StandaloneToggle : AvatarTagComponent
{
    public StandaloneToggleItem[] targets;
}
#endif
