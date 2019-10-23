using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Stl.IO;

namespace Stl.CommandLine.Git
{
    public class GitFetcher
    {
        public bool AlwaysFetch { get; set; } = false;
        public string SourceUrl { get; set; }
        public string SourceRevision { get; set; }
        public PathString TargetPath { get; set; }
        public Func<GitCmd> GitCmdFactory { get; set; }

        public GitFetcher(string sourceUrl, string sourceRevision, PathString targetPath, 
            Func<GitCmd>? gitCmdFactory = null)
        {
            SourceUrl = sourceUrl;
            SourceRevision = sourceRevision;
            TargetPath = targetPath;
            GitCmdFactory = gitCmdFactory ?? (() => new GitCmd());
        }

        public async Task FetchAsync(CancellationToken cancellationToken = default)
        {
            // First we make sure we aren't doing this concurrently with some other
            // GitFetcher (prob. running in a different process)
            var fileLock = new FileLock(TargetPath + ".lock");
            await using var _ = await fileLock.AcquireAsync(cancellationToken)
                .ConfigureAwait(false);
            
            var git = GitCmdFactory.Invoke();
            git.ResultChecks = CmdResultChecks.NonZeroExitCode;

            var gitFolder = TargetPath & ".git";
            if (!Directory.Exists(gitFolder)) {
                if (!Directory.Exists(TargetPath))
                    Directory.CreateDirectory(TargetPath);
                git.WorkingDirectory = ""; // It has to be non-target path on cloning
                await git
                    .RunAsync("clone" + CliString.Quote(SourceUrl) + CliString.Quote(TargetPath), cancellationToken)
                    .ConfigureAwait(false);
                git.WorkingDirectory = TargetPath;
                await git
                    .RunAsync("checkout" + new CliString(SourceRevision), cancellationToken)
                    .ConfigureAwait(false);
            } 
            else {
                git.WorkingDirectory = TargetPath;
                var mustFetch = AlwaysFetch;
                if (!AlwaysFetch) {
                    using (git.ChangeResultChecks(0)) {
                        var r = await git
                            .RunAsync("rev-parse --short=10 HEAD", cancellationToken)
                            .ConfigureAwait(false);
                        mustFetch |= r.ExitCode != 0;
                        mustFetch |= r.StandardOutput.Trim() != SourceRevision;
                    }
                }
                if (mustFetch) {
                    await git
                        .RunAsync("reset --hard HEAD", cancellationToken)
                        .ConfigureAwait(false);
                    await git
                        .RunAsync("fetch origin", cancellationToken)
                        .ConfigureAwait(false);
                    await git
                        .RunAsync("checkout" + new CliString(SourceRevision), cancellationToken)
                        .ConfigureAwait(false);
                }
            }
        }
    }
}
