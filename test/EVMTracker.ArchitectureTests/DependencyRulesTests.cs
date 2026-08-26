using System.Xml.Linq;

namespace EVMTracker.ArchitectureTests;

public sealed class DependencyRulesTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();

    [Fact]
    public void DomainProjectsMustNotReferenceOtherProjects()
    {
        IReadOnlyList<ProjectInfo> domainProjects = LoadProjects()
            .Where(project => project.Name.EndsWith(".Domain", StringComparison.Ordinal))
            .ToArray();

        Assert.NotEmpty(domainProjects);

        foreach (ProjectInfo project in domainProjects)
        {
            Assert.True(
                project.ProjectReferences.Count == 0,
                $"{project.Name} is a Domain project and must not reference: " +
                string.Join(", ", project.ProjectReferences.Select(Path.GetFileNameWithoutExtension)));
        }
    }

    [Fact]
    public void ApplicationProjectsMustNotReferenceInfrastructureOrHosts()
    {
        IReadOnlyList<ProjectInfo> applicationProjects = LoadProjects()
            .Where(project => project.Name.EndsWith(".Application", StringComparison.Ordinal))
            .ToArray();

        Assert.NotEmpty(applicationProjects);

        foreach (ProjectInfo project in applicationProjects)
        {
            string[] forbiddenReferences = project.ProjectReferences
                .Select(Path.GetFileNameWithoutExtension)
                .OfType<string>()
                .Where(reference =>
                    reference.Contains(".Infrastructure", StringComparison.Ordinal) ||
                    reference.EndsWith(".Api", StringComparison.Ordinal) ||
                    reference.EndsWith(".Worker", StringComparison.Ordinal))
                .ToArray();

            Assert.True(
                forbiddenReferences.Length == 0,
                $"{project.Name} has forbidden dependencies: {string.Join(", ", forbiddenReferences)}");
        }
    }

    [Fact]
    public void ServiceProjectsMustNotReferenceAnotherServiceImplementation()
    {
        IReadOnlyList<ProjectInfo> projects = LoadProjects();

        foreach (ProjectInfo project in projects.Where(project => project.ServiceName is not null))
        {
            foreach (string referencePath in project.ProjectReferences)
            {
                string? referencedService = GetServiceName(referencePath);

                Assert.True(
                    referencedService is null ||
                    string.Equals(project.ServiceName, referencedService, StringComparison.Ordinal),
                    $"{project.Name} in service {project.ServiceName} references " +
                    $"{Path.GetFileNameWithoutExtension(referencePath)} in service {referencedService}.");
            }
        }
    }

    [Fact]
    public void EveryHostMustReferenceObservability()
    {
        IReadOnlyList<ProjectInfo> hosts = LoadProjects()
            .Where(project =>
                project.Name.EndsWith(".Api", StringComparison.Ordinal) ||
                project.Name.EndsWith(".Worker", StringComparison.Ordinal))
            .ToArray();

        Assert.Equal(4, hosts.Count);

        foreach (ProjectInfo host in hosts)
        {
            Assert.Contains(
                host.ProjectReferences,
                reference => string.Equals(
                    Path.GetFileNameWithoutExtension(reference),
                    "EVMTracker.Observability",
                    StringComparison.Ordinal));
        }
    }

    private static ProjectInfo[] LoadProjects()
    {
        string sourceRoot = Path.Combine(RepositoryRoot, "src");

        return Directory
            .EnumerateFiles(sourceRoot, "*.csproj", SearchOption.AllDirectories)
            .Select(CreateProjectInfo)
            .ToArray();
    }

    private static ProjectInfo CreateProjectInfo(string projectPath)
    {
        XDocument document = XDocument.Load(projectPath);
        string projectDirectory = Path.GetDirectoryName(projectPath)!;

        string[] references = document
            .Descendants("ProjectReference")
            .Select(element => element.Attribute("Include")?.Value)
            .Where(include => !string.IsNullOrWhiteSpace(include))
            .Select(include => Path.GetFullPath(include!, projectDirectory))
            .ToArray();

        return new ProjectInfo(
            Path.GetFileNameWithoutExtension(projectPath),
            GetServiceName(projectPath),
            references);
    }

    private static string? GetServiceName(string path)
    {
        string relativePath = Path.GetRelativePath(RepositoryRoot, path)
            .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
        string[] segments = relativePath.Split(Path.DirectorySeparatorChar);

        return segments.Length >= 3 &&
               string.Equals(segments[0], "src", StringComparison.OrdinalIgnoreCase) &&
               string.Equals(segments[1], "Services", StringComparison.OrdinalIgnoreCase)
            ? segments[2]
            : null;
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "EVMTracker.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate the EVMTracker repository root.");
    }

    private sealed record ProjectInfo(
        string Name,
        string? ServiceName,
        IReadOnlyList<string> ProjectReferences);
}
