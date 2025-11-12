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

	// For now, this is a minimal implementation
	// TODO: Implement actual C# codegen
	ctr := base.
		WithDirectory("/sdk", m.SourceDir).
		WithWorkdir("/sdk")

	srcPath := filepath.Join(ModSourcePath, subPath)
	sdkPath := filepath.Join(srcPath, GenPath)
	runtime := dag.CurrentModule().Source()

	ctxDir := modSource.ContextDirectory().
		WithoutDirectory(filepath.Join(subPath, "bin")).
		WithoutDirectory(filepath.Join(subPath, "obj")).
		WithoutDirectory(filepath.Join(subPath, GenPath))

	// Mount the module directory
	ctr = ctr.
		WithMountedDirectory(ModSourcePath, ctxDir).
		WithWorkdir(srcPath).
		WithEntrypoint([]string{"dotnet", "run"})

	// TODO: Add codegen and restore logic
	_ = name
	_ = sdkPath
	_ = runtime
	_ = introspectionJSON

	return ctr, nil
}

func (m *CsharpSdk) ModuleRuntime(
	ctx context.Context,
	modSource *dagger.ModuleSource,
	introspectionJSON *dagger.File,
) (*dagger.Container, error) {
	return m.CodegenBase(ctx, modSource, introspectionJSON)
}
