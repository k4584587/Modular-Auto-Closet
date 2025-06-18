using System;
using nadena.dev.modular_avatar.core;
using UnityEngine;

[Serializable]
public class ClosetToggleItem
{
    private static Texture2D _icon;
    public GameObject target;
    public bool active = true;
}

public class ClosetToggle : AvatarTagComponent
{
    public ClosetToggleItem[] toggles;
}
