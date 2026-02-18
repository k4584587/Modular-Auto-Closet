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