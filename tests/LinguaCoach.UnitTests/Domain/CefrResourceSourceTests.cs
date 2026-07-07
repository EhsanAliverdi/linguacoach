using LinguaCoach.Domain.Entities;

namespace LinguaCoach.UnitTests.Domain;

public sealed class CefrResourceSourceTests
{
    [Fact]
    public void Constructor_ValidInput_CreatesUnapprovedSource()
    {
        var source = new CefrResourceSource("CEFR-J", "unknown-pending-review", "https://example.org/cefr-j");

        Assert.Equal("CEFR-J", source.Name);
        Assert.Equal("unknown-pending-review", source.LicenseType);
        Assert.False(source.IsImportApproved);
        Assert.Null(source.ImportedAtUtc);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_EmptyName_Throws(string name)
    {
        Assert.Throws<ArgumentException>(() => new CefrResourceSource(name, "cc-by"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_EmptyLicenseType_Throws(string licenseType)
    {
        Assert.Throws<ArgumentException>(() => new CefrResourceSource("Name", licenseType));
    }

    [Fact]
    public void ApproveForImport_SetsIsImportApprovedTrue()
    {
        var source = new CefrResourceSource("CEFR-J", "cc-by");
        source.ApproveForImport("Verified permissive license.");

        Assert.True(source.IsImportApproved);
        Assert.Equal("Verified permissive license.", source.UsageRestrictionNotes);
    }

    [Fact]
    public void RevokeApproval_SetsIsImportApprovedFalse()
    {
        var source = new CefrResourceSource("CEFR-J", "cc-by");
        source.ApproveForImport();
        source.RevokeApproval("License terms changed.");

        Assert.False(source.IsImportApproved);
        Assert.Equal("License terms changed.", source.UsageRestrictionNotes);
    }

    [Fact]
    public void RevokeApproval_EmptyReason_Throws()
    {
        var source = new CefrResourceSource("CEFR-J", "cc-by");
        source.ApproveForImport();

        Assert.Throws<ArgumentException>(() => source.RevokeApproval(""));
    }

    [Fact]
    public void RecordImport_WithoutApproval_Throws()
    {
        var source = new CefrResourceSource("CEFR-J", "cc-by");

        Assert.Throws<InvalidOperationException>(() => source.RecordImport(DateTimeOffset.UtcNow));
    }

    [Fact]
    public void RecordImport_AfterApproval_SetsImportedAtUtc()
    {
        var source = new CefrResourceSource("CEFR-J", "cc-by");
        source.ApproveForImport();
        var now = DateTimeOffset.UtcNow;
        source.RecordImport(now);

        Assert.Equal(now, source.ImportedAtUtc);
    }
}
