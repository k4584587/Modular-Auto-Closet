using System;
using nadena.dev.modular_avatar.core;
using UnityEngine;

[Serializable]
public class ClosetBlendshapeItem
{
    public SkinnedMeshRenderer mesh;
    public string shapeKey;
    public float value = 100f;
}

public class ClosetBlendshape : AvatarTagComponent
{
    public ClosetBlendshapeItem[] shapes;
}