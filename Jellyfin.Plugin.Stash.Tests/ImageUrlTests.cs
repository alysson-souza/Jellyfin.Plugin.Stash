using Stash.Helpers;
using Xunit;

namespace Stash.Tests
{
    public class ImageUrlTests
    {
        private const string Endpoint = "http://stash.example:9999";
        private const string ApiKey = "abc.def-ghi_jkl";

        [Fact]
        public void AppendsApiKeyToUrlWithExistingQuery()
        {
            Assert.Equal(
                Endpoint + "/performer/1/image?t=1&apikey=abc.def-ghi_jkl",
                ImageUrl.WithApiKey(Endpoint + "/performer/1/image?t=1", ApiKey));
        }

        [Fact]
        public void AppendsApiKeyToUrlWithoutQuery()
        {
            Assert.Equal(
                Endpoint + "/studio/1/image?apikey=abc.def-ghi_jkl",
                ImageUrl.WithApiKey(Endpoint + "/studio/1/image", ApiKey));
        }

        [Fact]
        public void EncodesApiKeyCharactersThatAreUnsafeInAQuery()
        {
            Assert.Equal(
                Endpoint + "/studio/1/image?apikey=a%2Bb%26c%3Dd",
                ImageUrl.WithApiKey(Endpoint + "/studio/1/image", "a+b&c=d"));
        }

        [Fact]
        public void LeavesUrlAloneWhenApiKeyIsMissing()
        {
            const string url = "http://stash.example:9999/scene/1/screenshot?t=1";

            Assert.Equal(url, ImageUrl.WithApiKey(url, null));
            Assert.Equal(url, ImageUrl.WithApiKey(url, string.Empty));
        }

        [Theory]
        [InlineData("http://stash.example:9999/scene/1/screenshot?apikey=existing")]
        [InlineData("http://stash.example:9999/scene/1/screenshot?t=1&apikey=existing")]
        [InlineData("http://stash.example:9999/scene/1/screenshot?t=1&APIKEY=existing")]
        public void DoesNotAppendASecondApiKey(string url)
        {
            Assert.Equal(url, ImageUrl.WithApiKey(url, ApiKey));
        }

        [Fact]
        public void LeavesEmptyUrlAlone()
        {
            Assert.Null(ImageUrl.WithApiKey(null, ApiKey));
            Assert.Equal(string.Empty, ImageUrl.WithApiKey(string.Empty, ApiKey));
        }

        [Fact]
        public void DoesNotMatchApiKeyInsideAnotherParameterName()
        {
            Assert.Equal(
                Endpoint + "/scene/1/screenshot?myapikey=x&apikey=abc.def-ghi_jkl",
                ImageUrl.WithApiKey(Endpoint + "/scene/1/screenshot?myapikey=x", ApiKey));
        }
    }
}
