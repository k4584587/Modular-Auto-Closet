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

[AddComponentMenu("Modular Avatar/Blendshape Toggle")]
[DisallowMultipleComponent]
[HelpURL("https://github.com/k4584587/Modular-Auto-Closet")]
[Icon("Packages/kr.needon.modular-auto-closet/Resource/toggleON.png")]
public class BlendshapeToggle : AvatarTagComponent
{
    public BlendshapeToggleItem[] shapes;
}
#endif
