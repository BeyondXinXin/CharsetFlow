using System.Collections.Concurrent;
using CharsetFlow.Models;

namespace CharsetFlow.Services;

internal sealed record ScanOptions(
    FilterMode FilterMode,
    string IncludeRule,
    string ExcludeRule,
    bool Recursive);

internal sealed record ScanProgress(int Completed, int Total, string CurrentFile, int Included);

internal sealed class FileScanner(EncodingDetectionService detector)
{
    public async Task<IReadOnlyList<FileItem>> ScanAsync(
        IEnumerable<string> inputPaths,
        ScanOptions options,
        IReadOnlySet<string> existingPaths,
        IProgress<ScanProgress>? progress,
        CancellationToken cancellationToken)
    {
        HashSet<string> extensions = ParseRule(options.IncludeRule);
        HashSet<string> excludes = ParseRule(options.ExcludeRule);
        List<FileCandidate> candidates = await Task.Run(
            () => EnumerateCandidates(inputPaths, options.Recursive, excludes, existingPaths, cancellationToken),
            cancellationToken);

        if (options.FilterMode == FilterMode.Extensions)
        {
            candidates = candidates.Where(candidate => MatchesExtension(candidate.Path, extensions)).ToList();
        }

        ConcurrentBag<FileItem> included = [];
        int completed = 0;
        await Parallel.ForEachAsync(
            candidates,
            new ParallelOptions
            {
                CancellationToken = cancellationToken,
                MaxDegreeOfParallelism = Math.Clamp(Environment.ProcessorCount / 2, 2, 6)
            },
            async (candidate, token) =>
            {
                FileItem? item = await AnalyzeAsync(candidate, options.FilterMode, token);
                if (item is not null)
                {
                    included.Add(item);
                }

                int done = Interlocked.Increment(ref completed);
                progress?.Report(new ScanProgress(done, candidates.Count, candidate.Path, included.Count));
            });

        return included.OrderBy(item => item.FullPath, StringComparer.CurrentCultureIgnoreCase).ToArray();
    }

    private async Task<FileItem?> AnalyzeAsync(FileCandidate candidate, FilterMode filterMode, CancellationToken token)
    {
        try
        {
            FileInfo info = new(candidate.Path);
            EncodingDetection detection = await detector.DetectFileAsync(candidate.Path, token);
            if (filterMode == FilterMode.Smart && (!detection.IsText || detection.Encoding is null && !detection.IsEmpty))
            {
                return null;
            }

            bool unknown = detection.Encoding is null && !detection.IsEmpty;
            return new FileItem
            {
                FullPath = candidate.Path,
                SourceRoot = candidate.SourceRoot,
                RelativePath = candidate.RelativePath,
                Size = info.Length,
                IsEmpty = detection.IsEmpty,
                SourceEncoding = detection.Encoding,
                EncodingName = detection.IsEmpty ? "空文件" : detection.Encoding?.DisplayName ?? "Unknown",
                ConfidenceText = detection.IsEmpty ? "—" : detection.Encoding is null ? "低" : $"{detection.Confidence:P0}",
                LineEndingName = detection.LineEnding,
                Status = unknown ? FileStatus.Unknown : FileStatus.Ready,
                StatusText = unknown ? "需指定编码" : "就绪"
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return filterMode == FilterMode.Smart
                ? null
                : new FileItem
                {
                    FullPath = candidate.Path,
                    SourceRoot = candidate.SourceRoot,
                    RelativePath = candidate.RelativePath,
                    Size = 0,
                    EncodingName = "Unknown",
                    ConfidenceText = "—",
                    LineEndingName = "—",
                    Status = FileStatus.Failed,
                    StatusText = "读取失败"
                };
        }
    }

    private static List<FileCandidate> EnumerateCandidates(
        IEnumerable<string> inputPaths,
        bool recursive,
        HashSet<string> excludes,
        IReadOnlySet<string> existingPaths,
        CancellationToken token)
    {
        List<FileCandidate> result = [];
        HashSet<string> seen = new(existingPaths, StringComparer.OrdinalIgnoreCase);

        foreach (string rawPath in inputPaths)
        {
            token.ThrowIfCancellationRequested();
            string path;
            try
            {
                path = Path.GetFullPath(rawPath);
            }
            catch
            {
                continue;
            }

            if (File.Exists(path))
            {
                if (seen.Add(path))
                {
                    string root = Path.GetDirectoryName(path) ?? path;
                    result.Add(new FileCandidate(path, root, Path.GetFileName(path)));
                }

                continue;
            }

            if (!Directory.Exists(path))
            {
                continue;
            }

            string sourceRoot = Path.GetDirectoryName(path.TrimEnd(Path.DirectorySeparatorChar)) ?? path;
            Stack<string> pending = new();
            pending.Push(path);
            while (pending.Count > 0)
            {
                token.ThrowIfCancellationRequested();
                string directory = pending.Pop();
                IEnumerable<string> entries;
                try
                {
                    entries = Directory.EnumerateFileSystemEntries(directory);
                }
                catch
                {
                    continue;
                }

                foreach (string entry in entries)
                {
                    token.ThrowIfCancellationRequested();
                    if (Directory.Exists(entry))
                    {
                        if (recursive && !IsExcludedDirectory(entry, excludes))
                        {
                            pending.Push(entry);
                        }

                        continue;
                    }

                    if (File.Exists(entry) && seen.Add(entry))
                    {
                        result.Add(new FileCandidate(entry, sourceRoot, Path.GetRelativePath(sourceRoot, entry)));
                    }
                }
            }
        }

        return result;
    }

    private static HashSet<string> ParseRule(string rule) => rule
        .Split([' ', '|', ';', ',', '\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(value => value.Trim().TrimStart('*').TrimStart('.'))
        .Where(value => value.Length > 0)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static bool MatchesExtension(string path, HashSet<string> extensions)
    {
        if (extensions.Count == 0)
        {
            return true;
        }

        string extension = Path.GetExtension(path).TrimStart('.');
        return extensions.Contains(extension);
    }

    private static bool IsExcludedDirectory(string path, HashSet<string> excludes) =>
        excludes.Contains(Path.GetFileName(path).TrimStart('.')) || excludes.Contains(Path.GetFileName(path));

    private sealed record FileCandidate(string Path, string SourceRoot, string RelativePath);
}
