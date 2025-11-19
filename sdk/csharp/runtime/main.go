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
	// +ignore=["**", "!src/", "!codegen/", "!LICENSE", "!README.md"]
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

// CodegenBase prepares the base container with SDK and template files (no build)
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

	// Build the standalone codegen CLI tool
	codegenBinary := base.
		WithDirectory("/codegen-src", m.SourceDir).
		WithWorkdir("/codegen-src/codegen").
		WithExec([]string{"dotnet", "build", "-c", "Release"}).
		WithExec([]string{"dotnet", "publish", "-c", "Release", "-o", "/codegen-bin"}).
		Directory("/codegen-bin")

	// Generate Dagger.SDK.g.cs using the codegen tool
	generatedCode := base.
		WithDirectory("/codegen", codegenBinary).
		WithFile("/schema.json", introspectionJSON).
		WithExec([]string{
			"dotnet", "/codegen/dagger-codegen.dll",
			"/schema.json", "/generated.cs",
		}).
		File("/generated.cs")

	// Prepare SDK source with generated code (include all source)
	sdkSource := base.
		WithWorkdir("/sdk-src").
		// Copy all SDK source
		WithDirectory("/sdk-src", m.SourceDir.Directory("src/Dagger.SDK"), dagger.ContainerWithDirectoryOpts{
			Exclude: []string{"bin/", "obj/"},
		}).
		// Add the generated code
		WithFile("Dagger.SDK.g.cs", generatedCode).
		// Return the SDK source directory
		Directory("/sdk-src")

	// Mount the module source directory
	ctr := base.
		WithMountedDirectory(ModSourcePath, ctxDir).
		WithWorkdir(srcPath).
		// Copy SDK with generated code
		WithDirectory(sdkPath, sdkSource)

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

	return ctr, nil
}

func (m *CsharpSdk) ModuleRuntime(
	ctx context.Context,
	modSource *dagger.ModuleSource,
	introspectionJSON *dagger.File,
) (*dagger.Container, error) {
	ctr, err := m.CodegenBase(ctx, modSource, introspectionJSON)
	if err != nil {
		return nil, err
	}

	subPath, err := modSource.SourceSubpath(ctx)
	if err != nil {
		return nil, fmt.Errorf("could not load module source path: %w", err)
	}

	srcPath := filepath.Join(ModSourcePath, subPath)
	projectFile := filepath.Join(srcPath, "DaggerModule.csproj")

	// Build the module with specific project file
	ctr = ctr.WithExec([]string{"dotnet", "build", projectFile, "-c", "Release"})

	// Set the entrypoint to run the compiled program
	ctr = ctr.WithEntrypoint([]string{
		"dotnet", "run", "--project", projectFile, "--no-build", "-c", "Release", "--",
	})

	return ctr, nil
}
