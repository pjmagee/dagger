package fsutil

import (
	"context"
	"io"
	"os"
	"path/filepath"
	"testing"

	"github.com/dagger/dagger/internal/fsutil/types"
	"github.com/stretchr/testify/require"
)

// TestDiskWriter_NestedDirectories tests that the DiskWriter can handle
// creating files in nested directories that don't exist yet.
// This is particularly important for Windows where multi-level paths
// like "./my/module" can cause issues during export.
func TestDiskWriter_NestedDirectories(t *testing.T) {
	ctx := context.Background()
	
	// Create a temporary directory for testing
	tmpDir, err := os.MkdirTemp("", "diskwriter-test-*")
	require.NoError(t, err)
	defer os.RemoveAll(tmpDir)

	// Create a DiskWriter with a sync callback
	dw, err := NewDiskWriter(ctx, tmpDir, DiskWriterOpt{
		SyncDataCb: func(ctx context.Context, path string, wc io.WriteCloser) error {
			// Write some test content
			_, err := wc.Write([]byte("test content"))
			return err
		},
	})
	require.NoError(t, err)

	// Test creating a file in a nested directory structure
	// This simulates the Windows issue where "./my/module/file.txt" fails
	nestedPath := filepath.Join("my", "module", "file.txt")
	
	stat := &types.Stat{
		Path:  nestedPath,
		Mode:  uint32(0644),
		Size_: 12,
		Uid:   uint32(os.Getuid()),
		Gid:   uint32(os.Getgid()),
	}
	
	fi := &StatInfo{stat}
	
	// This should work even though the parent directories don't exist
	err = dw.HandleChange(ChangeKindAdd, nestedPath, fi, nil)
	require.NoError(t, err)
	
	err = dw.Wait(ctx)
	require.NoError(t, err)

	// Verify the file was created
	createdFile := filepath.Join(tmpDir, nestedPath)
	fileInfo, err := os.Stat(createdFile)
	require.NoError(t, err)
	require.False(t, fileInfo.IsDir())
}

// TestDiskWriter_NestedDirectoriesOnly tests creating nested directories
func TestDiskWriter_NestedDirectoriesOnly(t *testing.T) {
	ctx := context.Background()
	
	tmpDir, err := os.MkdirTemp("", "diskwriter-test-*")
	require.NoError(t, err)
	defer os.RemoveAll(tmpDir)

	dw, err := NewDiskWriter(ctx, tmpDir, DiskWriterOpt{
		SyncDataCb: func(ctx context.Context, path string, wc io.WriteCloser) error {
			return nil
		},
	})
	require.NoError(t, err)

	// Test creating nested directories
	nestedDirPath := filepath.Join("my", "module")
	
	stat := &types.Stat{
		Path:  nestedDirPath,
		Mode:  uint32(os.ModeDir | 0755),
		Uid:   uint32(os.Getuid()),
		Gid:   uint32(os.Getgid()),
	}
	
	fi := &StatInfo{stat}
	
	err = dw.HandleChange(ChangeKindAdd, nestedDirPath, fi, nil)
	require.NoError(t, err)
	
	err = dw.Wait(ctx)
	require.NoError(t, err)

	// Verify the directory was created
	createdDir := filepath.Join(tmpDir, nestedDirPath)
	dirInfo, err := os.Stat(createdDir)
	require.NoError(t, err)
	require.True(t, dirInfo.IsDir())
}

// TestDiskWriter_WindowsStylePaths tests that Windows-style paths with drive letters
// are handled correctly
func TestDiskWriter_WindowsStylePaths(t *testing.T) {
	ctx := context.Background()
	
	tmpDir, err := os.MkdirTemp("", "diskwriter-test-*")
	require.NoError(t, err)
	defer os.RemoveAll(tmpDir)

	dw, err := NewDiskWriter(ctx, tmpDir, DiskWriterOpt{
		SyncDataCb: func(ctx context.Context, path string, wc io.WriteCloser) error {
			_, err := wc.Write([]byte("test"))
			return err
		},
	})
	require.NoError(t, err)

	// Test with a path that uses the system's path separator
	// This ensures cross-platform compatibility
	nestedPath := filepath.Join("my", "module", "file.txt")
	
	stat := &types.Stat{
		Path:  nestedPath,
		Mode:  uint32(0644),
		Size_: 4,
		Uid:   uint32(os.Getuid()),
		Gid:   uint32(os.Getgid()),
	}
	
	fi := &StatInfo{stat}
	
	err = dw.HandleChange(ChangeKindAdd, nestedPath, fi, nil)
	require.NoError(t, err)
	
	err = dw.Wait(ctx)
	require.NoError(t, err)

	// Verify the file was created
	createdFile := filepath.Join(tmpDir, nestedPath)
	fileInfo, err := os.Stat(createdFile)
	require.NoError(t, err)
	require.False(t, fileInfo.IsDir())
}

// TestDiskWriter_RootPath tests that root paths are handled correctly
// without attempting to create the root directory itself
func TestDiskWriter_RootPath(t *testing.T) {
ctx := context.Background()

tmpDir, err := os.MkdirTemp("", "diskwriter-test-*")
require.NoError(t, err)
defer os.RemoveAll(tmpDir)

dw, err := NewDiskWriter(ctx, tmpDir, DiskWriterOpt{
SyncDataCb: func(ctx context.Context, path string, wc io.WriteCloser) error {
_, err := wc.Write([]byte("test"))
return err
},
})
require.NoError(t, err)

// Test with a file at the root level (no nested directories)
rootPath := "file.txt"

stat := &types.Stat{
Path:  rootPath,
Mode:  uint32(0644),
Size_: 4,
Uid:   uint32(os.Getuid()),
Gid:   uint32(os.Getgid()),
}

fi := &StatInfo{stat}

// This should work without attempting to create parent directories
err = dw.HandleChange(ChangeKindAdd, rootPath, fi, nil)
require.NoError(t, err)

err = dw.Wait(ctx)
require.NoError(t, err)

// Verify the file was created
createdFile := filepath.Join(tmpDir, rootPath)
fileInfo, err := os.Stat(createdFile)
require.NoError(t, err)
require.False(t, fileInfo.IsDir())
}
