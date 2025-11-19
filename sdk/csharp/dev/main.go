// A dev module for dagger-csharp-sdk.
//
// This module contains functions for developing the SDK such as, running tests,
// generate introspection, etc.
package main

import (
	"context"

	"csharp-sdk-dev/internal/dagger"
)

func New(
	// C# SDK source.
	//
	// +optional
	// +defaultPath=".."
	// +ignore=["**/*","!src/**/*.cs","!src/**/*.csproj","!src/**/*.sln","!LICENSE","!README.md"]
	source *dagger.Directory,

	// Base container.
	//
	// +optional
	container *dagger.Container,
) *CsharpSdkDev {
	if container == nil {
		container = dag.Container().From("mcr.microsoft.com/dotnet/sdk:9.0")
	}
	path := "/src/sdk/csharp"
	return &CsharpSdkDev{
		Container: container.
			WithDirectory(path, source).
			WithWorkdir(path + "/src"),
	}
}

type CsharpSdkDev struct {
	Container *dagger.Container
}

// Generate code from introspection json file.
func (m *CsharpSdkDev) Generate(introspectionJSON *dagger.File) *dagger.Directory {
	return dag.Directory().WithFile("src/introspection.json", introspectionJSON)
}

// Testing the SDK.
func (m *CsharpSdkDev) Test(ctx context.Context, introspectionJSON *dagger.File) error {
	_, err := m.Container.
		WithFile("introspection.json", introspectionJSON).
		WithExec([]string{"dotnet", "restore"}).
		WithExec([]string{"dotnet", "build", "--no-restore"}).
		WithExec([]string{"dotnet", "test", "--no-build"}, dagger.ContainerWithExecOpts{
			ExperimentalPrivilegedNesting: true,
		}).
		Sync(ctx)
	return err
}

// Lint all C# source files in the SDK.
func (m *CsharpSdkDev) Lint(ctx context.Context) error {
	// Install dotnet format tool and run it
	_, err := m.Container.
		WithExec([]string{"dotnet", "format", "--verify-no-changes"}).
		Sync(ctx)
	return err
}

// Format all C# source files.
func (m *CsharpSdkDev) Format() *dagger.Directory {
	return m.Container.
		WithExec([]string{"dotnet", "format"}).
		Directory("..")
}
