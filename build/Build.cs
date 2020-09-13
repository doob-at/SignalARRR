using System;
using System.Linq;
using System.Text.Json;
using Nuke.Common;
using Nuke.Common.CI;
using Nuke.Common.CI.AzurePipelines;
using Nuke.Common.CI.GitHubActions;
using Nuke.Common.Execution;
using Nuke.Common.Git;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Tools.Git;
using Nuke.Common.Tools.GitVersion;
using Nuke.Common.Tools.NuGet;
using Nuke.Common.Utilities.Collections;
using static Nuke.Common.EnvironmentInfo;
using static Nuke.Common.IO.FileSystemTasks;
using static Nuke.Common.IO.PathConstruction;
using static Nuke.Common.Tools.DotNet.DotNetTasks;

[CheckBuildProjectConfigurations]
[UnsetVisualStudioEnvironmentVariables]


[AzurePipelines(
    "Test",
    AzurePipelinesImage.WindowsLatest,
    InvokedTargets = new []{ nameof(PushAzure)},
    TriggerBranchesInclude = new []{ "dev"},
    ImportSystemAccessTokenAs = nameof(AccessToken),
    NonEntryTargets = new []{ nameof(Clean), nameof(Restore), nameof(Compile), nameof(Pack)}
)]


class Build : NukeBuild {
    /// Support plugins are available for:
    ///   - JetBrains ReSharper        https://nuke.build/resharper
    ///   - JetBrains Rider            https://nuke.build/rider
    ///   - Microsoft VisualStudio     https://nuke.build/visualstudio
    ///   - Microsoft VSCode           https://nuke.build/vscode

    public static int Main() => Execute<Build>(x => x.Pack);

    //[Parameter("Configuration to build - Default is 'Debug' (local) or 'Release' (server)")]
    //readonly Configuration Configuration = IsLocalBuild ? Configuration.Debug : Configuration.Release;
    

    [Solution] readonly Solution Solution;
    [GitRepository] readonly GitRepository GitRepository;
    [GitVersion] GitVersion GitVersion;


    [Parameter("Access Token")]
    public string AccessToken { get; set; }

    AbsolutePath SourceDirectory => RootDirectory; // / "source";
    AbsolutePath TestsDirectory => RootDirectory / "tests";
    AbsolutePath OutputDirectory => RootDirectory / "output";
    
    Target Clean => _ => _
        .Before(Restore)
        .Executes(() => {
            SourceDirectory.GlobDirectories("**/bin", "**/obj").ForEach(DeleteDirectory);
            TestsDirectory.GlobDirectories("**/bin", "**/obj").ForEach(DeleteDirectory);
            EnsureCleanDirectory(OutputDirectory);
        });

    Target Restore => _ => _
        .Executes(() => {
            DotNetRestore(s => s
                .SetProjectFile(Solution));
        });

    Target Compile => _ => _
        .DependsOn(Restore)
        .Executes(() => {

            var libraries = Solution.AllProjects.Where(p => p.Name.StartsWith("SignalARRR", StringComparison.OrdinalIgnoreCase));

            foreach (var library in libraries) {
                var frameworks = library.GetTargetFrameworks().Where(fr => fr.StartsWith("netstandard") || fr.StartsWith("netcoreapp"));

                if (Platform == PlatformFamily.Windows) {

                    DotNetBuild(s => s
                        .SetConfiguration("release")
                        .SetAssemblyVersion(GitVersion.AssemblySemVer)
                        .SetFileVersion(GitVersion.AssemblySemFileVer)
                        .SetInformationalVersion(GitVersion.InformationalVersion)
                        .EnableNoRestore()
                        .SetProjectFile(library)
                    );

                } else {
                    foreach (var framework in frameworks) {
                        DotNetBuild(s => s
                            .SetConfiguration("release")
                            .SetAssemblyVersion(GitVersion.AssemblySemVer)
                            .SetFileVersion(GitVersion.AssemblySemFileVer)
                            .SetInformationalVersion(GitVersion.InformationalVersion)
                            .EnableNoRestore()
                            .SetProjectFile(library)
                            .SetFramework(framework)
                        );
                    }
                }
            }

        });

    Target Pack => _ => _
        .DependsOn(Restore)
        .Executes(() => {

            var libraries = Solution.AllProjects.Where(p => p.Name.StartsWith("SignalARRR", StringComparison.OrdinalIgnoreCase));

            DotNetPack(s => s
                .SetVersion(GitVersion.NuGetVersionV2)
                .SetConfiguration("release")
                .SetAssemblyVersion(GitVersion.AssemblySemVer)
                .SetFileVersion(GitVersion.AssemblySemFileVer)
                .SetInformationalVersion(GitVersion.InformationalVersion)
                .EnableNoRestore()
                .SetOutputDirectory(OutputDirectory)
                .CombineWith(libraries, (settings, project) => settings.SetProject(project))
            );
        });

    
    Target PushAzure => _ => _
        .DependsOn(Pack)
        .Executes(() => {

            DotNet(
                $"nuget add source https://windischb.pkgs.visualstudio.com/SignalARRR/_packaging/testing/nuget/v3/index.json -n azure -u windischb -p {AccessToken} --store-password-in-clear-text");

            GlobFiles(OutputDirectory, "*.nupkg")
                .NotEmpty()
                .Where(x => !x.EndsWith("symbols.nupkg"))
                .ForEach(x => {
                    DotNetNuGetPush(s => s
                        .SetTargetPath(x)
                        .SetSource("azure")
                        .SetApiKey(AccessToken)
                    );
                });
        });

}
