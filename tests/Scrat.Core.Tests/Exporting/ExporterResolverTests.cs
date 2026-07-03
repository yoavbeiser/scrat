using NSubstitute;
using Scrat.Core.Abstractions;
using Scrat.Core.Exporting;
using Scrat.Core.Models;

namespace Scrat.Core.Tests.Exporting;

public class ExporterResolverTests
{
    [Fact]
    public void Resolves_exporters_by_type()
    {
        var smb = Substitute.For<IExporter>();
        smb.Type.Returns(ExporterType.Smb);
        var ftp = Substitute.For<IExporter>();
        ftp.Type.Returns(ExporterType.Ftp);

        var factory = new ExporterResolver([smb, ftp]);

        Assert.Same(smb, factory.Resolve(ExporterType.Smb));
        Assert.Same(ftp, factory.Resolve(ExporterType.Ftp));
    }

    [Fact]
    public void Unregistered_type_throws()
    {
        var factory = new ExporterResolver([]);

        Assert.Throws<NotSupportedException>(() => factory.Resolve(ExporterType.Ftp));
    }
}

public class ExportPathTests
{
    [Theory]
    [InlineData("plain.txt", "plain.txt")]
    [InlineData("2024-06-01/report", "2024-06-01_report")]
    [InlineData("a/b\\c:d*e", "a_b_c_d_e")]
    public void Flattens_keys_into_safe_file_names(string key, string expected)
    {
        Assert.Equal(expected, ExportPath.ToFileName(key));
    }

    [Fact]
    public void Blank_key_throws()
    {
        Assert.Throws<ArgumentException>(() => ExportPath.ToFileName("  "));
    }
}
