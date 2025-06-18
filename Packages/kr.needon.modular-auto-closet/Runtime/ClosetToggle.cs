using System;
using UnityEngine;

[Serializable]
public class ClosetToggleItem
{
    public GameObject target;
    public bool active = true;
}

public class ClosetToggle : MonoBehaviour
{
    public ClosetToggleItem[] toggles;
}
