#if !__EMBY__
using MediaBrowser.Common.Net;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Stash.Helpers
{
    public sealed class SignedImageRegistration : IPluginServiceRegistrator
    {
        public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
            => serviceCollection.AddHttpClient(NamedClient.Default)
                .AddHttpMessageHandler(() => new SignedImageHandler(() => Plugin.Instance.Images));
    }
}
#endif
