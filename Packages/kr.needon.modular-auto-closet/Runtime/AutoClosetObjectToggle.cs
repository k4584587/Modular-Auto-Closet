#if UNITY_EDITOR
using System;
using nadena.dev.modular_avatar.core;
using UnityEngine;

[Serializable]
public class AutoClosetToggleTarget
{
    private static Texture2D _icon;
    public GameObject target;

    /// <summary>
    /// 토글을 켰을 때 이 대상이 켜지면 true, 꺼지면 false. 토글을 끄면 반대가 됩니다.
    /// </summary>
    public bool active = true;
}


public class AutoClosetObjectToggle : AvatarTagComponent
{
    public AutoClosetToggleTarget[] targets;
}
#endif