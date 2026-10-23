using AdNoir.BuildingBlocks.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using Shouldly;
using Xunit;

namespace AdNoir.UnitTests.Configuration;

public sealed class DotEnvConfigurationExtensionsTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "adnoir-dotenv-" + Guid.NewGuid().ToString("N"));

    public DotEnvConfigurationExtensionsTests()
    {
        Directory.CreateDirectory(ProjectPath);
        File.WriteAllText(Path.Combine(_root, "AdNoir.sln"), string.Empty);
        File.WriteAllText(Path.Combine(_root, ".env"), "AdNoir__Identity__ServiceCredentials__sample__ServiceId=sample");
    }

    private string ProjectPath => Path.Join(_root, "src", "Services", "Sample");

    [Fact]
    public void Development_project_loads_credentials_from_solution_root()
    {
        Build(Environments.Development)["AdNoir:Identity:ServiceCredentials:sample:ServiceId"].ShouldBe("sample");
    }

    [Fact]
    public void Project_local_file_takes_precedence()
    {
        File.WriteAllText(Path.Join(ProjectPath, ".env"), "AdNoir__Identity__ServiceCredentials__sample__ServiceId=local");
        Build(Environments.Development)["AdNoir:Identity:ServiceCredentials:sample:ServiceId"].ShouldBe("local");
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public void Non_development_does_not_load_files(string environment)
    {
        Build(environment)["AdNoir:Identity:ServiceCredentials:sample:ServiceId"].ShouldBeNull();
    }

    [Fact]
    public void Explicit_path_is_preserved()
    {
        string path = Path.Join(_root, "custom.env");
        File.WriteAllText(path, "Explicit=true");
        IConfigurationRoot configuration = Build(Environments.Development, path);
        configuration["Explicit"].ShouldBe("true");
        configuration["AdNoir:Identity:ServiceCredentials:sample:ServiceId"].ShouldBeNull();
    }

    [Fact]
    public void Missing_solution_does_not_load_ancestor_file()
    {
        File.Delete(Path.Join(_root, "AdNoir.sln"));
        Build(Environments.Development)["AdNoir:Identity:ServiceCredentials:sample:ServiceId"].ShouldBeNull();
    }

    [Fact]
    public void Nearest_solution_boundary_stops_search_even_without_env_file()
    {
        File.WriteAllText(Path.Join(ProjectPath, "AdNoir.sln"), string.Empty);
        Build(Environments.Development)["AdNoir:Identity:ServiceCredentials:sample:ServiceId"].ShouldBeNull();
    }

    private IConfigurationRoot Build(string environment, string path = ".env")
    {
        IHostEnvironment host = Substitute.For<IHostEnvironment>();
        host.EnvironmentName.Returns(environment);
        host.ContentRootPath.Returns(ProjectPath);
        return new ConfigurationBuilder().AddDotEnvFileForDevelopment(host, path).Build();
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);
}
