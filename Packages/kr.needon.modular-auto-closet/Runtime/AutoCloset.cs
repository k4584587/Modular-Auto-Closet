#if UNITY_EDITOR
using System.Collections;
using nadena.dev.modular_avatar.core;
using UnityEngine;
using UnityEngine.Serialization;

public class AutoCloset : AvatarTagComponent

{
    private static Texture2D _icon;

    /// <summary>
    /// Name of the toggle root object created by the ToggleCreator.
    /// </summary>
    public string toggleRootName = "Toggle";

    public enum ClosetLanguage
    {
        English,
        Korean,
        Japanese
    }

    /// <summary>
    /// Editor UI language for AutoCloset related inspectors.
    /// </summary>
    public ClosetLanguage language = ClosetLanguage.Korean;

    public enum WriteDefaultsMode
    {
        /// <summary>Detect the avatar's existing FX Write Defaults and match it.</summary>
        Auto,
        /// <summary>Force generated states to Write Defaults ON.</summary>
        On,
        /// <summary>Force generated states to Write Defaults OFF.</summary>
        Off
    }

    /// <summary>
    /// Write Defaults policy for generated animator states.
    /// Auto matches the avatar's existing FX (prevents WD mix that breaks clothing gimmicks).
    /// v1.0.8 serialized this as "writeDefaults" (bool): true(1)→On, false(0)→Auto.
    /// </summary>
    [FormerlySerializedAs("writeDefaults")]
    public WriteDefaultsMode writeDefaultsMode = WriteDefaultsMode.Auto;

    /// <summary>
    /// Reset Modular Avatar menu/reactive parameters inside clothing items when switching closet states.
    /// This prevents object-toggle based clothing gimmicks from keeping stale values after the clothing root was disabled.
    /// </summary>
    public bool resetChildMenuParameters = true;

    public IEnumerable Clothes;
}
#endif
