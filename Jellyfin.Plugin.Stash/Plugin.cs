using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller;
using MediaBrowser.Model.Plugins;
using Newtonsoft.Json;
using Stash.Configuration;
using Stash.Helpers;

#if __EMBY__
using MediaBrowser.Common;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Model.Logging;
#else
using System.Net.Http;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Serialization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Extensions.Logging;
#endif

[assembly: CLSCompliant(false)]

namespace Stash
{
#if __EMBY__
    public class Plugin : BasePluginSimpleUI<PluginConfiguration>, IDisposable
    {
        public Plugin(IServerApplicationHost applicationHost, IApplicationPaths applicationPaths, IHttpClient http, ILogManager logger)
            : base(applicationHost)
#else
    public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages, IDisposable
    {
        public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer, IHttpClientFactory http, ILogger<Plugin> logger, IServerApplicationHost applicationHost, IHttpContextAccessor httpContextAccessor)
            : base(applicationPaths, xmlSerializer)
#endif
        {
            Instance = this;
            Http = http;

#if __EMBY__
            if (logger != null)
            {
                Log = logger.GetLogger(this.Name);
            }
#else
            Log = logger;
#endif

            this.Images = new ImageUrl(
                Path.Combine(applicationPaths.DataPath, "stash-images", "signing.key"),
                () => (this.Configuration.StashEndpoint, this.Configuration.StashAPIKey),
#if __EMBY__
                () => applicationHost.GetLocalApiUrl(IPAddress.Loopback));
#else
                () =>
                {
                    var request = httpContextAccessor.HttpContext?.Request;
                    return request == null
                        ? applicationHost.GetApiUrlForLocalAccess()
                        : UriHelper.BuildAbsolute(request.Scheme, request.Host, request.PathBase);
                });
#endif

            JsonConvert.DefaultSettings = () => new JsonSerializerSettings { MaxDepth = 128 };
        }

#if __EMBY__
        public static IHttpClient Http { get; set; }
#else
        public static IHttpClientFactory Http { get; set; }
#endif

        public static ILogger Log { get; set; }

        public static Plugin Instance { get; private set; }

        public ImageUrl Images { get; }

        public override string Name => "Stash (Extended)";

        public override Guid Id => Guid.Parse("fb7a756d-d694-461a-99eb-2458b44f983f");

#if __EMBY__
        public PluginConfiguration Configuration => this.GetOptions();
#else
#endif

        public void Dispose() => this.Images.Dispose();

        public IEnumerable<PluginPageInfo> GetPages()
            => new[]
            {
                new PluginPageInfo
                {
                    Name = this.Name,
                    EmbeddedResourcePath = $"{this.GetType().Namespace}.Configuration.configPage.html",
                },
            };
    }
}
