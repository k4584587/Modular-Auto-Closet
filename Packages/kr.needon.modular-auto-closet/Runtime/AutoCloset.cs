using System.Collections;
using UnityEngine;

public class AutoCloset : MonoBehaviour
#if USE_VRC_SDK_BASE
    , VRC.SDKBase.IEditorOnly
#endif

{
    public IEnumerable Clothes;
}