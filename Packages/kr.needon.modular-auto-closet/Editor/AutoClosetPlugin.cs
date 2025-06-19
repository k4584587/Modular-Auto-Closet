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
                     .Run(ApplyToggleCreatorPass.Instance));
        }
    }   
}