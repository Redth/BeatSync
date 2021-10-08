﻿using BeatSyncLib.Playlists;
using Newtonsoft.Json;
using SongFeedReaders.Feeds;
using SongFeedReaders.Feeds.BeatSaver;
using System;

namespace BeatSyncLib.Configs
{
    public class BeatSaverFavoriteMappers : PagedFeedConfigBase
    {
        #region Defaults
        protected override bool DefaultEnabled => true;

        protected override int DefaultMaxSongs => 20;

        protected override bool DefaultCreatePlaylist => true;

        protected override PlaylistStyle DefaultPlaylistStyle => PlaylistStyle.Append;

        protected override BuiltInPlaylist DefaultFeedPlaylist => BuiltInPlaylist.BeatSaverFavoriteMappers;

        protected bool DefaultSeparateMapperPlaylists => false;
        #endregion
        [JsonIgnore]
        private bool? _separateMapperPlaylists;

        public bool SeparateMapperPlaylists
        {
            get
            {
                if (_separateMapperPlaylists == null)
                {
                    _separateMapperPlaylists = DefaultSeparateMapperPlaylists;
                    SetConfigChanged();
                }
                return _separateMapperPlaylists ?? DefaultSeparateMapperPlaylists;
            }
            set
            {
                if (_separateMapperPlaylists == value)
                    return;
                _separateMapperPlaylists = value;
                SetConfigChanged();
            }
        }

        [JsonIgnore]
        public string[]? Mappers { get; set; }

        public override void FillDefaults()
        {
            base.FillDefaults();
            _ = SeparateMapperPlaylists;
            Mappers = Array.Empty<string>();
        }

        public override IFeedSettings ToFeedSettings()
        {
            return new BeatSaverMapperSettings()
            {
                MaxSongs = this.MaxSongs
            };
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="mapperName"></param>
        /// <exception cref="ArgumentNullException"></exception>
        /// <returns></returns>
        public IFeedSettings ToFeedSettings(string mapperName)
        {
            mapperName = mapperName.Trim();
            if (string.IsNullOrEmpty(mapperName))
                throw new ArgumentNullException(nameof(mapperName), "mapperName cannot be a null or empty string.");
            return new BeatSaverMapperSettings()
            {
                MaxSongs = this.MaxSongs,
                StartingPage = this.StartingPage,
                MapperName = mapperName
            };

        }

        public override bool ConfigMatches(ConfigBase other)
        {
            if (other is BeatSaverFavoriteMappers castOther)
            {
                if (!base.ConfigMatches(castOther))
                    return false;
                if (SeparateMapperPlaylists != castOther.SeparateMapperPlaylists)
                    return false;
            }
            else
                return false;
            return true;
        }
    }

    public class BeatSaverLatest : DatedFeedConfigBase
    {
        #region Defaults
        protected override bool DefaultEnabled => false;

        protected override int DefaultMaxSongs => 20;

        protected override bool DefaultCreatePlaylist => true;

        protected override PlaylistStyle DefaultPlaylistStyle => PlaylistStyle.Append;

        protected override BuiltInPlaylist DefaultFeedPlaylist => BuiltInPlaylist.BeatSaverLatest;
        #endregion

        public override IFeedSettings ToFeedSettings()
        {
            return new BeatSaverLatestSettings()
            {
                MaxSongs = this.MaxSongs,
                EndingDate = this.StartBeforeDate ?? DateTime.MaxValue,
                StartingDate = this.StartAfterDate ?? DateTime.MinValue
            };
        }
    }

    //public class BeatSaverHot : FeedConfigBase
    //{
    //    #region Defaults
    //    protected override bool DefaultEnabled => false;

    //    protected override int DefaultMaxSongs => 20;

    //    protected override bool DefaultCreatePlaylist => true;

    //    protected override PlaylistStyle DefaultPlaylistStyle => PlaylistStyle.Append;

    //    protected override BuiltInPlaylist DefaultFeedPlaylist => BuiltInPlaylist.BeatSaverHot;
    //    #endregion

    //    public override IFeedSettings ToFeedSettings()
    //    {
    //        return new BeatSaverFeedSettings((int)BeatSaverFeedName.Hot)
    //        {
    //            MaxSongs = this.MaxSongs,
    //            StartingPage = this.StartingPage
    //        };
    //    }
    //}

    //[Obsolete("Only has really old play data.")]
    //public class BeatSaverPlays : FeedConfigBase
    //{
    //    #region Defaults
    //    protected override bool DefaultEnabled => false;

    //    protected override int DefaultMaxSongs => 20;

    //    protected override bool DefaultCreatePlaylist => true;

    //    protected override PlaylistStyle DefaultPlaylistStyle => PlaylistStyle.Append;

    //    protected override BuiltInPlaylist DefaultFeedPlaylist => BuiltInPlaylist.BeatSaverPlays;
    //    #endregion

    //    public override IFeedSettings ToFeedSettings()
    //    {
    //        return new BeatSaverFeedSettings((int)BeatSaverFeedName.Plays)
    //        {
    //            MaxSongs = this.MaxSongs,
    //            StartingPage = this.StartingPage
    //        };
    //    }
    //}

    //public class BeatSaverDownloads : FeedConfigBase
    //{
    //    #region Defaults
    //    protected override bool DefaultEnabled => false;

    //    protected override int DefaultMaxSongs => 20;

    //    protected override bool DefaultCreatePlaylist => true;

    //    protected override PlaylistStyle DefaultPlaylistStyle => PlaylistStyle.Append;

    //    protected override BuiltInPlaylist DefaultFeedPlaylist => BuiltInPlaylist.BeatSaverDownloads;
    //    #endregion

    //    public override IFeedSettings ToFeedSettings()
    //    {
    //        return new BeatSaverFeedSettings((int)BeatSaverFeedName.Downloads)
    //        {
    //            MaxSongs = this.MaxSongs,
    //            StartingPage = this.StartingPage
    //        };
    //    }
    //}


}
