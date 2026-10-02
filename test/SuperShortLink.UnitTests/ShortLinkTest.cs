using Microsoft.Extensions.DependencyInjection;
using SuperShortLink.Cache;
using SuperShortLink.Repository;
using SuperShortLink.Safety;
using System.Threading.Tasks;
using Xunit;

namespace SuperShortLink.UnitTests
{
    public class ShortLinkTest
    {
        private readonly IMemoryCaching _memory;
        private readonly Base62Converter _converter;
        private readonly IUrlSafetyService _urlSafetyService;

        public ShortLinkTest()
        {
            IServiceCollection services = new ServiceCollection();
            services.AddLogging();
            services.AddShortLink(option =>
            {
                option.ConnectionString = "Server=127.0.0.1;Database=unused;";
                option.DbType = DatabaseType.MySQL;
                option.Secrect = "s9LFkgy5RovixI1aOf8UhdY3r4DMplQZJXPqebE0WSjBn7wVzmN2Gc6THCAKut";
                option.CodeLength = 6;
            });
            var serviceProvider = services.BuildServiceProvider();
            _memory = serviceProvider.GetRequiredService<IMemoryCaching>();
            _converter = serviceProvider.GetRequiredService<Base62Converter>();
            _urlSafetyService = serviceProvider.GetRequiredService<IUrlSafetyService>();
        }

        [Fact]
        public void Generate_Key_Be_Valid() => Assert.NotNull(_converter.GenerateSecret());

        [Theory]
        [InlineData("abcd")]
        [InlineData("ftp://example.com/file")]
        [InlineData("http://127.0.0.1/private")]
        [InlineData("http://169.254.169.254/latest/meta-data")]
        public async Task Unsafe_Url_Should_Be_Rejected(string url)
        {
            var result = await _urlSafetyService.CheckAsync(url);
            Assert.False(result.Allowed);
        }

        [Fact]
        public void Cache_Should_Succeed()
        {
            Assert.True(_memory.Set("key", "value"));
            var cache = _memory.Get<string>("key");
            Assert.False(cache.IsNull);
            Assert.Equal("value", cache.Value);
        }

        [Fact]
        public void Base62_Convert_Should_Be_Same()
        {
            var converted = _converter.Encode(63);
            Assert.NotEmpty(converted);
            Assert.Equal(63, _converter.Decode(converted));
        }

        [Fact]
        public void Confuse_Should_Be_Same()
        {
            var converted = _converter.Confuse(int.MaxValue);
            Assert.Equal(int.MaxValue, _converter.ReCoverConfuse(converted));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Empty_Short_Key_Should_Be_Rejected(string key)
        {
            Assert.Equal(0, _converter.ReCoverConfuse(key));
        }
    }
}
