// Toolchain to develop the Dagger C# SDK (experimental).
package main

import (
	"context"
	"fmt"
	"strings"

	"dagger/csharp-client-dev/internal/dagger"
)

const (
	dotnetImage   = "mcr.microsoft.com/dotnet/sdk:10.0"
	workspacePath = "/src"
	sourcePath    = "sdk/csharp"
)

// Develop the Dagger C# SDK (experimental).
type CsharpClientDev struct {
	OriginalWorkspace  *dagger.Directory // +private
	Workspace          *dagger.Directory // +private
	ClientDockerConfig *dagger.Secret    // +private
	Ws                 *dagger.Workspace // +private
}

func New(
	// A directory with all the files needed to develop the SDK.
	// +defaultPath="/"
	// +ignore=["*", "!sdk/csharp/**", "sdk/csharp/**/bin/**", "sdk/csharp/**/obj/**", "sdk/csharp/examples/**"]
	workspaceDir *dagger.Directory,
	// A docker config file with credentials to install on clients.
	// +optional
	clientDockerConfig *dagger.Secret,
	// Workspace forwarded to engine-dev for VCS stamping.
	ws *dagger.Workspace,
) *CsharpClientDev {
	return &CsharpClientDev{
		OriginalWorkspace:  workspaceDir,
		Workspace:          workspaceDir,
		ClientDockerConfig: clientDockerConfig,
		Ws:                 ws,
	}
}

func (m *CsharpClientDev) engine() *dagger.DaggerEngine {
	return dag.DaggerEngine(dagger.DaggerEngineOpts{
		ClientDockerConfig: m.ClientDockerConfig,
		Ws:                 m.Ws,
	})
}

// Return the C# SDK workspace mounted in a .NET SDK container,
// working directory set to the SDK source.
func (m *CsharpClientDev) DevContainer() *dagger.Container {
	return m.engine().InstallClient(
		dag.Container().
			From(dotnetImage).
			WithDirectory(workspacePath, m.Workspace).
			WithWorkdir(workspacePath+"/"+sourcePath+"/src"),
	)
}

// Source returns the source directory for the C# SDK.
func (m *CsharpClientDev) Source() *dagger.Directory {
	return m.Workspace.Directory(sourcePath)
}

// Generate code from the engine introspection JSON.
// +check
func (m *CsharpClientDev) Generate(ctx context.Context) (*dagger.Changeset, error) {
	introspectionJSON := m.engine().IntrospectionJSON()
	ctr := m.DevContainer()
	codegenBin := ctr.
		WithWorkdir(workspacePath + "/" + sourcePath + "/codegen").
		WithExec([]string{"dotnet", "build", "Codegen.csproj", "-c", "Release"}).
		WithExec([]string{"dotnet", "publish", "Codegen.csproj", "-c", "Release", "-o", "/codegen-bin"}).
		Directory("/codegen-bin")
	generatedCS := ctr.
		WithDirectory("/codegen", codegenBin).
		WithFile("/schema.json", introspectionJSON).
		WithExec([]string{
			"dotnet", "/codegen/dagger-codegen.dll",
			"/schema.json", "/generated.cs",
		}).
		File("/generated.cs")
	src := ctr.Directory(workspacePath)
	generated := src.
		WithFile(sourcePath+"/src/introspection.json", introspectionJSON).
		WithFile(sourcePath+"/src/Dagger.SDK/Dagger.SDK.g.cs", generatedCS)
	return generated.Changes(src).Sync(ctx)
}

// Test the C# SDK.
// +check
func (m *CsharpClientDev) Test(ctx context.Context) error {
	_, err := m.DevContainer().
		WithFile("introspection.json", m.engine().IntrospectionJSON()).
		WithExec([]string{"dotnet", "restore"}).
		WithExec([]string{"dotnet", "build", "--no-restore"}).
		WithExec([]string{"dotnet", "test", "--no-build"}, dagger.ContainerWithExecOpts{
			ExperimentalPrivilegedNesting: true,
		}).
		Sync(ctx)
	return err
}

// Lint all C# source files in the SDK using csharpier.
// +check
func (m *CsharpClientDev) Lint(ctx context.Context) error {
	_, err := m.DevContainer().
		WithExec([]string{"dotnet", "tool", "restore"}).
		WithExec([]string{"dotnet", "csharpier", "check", "."}).
		Sync(ctx)
	return err
}

// Format all C# source files using csharpier.
func (m *CsharpClientDev) Format() *dagger.Directory {
	return m.DevContainer().
		WithExec([]string{"dotnet", "tool", "restore"}).
		WithExec([]string{"dotnet", "csharpier", "format", "."}).
		Directory(workspacePath)
}

// Pack the Dagger.SDK into a NuGet package.
func (m *CsharpClientDev) Pack(
	// +optional
	// +default="Release"
	configuration string,
) *dagger.Directory {
	return m.DevContainer().
		WithFile("introspection.json", m.engine().IntrospectionJSON()).
		WithExec([]string{
			"dotnet", "pack",
			"Dagger.SDK/Dagger.SDK.csproj",
			"-c", configuration,
			"-o", "/packages",
		}).
		Directory("/packages")
}

// Publish the Dagger.SDK to NuGet.
func (m *CsharpClientDev) Publish(
	ctx context.Context,

	// +optional
	version string,

	// +optional
	nugetToken *dagger.Secret,

	// +optional
	dryRun bool,
) error {
	// Default the package version to the Dagger engine version with a
	// -preview prerelease suffix (e.g. v0.21.7 -> 0.21.7-preview).
	if version == "" {
		engineVersion, err := dag.Version(ctx)
		if err != nil {
			return fmt.Errorf("resolve engine version: %w", err)
		}
		version = strings.TrimPrefix(engineVersion, "v") + "-preview"
	}

	introspectionJSON := m.engine().IntrospectionJSON()
	ctr := m.DevContainer()

	codegenBinary := ctr.
		WithWorkdir(workspacePath+"/"+sourcePath+"/codegen").
		WithExec([]string{"dotnet", "build", "Codegen.csproj", "-c", "Release"}).
		WithExec([]string{"dotnet", "publish", "Codegen.csproj", "-c", "Release", "-o", "/codegen-bin"}).
		Directory("/codegen-bin")

	generatedCode := ctr.
		WithDirectory("/codegen", codegenBinary).
		WithFile("/schema.json", introspectionJSON).
		WithExec([]string{
			"dotnet", "/codegen/dagger-codegen.dll",
			"/schema.json", "/generated.cs",
		}).
		File("/generated.cs")

	packaged := ctr.
		WithFile("Dagger.SDK/Dagger.SDK.g.cs", generatedCode).
		WithExec([]string{
			"dotnet", "pack",
			"Dagger.SDK/Dagger.SDK.csproj",
			"-c", "Release",
			"-p:Version=" + version,
			"-o", "/packages",
		})

	if dryRun {
		entries, err := packaged.Directory("/packages").Entries(ctx)
		if err != nil {
			return err
		}
		if len(entries) == 0 {
			return fmt.Errorf("no packages were created")
		}
		return nil
	}

	if nugetToken == nil {
		return fmt.Errorf("nuget-token is required for publishing")
	}

	_, err := packaged.
		WithSecretVariable("NUGET_API_KEY", nugetToken).
		WithExec([]string{
			"sh", "-c",
			"dotnet nuget push /packages/*.nupkg --api-key $NUGET_API_KEY --source https://api.nuget.org/v3/index.json",
		}).
		Sync(ctx)

	return err
}
