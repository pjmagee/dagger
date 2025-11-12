// Runtime module for the C# SDK

package main

import (
	"context"
	"fmt"
	"path/filepath"

	"csharp-sdk/internal/dagger"
)

const (
	DotnetImage   = "mcr.microsoft.com/dotnet/sdk:9.0"
	ModSourcePath = "/src"
	GenPath       = "sdk"
)

type CsharpSdk struct {
	SourceDir *dagger.Directory
}

func New(
	// Directory with the C# SDK source code.
	// +optional
	// +defaultPath="/sdk/csharp"
	// +ignore=["**", "!src/", "!LICENSE", "!README.md"]
	sdkSourceDir *dagger.Directory,
) (*CsharpSdk, error) {
	if sdkSourceDir == nil {
		return nil, fmt.Errorf("sdk source directory not provided")
	}
	return &CsharpSdk{
		SourceDir: sdkSourceDir,
	}, nil
}

func (m *CsharpSdk) Codegen(
	ctx context.Context,
	modSource *dagger.ModuleSource,
	introspectionJSON *dagger.File,
) (*dagger.GeneratedCode, error) {
	ctr, err := m.CodegenBase(ctx, modSource, introspectionJSON)
	if err != nil {
		return nil, err
	}
	return dag.
		GeneratedCode(ctr.Directory(ModSourcePath)).
		WithVCSGeneratedPaths([]string{
			GenPath + "/**",
		}).
		WithVCSIgnoredPaths([]string{GenPath, "bin", "obj"}), nil
}

func (m *CsharpSdk) CodegenBase(
	ctx context.Context,
	modSource *dagger.ModuleSource,
	introspectionJSON *dagger.File,
) (*dagger.Container, error) {
	name, err := modSource.ModuleOriginalName(ctx)
	if err != nil {
		return nil, fmt.Errorf("could not load module name: %w", err)
	}

	subPath, err := modSource.SourceSubpath(ctx)
	if err != nil {
		return nil, fmt.Errorf("could not load module source path: %w", err)
	}

	base := dag.Container().
		From(DotnetImage).
		WithExec([]string{"apt-get", "update"}).
		WithExec([]string{"apt-get", "install", "-y", "git"})

	srcPath := filepath.Join(ModSourcePath, subPath)
	sdkPath := filepath.Join(srcPath, GenPath)
	runtime := dag.CurrentModule().Source()

	ctxDir := modSource.ContextDirectory().
		WithoutDirectory(filepath.Join(subPath, "bin")).
		WithoutDirectory(filepath.Join(subPath, "obj")).
		WithoutDirectory(filepath.Join(subPath, GenPath))

	// Build the SDK with introspection
	ctr := base.
		WithDirectory("/sdk-src", m.SourceDir).
		WithMountedFile("/schema.json", introspectionJSON).
		WithWorkdir("/sdk-src/src").
		WithExec([]string{"dotnet", "build", "-c", "Release"})

	// Get the built SDK DLLs
	sdkBuild := ctr.Directory("/sdk-src/src/Dagger.SDK/bin/Release/net9.0")

	// Mount the module source directory
	ctr = base.
		WithMountedDirectory(ModSourcePath, ctxDir).
		WithWorkdir(srcPath)

	// Copy SDK DLLs to module's sdk folder
	ctr = ctr.
		WithDirectory(sdkPath, sdkBuild)

	// Initialize module if needed (copy template files)
	entries, err := ctr.Directory(srcPath).Entries(ctx)
	if err != nil {
		return nil, err
	}

	// Check if this is a new module (no Main.cs or Program.cs)
	hasMainCs := false
	hasProgramCs := false
	hasProjectFile := false
	for _, entry := range entries {
		if entry == "Main.cs" {
			hasMainCs = true
		}
		if entry == "Program.cs" {
			hasProgramCs = true
		}
		if entry == "DaggerModule.csproj" || entry == name+".csproj" {
			hasProjectFile = true
		}
	}

	// If this is a new module, copy template files
	if !hasMainCs && !hasProgramCs && !hasProjectFile {
		ctr = ctr.
			WithFile("Main.cs", runtime.File("template/Main.cs")).
			WithFile("Program.cs", runtime.File("template/Program.cs")).
			WithFile("DaggerModule.csproj", runtime.File("template/DaggerModule.csproj")).
			WithExec([]string{"sh", "-c", fmt.Sprintf(
				"sed -i 's/DaggerModule/%s/g' Main.cs",
				name,
			)})
	}

	// Build the user's module
	ctr = ctr.
		WithExec([]string{"dotnet", "build", "-c", "Release"})

	// Set the entrypoint to run the user's compiled program
	// The Program.cs bootstraps the ModuleRuntime from the SDK
	ctr = ctr.
		WithEntrypoint([]string{
			"dotnet",
			"run",
			"--no-build",
			"-c",
			"Release",
			"--",
		})

	return ctr, nil
}

func (m *CsharpSdk) ModuleRuntime(
	ctx context.Context,
	modSource *dagger.ModuleSource,
	introspectionJSON *dagger.File,
) (*dagger.Container, error) {
	return m.CodegenBase(ctx, modSource, introspectionJSON)
}
