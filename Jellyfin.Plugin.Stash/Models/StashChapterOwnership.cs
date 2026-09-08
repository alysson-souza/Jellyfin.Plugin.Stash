using System;
using MediaBrowser.Model.Entities;

namespace Stash.Models
{
    internal sealed class StashChapterOwnership
    {
        public StashChapterOwnership()
        {
        }

        public StashChapterOwnership(ChapterInfo chapter)
        {
            this.StartPositionTicks = chapter.StartPositionTicks;
            this.Name = chapter.Name;
            this.ImagePath = chapter.ImagePath;
        }

        public long StartPositionTicks { get; set; }

        public string Name { get; set; }

        public string ImagePath { get; set; }

        public static bool SameChapter(ChapterInfo first, ChapterInfo second)
            => first.StartPositionTicks == second.StartPositionTicks
                && string.Equals(first.Name, second.Name, StringComparison.Ordinal)
                && string.Equals(first.ImagePath, second.ImagePath, StringComparison.Ordinal)
#if __EMBY__
                && first.MarkerType == second.MarkerType
#endif
                ;

        public bool Matches(ChapterInfo chapter)
            => this.StartPositionTicks == chapter.StartPositionTicks
                && string.Equals(this.Name, chapter.Name, StringComparison.Ordinal)
                && string.Equals(this.ImagePath, chapter.ImagePath, StringComparison.Ordinal)
#if __EMBY__
                && chapter.MarkerType == MarkerType.Chapter
#endif
                ;
    }
}
