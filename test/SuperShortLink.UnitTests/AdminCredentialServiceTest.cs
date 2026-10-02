#nullable enable
using FakeItEasy;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using System;
using System.IO;
using Xunit;

namespace SuperShortLink.UnitTests;

public class AdminCredentialServiceTest
{
    [Fact]
    public void ChangedPasswordPersistsAndRejectsBootstrapPassword()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"supershortlink-credentials-{Guid.NewGuid():N}");
        var path = Path.Combine(directory, "admin-password.hash");
        try
        {
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new[]
            {
                new System.Collections.Generic.KeyValuePair<string, string?>("AdminCredentialPath", path)
            }).Build();
            var options = Options.Create(new ShortLinkOptions { LoginAcount = "admin", LoginPassword = "A123456" });
            var environment = A.Fake<IWebHostEnvironment>();
            var credentials = new AdminCredentialService(options, configuration, environment);

            Assert.True(credentials.Verify("admin", "A123456"));
            Assert.False(credentials.ChangePassword("wrong", "new-long-password"));
            Assert.True(credentials.ChangePassword("A123456", "new-long-password"));
            Assert.DoesNotContain("new-long-password", File.ReadAllText(path));

            var reloaded = new AdminCredentialService(options, configuration, environment);
            Assert.True(reloaded.Verify("admin", "new-long-password"));
            Assert.False(reloaded.Verify("admin", "A123456"));
            Assert.False(reloaded.Verify("other", "new-long-password"));
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }
}
