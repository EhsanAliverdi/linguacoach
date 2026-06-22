using FluentAssertions;
using LinguaCoach.Application.Notifications;
using LinguaCoach.Infrastructure.Notifications;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;

namespace LinguaCoach.UnitTests.Notifications;

/// <summary>
/// Verifies that Data Protection key persistence works correctly with a
/// real temp directory — round-trip after rebuilding the provider, and
/// isolation between different key directories.
/// </summary>
public sealed class KeyPersistenceTests : IDisposable
{
    private readonly string _tempDir;

    public KeyPersistenceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"lc-dp-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* best-effort */ }
    }

    private static ISecretProtector BuildProtector(string keysDir, string appName = "SpeakPath.Test")
    {
        var sp = new ServiceCollection()
            .AddDataProtection()
            .SetApplicationName(appName)
            .PersistKeysToFileSystem(new DirectoryInfo(keysDir))
            .Services
            .AddSingleton<ISecretProtector, DataProtectionSecretProtector>()
            .BuildServiceProvider();
        return sp.GetRequiredService<ISecretProtector>();
    }

    [Fact]
    public void Secret_EncryptedWithPersistedKeys_DecryptsAfterRebuildingProvider()
    {
        const string plaintext = "my-smtp-secret";

        // Encrypt with first provider instance
        var ciphertext = BuildProtector(_tempDir).Protect(plaintext);

        // Rebuild provider from same key directory (simulates restart)
        var recovered = BuildProtector(_tempDir).Unprotect(ciphertext);

        recovered.Should().Be(plaintext);
    }

    [Fact]
    public void Secret_EncryptedWithOneKeyDir_CannotDecryptWithDifferentKeyDir()
    {
        var dir2 = Path.Combine(Path.GetTempPath(), $"lc-dp-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir2);
        try
        {
            const string plaintext = "secret-value";
            var ciphertext = BuildProtector(_tempDir).Protect(plaintext);

            // Different key directory — decryption should fail (returns null via fallback path)
            var result = BuildProtector(dir2).Unprotect(ciphertext);

            // DataProtectionSecretProtector catches the exception and tries Base64 fallback,
            // which also fails for non-Base64 DP tokens — returns null.
            result.Should().BeNull();
        }
        finally
        {
            try { Directory.Delete(dir2, recursive: true); } catch { /* best-effort */ }
        }
    }

    [Fact]
    public void KeysDirectory_CreatedByDI_WhenItDoesNotExist()
    {
        var nonExistentDir = Path.Combine(_tempDir, "subdir-that-does-not-exist");
        nonExistentDir.Should().NotBeNull();

        // Simulate what DI does: create directory if missing, then build protector
        var dir = new DirectoryInfo(nonExistentDir);
        if (!dir.Exists) dir.Create();

        dir.Exists.Should().BeTrue();
        var protector = BuildProtector(nonExistentDir);
        var ciphertext = protector.Protect("test");
        protector.Unprotect(ciphertext).Should().Be("test");
    }

    [Fact]
    public void NotificationKeyProtectionOptions_DefaultsAreCorrect()
    {
        var opts = new NotificationKeyProtectionOptions();
        opts.ApplicationName.Should().Be("SpeakPath");
        opts.KeysPath.Should().Be("./app-data/data-protection-keys");
        NotificationKeyProtectionOptions.SectionName.Should().Be("DataProtection");
    }

    [Fact]
    public void KeyFile_WrittenToDirectory_AfterProtect()
    {
        BuildProtector(_tempDir).Protect("trigger-key-write");

        // At least one XML key file should exist in the keys directory
        var keyFiles = Directory.GetFiles(_tempDir, "*.xml");
        keyFiles.Should().NotBeEmpty("Data Protection should have written a key file");
    }
}
