using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Nerdbank.GitVersioning;
using System.IO;
using System.Runtime.InteropServices.ComTypes;

namespace AppGates.Build.ApiManagement.Tasks
{
    public class UpdateVersionOnApiChangeTask: Task
    {
        private DirectoryInfo ProjectDirectory => new DirectoryInfo(this.ProjectDirectoryPath);
        private FileInfo GitVersionFile => new FileInfo(GitVersionFilePath);
        private FileInfo PublicApiShippedFile => new FileInfo(this.PublicApiShippedFilePath);
        private FileInfo PublicApiUnshippedFile => new FileInfo(this.PublicApiUnshippedFilePath);
        private FileInfo PackageLockFile => new FileInfo(this.PackageLockFilePath);
        [Required]
        public string ProjectDirectoryPath { get; set; }

        [Required]
        public string GitVersionFilePath { get; set; }

        [Required]
        public string PublicApiShippedFilePath { get; set; }

        [Required]
        public string PublicApiUnshippedFilePath { get; set; }

        [Required]
        public string PackageLockFilePath { get; set; }


        public override bool Execute()
        {

        
            return true;
        }

        private void DoExecute()
        {
            var updater = new UpdateVersionOnApiChange(this.ProjectDirectoryPath.ToDirectoryInfo(), 
                this.GitVersionFile, this.PublicApiShippedFile,
                this.PublicApiUnshippedFile, this.PackageLockFile);


            var newVersion = updater.UpdateVersion(
                x => GitExtensions.OpenGitRepo(x.RepositoryDirectory.FullName).RetrieveStatus(x.Options));

            VersionFile.SetVersion(this.ProjectDirectory.FullName, newVersion);

        }

    }
}
