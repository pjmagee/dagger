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

	// Mount the SDK source code
	ctr := base.
		WithDirectory("/sdk", m.SourceDir).
		WithWorkdir("/sdk")

	// Copy introspection.json for code generation
	ctr = ctr.
		WithMountedFile("/schema.json", introspectionJSON)

	// Mount the template files
	ctr = ctr.
		WithMountedDirectory("/opt/template", runtime.Directory("template"))

	// Mount the module source directory
	ctr = ctr.
		WithMountedDirectory(ModSourcePath, ctxDir).
		WithWorkdir(srcPath)

	// Copy SDK to module location
	ctr = ctr.
		WithDirectory(sdkPath, ctr.Directory("/sdk/src"))

	// Initialize module if needed (copy template files)
	// Check if Main.cs exists, if not copy from template
	entries, err := ctr.Directory(srcPath).Entries(ctx)
	if err != nil {
		return nil, err
	}

	// If no Main.cs, initialize from template
	hasMainCs := false
	for _, entry := range entries {
		if entry == "Main.cs" {
			hasMainCs = true
			break
		}
	}

	if !hasMainCs {
		// Copy template Main.cs and rename class
		ctr = ctr.
			WithExec([]string{"sh", "-c", fmt.Sprintf(
				"cp /opt/template/Main.cs . && sed -i 's/DaggerModule/%s/g' Main.cs",
				name,
			)})
	}

	// Copy entrypoint
	ctr = ctr.
		WithDirectory("Entrypoint", runtime.Directory("template/Entrypoint"))

	// Restore and build the module
	ctr = ctr.
		WithExec([]string{"dotnet", "restore"}).
		WithExec([]string{"dotnet", "build", "--no-restore", "-c", "Release"})

	// Set the entrypoint to the compiled executable
	// The entrypoint will be called by Dagger to execute module functions
	ctr = ctr.
		WithEntrypoint([]string{
			"dotnet",
			"run",
			"--project",
			"Entrypoint/Entrypoint.csproj",
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
