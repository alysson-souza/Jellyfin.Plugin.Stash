#if __EMBY__
using MediaBrowser.Model.Services;

namespace Stash.Api
{
    [Route("/" + Helpers.ImageUrl.Route, "GET")]
    public sealed class SignedImageRequest : IReturn<byte[]>
    {
        public string Resource { get; set; }

        public string Signature { get; set; }
    }
}
#endif
