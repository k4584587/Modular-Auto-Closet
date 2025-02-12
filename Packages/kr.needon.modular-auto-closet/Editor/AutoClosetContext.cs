using nadena.dev.ndmf;

namespace needon.Editor
{
    internal class AutoClosetContext : IExtensionContext
    {
        public AutoCloset AutoCloset { get; private set; }
        
        public void OnActivate(BuildContext context)
        {
            AutoCloset = context.AvatarRootObject.GetComponentInChildren<AutoCloset>(true);
        }

        public void OnDeactivate(BuildContext context)
        {
            AutoCloset = null;
        }
    }
}