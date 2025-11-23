// Runtime module for the C# SDK

package main

import (
	"context"
	"fmt"
	"path/filepath"

	"csharp-sdk/internal/dagger"
)

const (
	DotnetImage   = "mcr.microsoft.com/dotnet/sdk:10.0"
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

	// Build the analyzers to get the DLL
	// Using a clean restore and build to ensure NuGet packages are properly resolved
	analyzerDll := base.
		WithDirectory("/analyzer-src", m.SourceDir.Directory("src/Dagger.SDK.Analyzers")).
		WithWorkdir("/analyzer-src").
		// Explicitly restore with verbose output to diagnose any issues
		WithExec([]string{"dotnet", "restore", "--verbosity", "minimal"}).
		// Build in Release mode
		WithExec([]string{"dotnet", "build", "-c", "Release", "--no-restore"}).
		File("/analyzer-src/bin/Release/netstandard2.0/Dagger.SDK.Analyzers.dll")

	// Prepare SDK source with generated code (include all source)
	sdkSource := base.
		WithWorkdir("/sdk-src").
		// Copy all SDK source
		WithDirectory("/sdk-src", m.SourceDir.Directory("src/Dagger.SDK"), dagger.ContainerWithDirectoryOpts{
			Exclude: []string{"bin/", "obj/"},
		}).
		// Add the generated code
		WithFile("Dagger.SDK.g.cs", generatedCode).
		// Add analyzers directory with the built DLL
		WithDirectory("analyzers/dotnet/cs", dag.Directory().WithFile("Dagger.SDK.Analyzers.dll", analyzerDll)).
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
		// Convert module name to valid C# identifier (PascalCase)
		className := toPascalCase(name)
		ctr = ctr.
			WithDirectory(".", runtime.Directory("template")).
			WithExec([]string{"sh", "-c", fmt.Sprintf(
				"sed -i 's/DaggerModule/%s/g' Main.cs",
				className,
			)})
	}

	return ctr, nil
}

// toPascalCase converts a module name to a valid C# PascalCase identifier
// Examples: "my-module" -> "MyModule", "my_module" -> "MyModule", "123test" -> "Test123"
func toPascalCase(name string) string {
	if name == "" {
		return "DaggerModule"
	}

	// Split on hyphens, underscores, and spaces
	var parts []string
	currentPart := ""
	for _, r := range name {
		if r == '-' || r == '_' || r == ' ' {
			if currentPart != "" {
				parts = append(parts, currentPart)
				currentPart = ""
			}
		} else {
			currentPart += string(r)
		}
	}
	if currentPart != "" {
		parts = append(parts, currentPart)
	}

	if len(parts) == 0 {
		return "DaggerModule"
	}

	// Convert each part to title case
	result := ""
	for _, part := range parts {
		if len(part) > 0 {
			// Capitalize first letter, lowercase rest
			firstRune := []rune(part)[0]
			if firstRune >= 'a' && firstRune <= 'z' {
				firstRune = firstRune - 32 // Convert to uppercase
			}
			rest := ""
			if len(part) > 1 {
				rest = part[1:]
			}
			result += string(firstRune) + rest
		}
	}

	// Ensure first character is a letter (remove leading digits)
	for len(result) > 0 {
		firstRune := []rune(result)[0]
		if (firstRune >= 'A' && firstRune <= 'Z') || (firstRune >= 'a' && firstRune <= 'z') {
			break
		}
		if len(result) > 1 {
			result = result[1:]
		} else {
			return "DaggerModule"
		}
	}

	if result == "" {
		return "DaggerModule"
	}

	return result
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
