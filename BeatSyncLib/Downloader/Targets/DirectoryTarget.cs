﻿using BeatSyncLib.Hashing;
using BeatSyncLib.History;
using BeatSaberPlaylistsLib;
using BeatSyncLib.Utilities;
using SongFeedReaders.Models;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using BeatSaber.SongHashing;

namespace BeatSyncLib.Downloader.Targets
{
    public class DirectoryTarget : SongTarget, ITargetWithHistory, ITargetWithPlaylists
    {
        public static Hasher Hasher = new Hasher();
        public override string TargetName => nameof(DirectoryTarget);
        public ISongHashCollection SongHasher { get; protected set; }
        public HistoryManager? HistoryManager { get; protected set; }
        public PlaylistManager? PlaylistManager { get; protected set; }
        public string SongsDirectory { get; protected set; }
        public bool OverwriteTarget { get; protected set; }
        public bool UnzipBeatmaps { get; protected set; } = true;


        public DirectoryTarget(string songsDirectory, bool overwriteTarget, bool unzipBeatmaps, ISongHashCollection songHasher, HistoryManager? historyManager, PlaylistManager? playlistManager)
            : base()
        {
            SongsDirectory = Path.GetFullPath(songsDirectory);
            SongHasher = songHasher;
            HistoryManager = historyManager;
            PlaylistManager = playlistManager;
            OverwriteTarget = overwriteTarget;
            UnzipBeatmaps = unzipBeatmaps;
        }

        public override Task<SongState> CheckSongExistsAsync(string songHash)
        {
            SongState state = SongState.Wanted;

            if (SongHasher != null)
            {
                if (SongHasher.HashingState == HashingState.NotStarted)
                    Logger.log?.Warn($"SongHasher hasn't hashed any songs yet.");
                else if (SongHasher.HashingState == HashingState.InProgress)
                    Logger.log?.Warn($"SongHasher hasn't finished hashing.");
                //await SongHasher.InitializeAsync().ConfigureAwait(false);
                if (SongHasher.HashExists(songHash))
                    state = SongState.Exists;
            }
            if (state == SongState.Wanted && HistoryManager != null)
            {
                if (!HistoryManager.IsInitialized)
                    HistoryManager.Initialize();
                if (HistoryManager.TryGetValue(songHash, out HistoryEntry entry))
                {
                    if (!entry.AllowRetry)
                    {
                        state = SongState.NotWanted;
                        entry.Flag = HistoryFlag.Deleted;
                    }
                }
            }
            return Task.FromResult(state);
        }

        public override async Task<TargetResult> TransferAsync(ISong song, Stream sourceStream, CancellationToken cancellationToken)
        {
            if (song == null)
                throw new ArgumentNullException(nameof(song), "Song cannot be null for TransferAsync.");
            string? directoryPath = null;
            ZipExtractResult? zipResult = null;
            string directoryName;
            if (song.Name != null && song.LevelAuthorName != null)
                directoryName = Util.GetSongDirectoryName(song.Key, song.Name, song.LevelAuthorName);
            else if (song.Key != null)
                directoryName = song.Key;
            else
                directoryName = song.Hash ?? Path.GetRandomFileName();
            try
            {
                if (cancellationToken.IsCancellationRequested)
                    return new TargetResult(this, SongState.Wanted, false, new OperationCanceledException());
                directoryPath = Path.Combine(SongsDirectory, directoryName);
                if (UnzipBeatmaps)
                {
                    if (!Directory.Exists(SongsDirectory))
                        throw new SongTargetTransferException($"Parent directory doesn't exist: '{SongsDirectory}'");
                    zipResult = await Task.Run(() => FileIO.ExtractZip(sourceStream, directoryPath, OverwriteTarget)).ConfigureAwait(false);
                    if (!string.IsNullOrEmpty(song.Hash) && zipResult.OutputDirectory != null)
                    {
                        var hashResult = await Task.Run(() => Hasher.HashDirectory(zipResult.OutputDirectory, cancellationToken)).ConfigureAwait(false);
                        if (hashResult.ResultType == HashResultType.Error)
                            Logger.log?.Warn($"Unable to get hash for '{song}': {hashResult.Message}.");
                        else if (hashResult.ResultType == HashResultType.Warn)
                            Logger.log?.Warn($"Hash warning for '{song}': {hashResult.Message}.");
                        else
                        {
                            string? hashAfterDownload = hashResult.Hash;
                            if (hashAfterDownload != song.Hash)
                                throw new SongTargetTransferException($"Extracted song hash doesn't match expected hash: {song.Hash} != {hashAfterDownload}");
                        }
                    }
                    TargetResult = new DirectoryTargetResult(this, SongState.Wanted, zipResult.ResultStatus == ZipExtractResultStatus.Success, zipResult, zipResult.Exception);
                }
                else
                {
                    string zipDestination = directoryPath + ".zip";
                    using FileStream fs = File.Open(zipDestination, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
                    try
                    {
                        await sourceStream.CopyToAsync(fs, 81920, cancellationToken).ConfigureAwait(false);
                        TargetResult = new DirectoryTargetResult(this, SongState.Wanted, true, null);
                    }
                    catch (OperationCanceledException ex)
                    {
                        TargetResult = new DirectoryTargetResult(this, SongState.Wanted, false, ex);
                    }
#pragma warning disable CA1031 // Do not catch general exception types
                    catch (Exception ex)
                    {
                        TargetResult = new DirectoryTargetResult(this, SongState.Wanted, false, ex);
                    }
#pragma warning restore CA1031 // Do not catch general exception types
                    return TargetResult;
                }
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception ex)
            {
                TargetResult = new DirectoryTargetResult(this, SongState.Wanted, false, zipResult, ex);
                return TargetResult;
            }
#pragma warning restore CA1031 // Do not catch general exception types

            return TargetResult;
        }
    }

    public class DirectoryTargetResult : TargetResult
    {
        public ZipExtractResult? ZipExtractResult { get; protected set; }
        public DirectoryTargetResult(SongTarget target, SongState songState, bool success, Exception? exception)
            : base(target, songState, success, exception)
        { }

        public DirectoryTargetResult(SongTarget target, SongState songState, bool success, ZipExtractResult? zipExtractResult, Exception? exception)
            : base(target, songState, success, exception)
        {
            ZipExtractResult = zipExtractResult;
        }
    }
}
