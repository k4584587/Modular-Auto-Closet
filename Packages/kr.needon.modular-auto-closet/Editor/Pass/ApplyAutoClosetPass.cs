using System;
using nadena.dev.ndmf;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace needon.Editor.Pass
{
    internal class ApplyAutoClosetPass : Pass<ApplyAutoClosetPass>
    {
        private const string ToggleParameterName = "AutoCloset";
        private const string AnimatorLayerName = "AutoCloset";

        private AnimatorController _autoClosetController;
        private int _layerIndex = -1;

        protected override void Execute(BuildContext context)
        {
            try
            {
                InitializeControllerAndLayer(context);
                ProcessAutoCloset(context);
            }
            catch (Exception e)
            {
                Debug.LogError($"AutoCloset 처리 중 오류 발생: {e.Message}");
                throw;
            }
        }

        private void InitializeControllerAndLayer(BuildContext context)
        {
            var avatar = context.AvatarDescriptor;
            _autoClosetController = (AnimatorController)AutoClosetUtil.GetAvatarFxAnimator(avatar);

            if (_autoClosetController == null)
            {
                throw new InvalidOperationException("FX Animator Controller를 찾을 수 없습니다.");
            }

            CreateLayerAndParameter();
            _layerIndex = FindAutoClosetLayerIndex();
        }

        private void CreateLayerAndParameter()
        {
            AutoClosetUtil.ApplyCreateAnimatorLayer(_autoClosetController, AnimatorLayerName);
            AutoClosetUtil.AddAnimatorParameter(_autoClosetController, ToggleParameterName, AnimatorControllerParameterType.Int);
            EditorUtility.SetDirty(_autoClosetController);
            AssetDatabase.SaveAssets();
        }

        private int FindAutoClosetLayerIndex()
        {
            for (int i = 0; i < _autoClosetController.layers.Length; i++)
            {
                if (_autoClosetController.layers[i].name == AnimatorLayerName)
                {
                    return i;
                }
            }

            throw new InvalidOperationException($"'{AnimatorLayerName}' 레이어를 찾을 수 없습니다.");
        }

        private void ProcessAutoCloset(BuildContext context)
        {
            var autoClosetContext = context.Extension<AutoClosetContext>();
            if (autoClosetContext == null || autoClosetContext.AutoCloset == null)
            {
                return;
            }

            var autoCloset = autoClosetContext.AutoCloset;
            var closetGameObject = autoCloset.gameObject;

            if (!ValidateCloset(closetGameObject))
            {
                return;
            }

            var stateMachine = _autoClosetController.layers[_layerIndex].stateMachine;
            CreateClosetStates(closetGameObject, stateMachine);
        }


        private bool ValidateCloset(GameObject closet)
        {
            if (closet == null) return false;
            if (closet.transform.childCount == 0)
            {
                Debug.LogWarning("옷장에 자식 오브젝트가 없습니다.");
                return false;
            }

            return true;
        }

        private void CreateClosetStates(GameObject closet, AnimatorStateMachine stateMachine)
        {
            string parentName = closet.transform.parent != null ? closet.transform.parent.name : closet.name;
            Transform defaultClothes = closet.transform.GetChild(0);

            // 기본옷 상태 생성
            CreateDefaultClothesState(closet, parentName, defaultClothes, stateMachine);

            // 추가 옷 상태 생성
            CreateAdditionalClothesStates(closet, parentName, defaultClothes, stateMachine);
        }

        private void CreateDefaultClothesState(GameObject closet, string parentName, Transform defaultClothes, AnimatorStateMachine stateMachine)
        {
            AnimationClip defaultClip = AutoClosetUtil.CreateClosetAnimationClip(closet, parentName, defaultClothes.name);
            AnimatorState defaultState = stateMachine.AddState(defaultClothes.name, new Vector3(300, 0, 0));
            defaultState.motion = defaultClip;
            stateMachine.defaultState = defaultState;

            var anyToDefaultTransition = stateMachine.AddAnyStateTransition(defaultState);
            ConfigureTransition(anyToDefaultTransition, 0);
        }

        private void CreateAdditionalClothesStates(GameObject closet, string parentName, Transform defaultClothes, AnimatorStateMachine stateMachine)
        {
            int index = 1;
            foreach (Transform child in closet.transform)
            {
                if (child == defaultClothes) continue;

                AnimationClip stateClip = AutoClosetUtil.CreateClosetAnimationClip(closet, parentName, child.name);
                AnimatorState newState = stateMachine.AddState(child.name, new Vector3(300, index * 60, 0));
                newState.motion = stateClip;

                var anyStateTransition = stateMachine.AddAnyStateTransition(newState);
                ConfigureTransition(anyStateTransition, index);

                index++;
            }
        }

        private void ConfigureTransition(AnimatorStateTransition transition, int parameterValue)
        {
            transition.hasExitTime = false;
            transition.exitTime = 0f;
            transition.duration = 0f;
            transition.AddCondition(AnimatorConditionMode.Equals, parameterValue, ToggleParameterName);
        }
    }
}
