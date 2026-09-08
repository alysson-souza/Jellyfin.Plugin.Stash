using System.ComponentModel;

#if __EMBY__
using Emby.Web.GenericEdit;
using MediaBrowser.Model.Attributes;
#else
using MediaBrowser.Model.Plugins;
#endif

namespace Stash.Configuration
{
    public enum TagStyle
    {
        [Description("Genres")]
        Genre = 0,

        [Description("Tags")]
        Tag = 1,

        [Description("Do not import")]
        Disabled = 2,
    }

#if __EMBY__
    public class PluginConfiguration : EditableOptionsBase
    {
#else
    public class PluginConfiguration : BasePluginConfiguration
    {
#endif
        public PluginConfiguration()
        {
            this.StashEndpoint = "http://localhost:9999";
            this.StashAPIKey = string.Empty;

            this.UseFilePath = true;
            this.UseFullPathToSearch = true;

            this.PathPrefixJellyfin = string.Empty;
            this.PathPrefixStash = string.Empty;

            this.AddDisambiguation = false;

            this.TagStyle = TagStyle.Tag;

            this.ImportSceneMarkers = false;
        }

#if __EMBY__
        public override string EditorTitle => Plugin.Instance.Name;
#endif

        [DisplayName("Stash endpoint")]
        [Description("Base URL of your Stash instance, including port. Do not include /graphql.")]
        public string StashEndpoint { get; set; }

#if __EMBY__
        [IsPassword]
#endif
        [DisplayName("Stash API key")]
        [Description("Used for authenticated GraphQL calls. Leave blank only if Stash allows anonymous access.")]
        public string StashAPIKey { get; set; }

        [DisplayName("Match scenes by file path")]
        [Description("Match on the file path instead of searching Stash by title.")]
        public bool UseFilePath { get; set; }

#if __EMBY__
        [VisibleCondition(nameof(UseFilePath), SimpleCondition.IsTrue)]
#endif
        [DisplayName("Match on the full path")]
        [Description("Require the whole path to match. When off, only the file name is matched.")]
        public bool UseFullPathToSearch { get; set; }

        [DisplayName("Media server prefix")]
        [Description("Translate this media server's path prefix to the Stash prefix.")]
        public string PathPrefixJellyfin { get; set; }

        [DisplayName("Stash prefix")]
        [Description("The matching path prefix as Stash sees it. Leave both blank if the paths are identical.")]
        public string PathPrefixStash { get; set; }

        [DisplayName("Add disambiguation to performer names")]
        [Description("Append the Stash disambiguation text so performers who share a name stay distinct.")]
        public bool AddDisambiguation { get; set; }

        [DisplayName("Tag style")]
        [Description("Where Stash tags are written: as tags, as genres, or not at all.")]
        public TagStyle TagStyle { get; set; }

        [DisplayName("Import scene markers as chapters")]
        [Description("Turn Stash scene markers into chapters, downloading each marker image to this server.")]
        public bool ImportSceneMarkers { get; set; }
    }
}
