using FluentAssertions;
using LinguaCoach.Application.Notifications;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LinguaCoach.UnitTests.Notifications;

public sealed class KeyProtectionOptionsTests
{
    [Fact]
    public void Defaults_AreCorrect()
    {
        var opts = new NotificationKeyProtectionOptions();

        opts.ApplicationName.Should().Be("SpeakPath");
        opts.KeysPath.Should().Be("./app-data/data-protection-keys");
        opts.KeyProtectionMode.Should().Be(DataProtectionKeyMode.None);
        opts.CertificatePath.Should().BeNull();
        opts.CertificatePassword.Should().BeNull();
        opts.CertificateThumbprint.Should().BeNull();
        NotificationKeyProtectionOptions.SectionName.Should().Be("DataProtection");
    }

    [Fact]
    public void BindsFromConfiguration_NoneMode()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DataProtection:ApplicationName"] = "TestApp",
                ["DataProtection:KeysPath"] = "/tmp/keys",
                ["DataProtection:KeyProtectionMode"] = "None",
            })
            .Build();

        var sp = new ServiceCollection()
            .Configure<NotificationKeyProtectionOptions>(config.GetSection("DataProtection"))
            .BuildServiceProvider();

        var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<NotificationKeyProtectionOptions>>().Value;

        opts.ApplicationName.Should().Be("TestApp");
        opts.KeysPath.Should().Be("/tmp/keys");
        opts.KeyProtectionMode.Should().Be(DataProtectionKeyMode.None);
    }

    [Fact]
    public void BindsFromConfiguration_CertificateMode()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DataProtection:KeyProtectionMode"] = "Certificate",
                ["DataProtection:CertificatePath"] = "/certs/dp.pfx",
                ["DataProtection:CertificatePassword"] = "secret",
            })
            .Build();

        var sp = new ServiceCollection()
            .Configure<NotificationKeyProtectionOptions>(config.GetSection("DataProtection"))
            .BuildServiceProvider();

        var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<NotificationKeyProtectionOptions>>().Value;

        opts.KeyProtectionMode.Should().Be(DataProtectionKeyMode.Certificate);
        opts.CertificatePath.Should().Be("/certs/dp.pfx");
        opts.CertificatePassword.Should().Be("secret");
    }

    [Fact]
    public void KeyProtectionMode_ParsesCaseInsensitive()
    {
        foreach (var value in new[] { "none", "NONE", "None" })
        {
            var parsed = Enum.Parse<DataProtectionKeyMode>(value, ignoreCase: true);
            parsed.Should().Be(DataProtectionKeyMode.None);
        }

        foreach (var value in new[] { "certificate", "CERTIFICATE", "Certificate" })
        {
            var parsed = Enum.Parse<DataProtectionKeyMode>(value, ignoreCase: true);
            parsed.Should().Be(DataProtectionKeyMode.Certificate);
        }
    }

    [Fact]
    public void KeyProtectionMode_InvalidValue_DefaultsToNone_ViaTryParse()
    {
        var valid = Enum.TryParse<DataProtectionKeyMode>("bogus", ignoreCase: true, out var result);
        valid.Should().BeFalse();
        result.Should().Be(DataProtectionKeyMode.None);
    }

    [Theory]
    [InlineData(DataProtectionKeyMode.None)]
    [InlineData(DataProtectionKeyMode.Certificate)]
    public void AllModes_AreDefinedInEnum(DataProtectionKeyMode mode)
    {
        Enum.IsDefined(mode).Should().BeTrue();
    }

    [Fact]
    public void AddInfrastructure_NoneMode_DoesNotThrow()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DataProtection:KeysPath"] = Path.Combine(Path.GetTempPath(), $"dp-test-{Guid.NewGuid():N}"),
                ["DataProtection:KeyProtectionMode"] = "None",
            })
            .Build();

        var act = () =>
        {
            var services = new ServiceCollection()
                .AddLogging()
                .AddSingleton<Microsoft.Extensions.Configuration.IConfiguration>(config);
            LinguaCoach.Infrastructure.DependencyInjection.AddInfrastructure(services, config);
            services.BuildServiceProvider();
        };

        act.Should().NotThrow();
    }

    [Fact]
    public void AddInfrastructure_CertificateMode_MissingCertPath_Throws()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DataProtection:KeysPath"] = Path.Combine(Path.GetTempPath(), $"dp-test-{Guid.NewGuid():N}"),
                ["DataProtection:KeyProtectionMode"] = "Certificate",
                // No CertificatePath, no CertificateThumbprint
            })
            .Build();

        var act = () =>
        {
            var services = new ServiceCollection().AddLogging();
            LinguaCoach.Infrastructure.DependencyInjection.AddInfrastructure(services, config);
        };

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Certificate*");
    }

    [Fact]
    public void AddInfrastructure_CertificateMode_CertFileNotFound_Throws()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DataProtection:KeysPath"] = Path.Combine(Path.GetTempPath(), $"dp-test-{Guid.NewGuid():N}"),
                ["DataProtection:KeyProtectionMode"] = "Certificate",
                ["DataProtection:CertificatePath"] = "/nonexistent/path/dp.pfx",
            })
            .Build();

        var act = () =>
        {
            var services = new ServiceCollection().AddLogging();
            LinguaCoach.Infrastructure.DependencyInjection.AddInfrastructure(services, config);
        };

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*not found*");
    }
}
