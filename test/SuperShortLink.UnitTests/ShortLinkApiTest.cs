using Microsoft.Extensions.DependencyInjection;
using SuperShortLink.Api;
using Xunit;

namespace SuperShortLink.UnitTests
{
    public class ApiTest
    {
        [Fact]
        public void Api_Client_Should_Be_Registered()
        {
            IServiceCollection services = new ServiceCollection();
            services.AddShortLinkApi(option =>
            {
                option.ApiDomain = "https://short.example.test";
                option.AppSecret = "test-secret";
                option.AppCode = "test-code";
            });

            using var serviceProvider = services.BuildServiceProvider();
            Assert.NotNull(serviceProvider.GetService<IShortLinkApiService>());
        }
    }
}
