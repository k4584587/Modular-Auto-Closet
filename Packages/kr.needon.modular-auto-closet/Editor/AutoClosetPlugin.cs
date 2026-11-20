using nadena.dev.ndmf;
using needon.Editor;
using needon.Editor.Pass;

[assembly: ExportsPlugin(typeof(AutoClosetPlugin))]

namespace needon.Editor
{
    public class AutoClosetPlugin : Plugin<AutoClosetPlugin>
    {
        public override string QualifiedName => "kr.needon.modular-auto-closet";
        public override string DisplayName => "AutoCloset";

        protected override void Configure()
        {
            // Resolving: RemoveEditorOnlyPass가 EditorOnly 의상을 삭제하기 직전에 경고 로그를 남김
            InPhase(BuildPhase.Resolving)
                .Run(WarnEditorOnlyOutfitsPass.Instance)
                .BeforePass(nadena.dev.ndmf.builtin.RemoveEditorOnlyPass.Instance);

            InPhase(BuildPhase.Generating)
                .WithRequiredExtension(typeof(AutoClosetContext), s =>
                    s.Run(ApplyAutoClosetPass.Instance)
                     .Then.Run(ApplyToggleCreatorPass.Instance)
                     .Then.Run(ApplyStandaloneTogglePass.Instance));

            // Optimizing: 다른 툴이 레이어 Weight를 0으로 초기화하는 문제를 최종 복원
            InPhase(BuildPhase.Optimizing)
                .WithRequiredExtension(typeof(AutoClosetContext), s =>
                    s.Run(EnsureLayerWeightsPass.Instance));
        }
    }   
}