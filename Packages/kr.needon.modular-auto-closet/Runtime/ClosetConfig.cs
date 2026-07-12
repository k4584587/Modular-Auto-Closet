#if UNITY_EDITOR
using System;
using nadena.dev.modular_avatar.core;
using UnityEngine;

/// <summary>
/// Parameter driver item for VRC Avatar Parameter Driver.
/// Supports Set, Add, Random, and Copy operations.
/// </summary>
[Serializable]
public class ClosetParameterDriverItem
{
    public enum ChangeType
    {
        Set,
        Add,
        Random,
        Copy
    }

    public enum TargetMode
    {
        Parameter,   // 파라미터 이름 문자열로 지정 (기존 방식)
        MenuTarget   // 의상/토글 오브젝트 참조 — 빌드 시 MA MenuItem에서 (이름, 값)을 해결
    }

    public ChangeType type = ChangeType.Set;
    public string name = "";
    public float value = 0f;
    public float valueMin = 0f;
    public float valueMax = 1f;
    public float chance = 1f;
    public string source = "";
    public string destName = "";

    // v2 (parameter-driver-v2.md): 참조 기반 타겟팅 + 미등록 파라미터 자동 등록.
    // 신규 필드가 모두 기본값이면 기존 직렬화 데이터와 동작이 완전히 동일하다.
    public TargetMode targetMode = TargetMode.Parameter;
    public GameObject targetObject;         // MenuTarget 모드: MA MenuItem을 가진 의상/토글 오브젝트
    public bool menuTargetOn = true;        // MenuTarget 대상이 Bool 토글일 때 켜기(true)/끄기(false)
    public bool autoRegister = false;       // 미등록 파라미터를 빌드 시 MA Parameters로 등록
    public bool autoRegisterSynced = false; // 등록 시 synced 여부 (기본 local — 동기화 예산 보호)
}

/// <summary>
/// Unified component for Closet item configuration.
/// Combines object toggles, blendshape settings, and parameter drivers into a single attachable component.
/// </summary>
public class ClosetConfig : AvatarTagComponent
{
    public ClosetToggleItem[] toggles;           // Optional object toggles
    public ClosetBlendshapeItem[] shapes;        // Optional blendshape settings
    public ClosetParameterDriverItem[] drivers;  // Optional parameter drivers
}
#endif

