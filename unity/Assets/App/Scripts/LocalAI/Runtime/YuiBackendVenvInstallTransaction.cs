using System;
using System.IO;

namespace YuiPhysicalAI.LocalAI
{
    // A Python environment is one distribution unit. Merging individual files
    // leaves removed modules and old versioned .dist-info directories behind.
    internal sealed class YuiBackendVenvInstallTransaction
    {
        private readonly string staged;
        private readonly string target;
        private readonly string backup;
        private bool oldMoved;
        private bool newMoved;

        public YuiBackendVenvInstallTransaction(string stagedRoot, string installRoot, string backupRoot)
        {
            staged = Path.Combine(stagedRoot, "backend", ".venv");
            target = Path.Combine(installRoot, "backend", ".venv");
            backup = Path.Combine(backupRoot, "backend", ".venv");
        }

        public void Install(System.Threading.CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            // A source-only bundle must not remove an installed runtime.
            if (!Directory.Exists(staged)) return;
            if (!File.Exists(Path.Combine(staged, "Scripts", "python.exe"))
                && !File.Exists(Path.Combine(staged, "bin", "python")))
                throw new InvalidDataException("Backend bundle has an incomplete Python environment.");
            RejectLink(staged);
            var backendRoot = Path.GetDirectoryName(target);
            RejectLink(Path.GetDirectoryName(backendRoot));
            RejectLink(backendRoot);
            RejectLink(target);
            if (File.Exists(target)) throw new IOException("Backend Python environment is not a directory.");
            RejectLinkedDirectories(staged);
            if (Directory.Exists(target)) RejectLinkedDirectories(target);
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            if (Directory.Exists(target))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(backup));
                Directory.Move(target, backup);
                oldMoved = true;
            }
            cancellationToken.ThrowIfCancellationRequested();
            Directory.Move(staged, target);
            newMoved = true;
        }

        public void Rollback()
        {
            if (newMoved)
            {
                // Return the new tree to staging instead of recursively deleting
                // it while another operation might still be restoring backups.
                Directory.Move(target, staged);
                newMoved = false;
            }
            if (oldMoved)
            {
                Directory.Move(backup, target);
                oldMoved = false;
            }
        }

        private static void RejectLink(string path)
        {
            if ((Directory.Exists(path) || File.Exists(path))
                && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Cannot replace a linked Backend Python environment: " + path);
        }

        private static void RejectLinkedDirectories(string root)
        {
            // Do not traverse junctions/symlinks and later delete their contents
            // when cleaning the old environment's backup.
            foreach (var child in Directory.GetDirectories(root))
            {
                RejectLink(child);
                RejectLinkedDirectories(child);
            }
        }
    }
}
