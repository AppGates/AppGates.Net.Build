using LibGit2Sharp;
using Nerdbank.GitVersioning;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AppGates.Build.ApiManagement.Tasks
{

    public class UpdateVersionOnApiChange
    {
        private DirectoryInfo ProjectDirectory  { get;  }

        public UpdateVersionOnApiChange(DirectoryInfo projectDirectory,
            FileInfo gitVersionFile = null,
            FileInfo publicApiShippedFile = null,
            FileInfo publicApiUnshippedFile= null, 
            FileInfo packageLockFile = null)
        {
            ProjectDirectory = projectDirectory;
            GitVersionFile = gitVersionFile ?? throw new ArgumentException(nameof(gitVersionFile));
            PublicApiShippedFile = publicApiShippedFile;
            PublicApiUnshippedFile = publicApiUnshippedFile;
            PackageLockFile = packageLockFile;
        }

        private FileInfo GitVersionFile{get;}
        private FileInfo PublicApiShippedFile {get;}
        private FileInfo PublicApiUnshippedFile{get;}
        private FileInfo PackageLockFile{ get;}

        public VersionOptions UpdateVersion(Func<(DirectoryInfo RepositoryDirectory, StatusOptions Options), IEnumerable<StatusEntry>> getStatus)
        {
            var options = VersionFile.GetVersion(
                GitVersionFile.DirectoryName);


            var version = options.Version.Version;
           var newVersion =  this.UpdateVersion2(o=> getStatus((this.ProjectDirectory, o)), version,
                (this.PublicApiShippedFile, true),
                (this.PublicApiUnshippedFile, false),
                (this.PackageLockFile, false));

            options.Version = new SemanticVersion(
                newVersion, options.Version.Prerelease, options.Version.BuildMetadata);

            return options;
        }
        private System.Version UpdateVersion2(Func<StatusOptions, IEnumerable<StatusEntry>> getStatus, System.Version previousVersion, params (FileInfo IndicatorFile, bool IndicatesMajorChange)[] indicators)
        {
            var indicatorFileStatus = getStatus(
              new StatusOptions() { PathSpec = indicators.Select(f => f.IndicatorFile.FullName).ToArray() })
                .Where(x => x.State != FileStatus.Unaltered).ToArray();

            var changed = indicatorFileStatus.Join(indicators, se => se.FilePath, f => f.IndicatorFile.FullName, (e, i) => (Indicator: i, Status: e)).ToArray();



            var major = previousVersion.Major;
            var minor = previousVersion.Minor;
            var build = previousVersion.Build;

            if (changed.Any(m => m.Indicator.IndicatesMajorChange))
            {
                ++major;
                minor = 0;
                build = 0;
            }
            else if (changed.Any())
            {
                ++minor;
                build = 0;
            }
            return new System.Version(major, minor, build);

        }

    }
}
