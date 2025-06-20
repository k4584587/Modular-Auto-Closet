#if UNITY_EDITOR
using System;
using nadena.dev.modular_avatar.core;
using UnityEngine;

[Serializable]
public class BlendshapeToggleItem
{
    public SkinnedMeshRenderer mesh;
    public string shapeKey;
    public float value = 100f;
    public bool active = true;
}

public class BlendshapeToggle : AvatarTagComponent
{
    public BlendshapeToggleItem[] shapes;
}
#endif
