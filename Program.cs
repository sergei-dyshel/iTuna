using ATL;
using DotMake.CommandLine;
using iTunesLib;

Cli.Run<RootCliCommand>(args);

[CliCommand(
    Description = "iTunes helper utility",
    NameCasingConvention = CliNameCasingConvention.KebabCase,
    NamePrefixConvention = CliNamePrefixConvention.DoubleHyphen,
    ShortFormPrefixConvention = CliNamePrefixConvention.SingleHyphen
)]
class RootCliCommand
{

    [CliOption(Alias = "-s", Description = "Process only selected tracks (by default process all)")]
    public bool selectedOption { get; set; }

    [CliOption(Alias = "-n", Description = "Do not do any actual changes, just print actions")]
    public bool dryRunOption { get; set; }

    [CliOption(Alias = "-u", Description = "Force update iTunes metadata from file tag")]
    public bool updateOption { get; set; }

    [CliOption(Alias = "-d", Description = "Dump tag info")]
    public bool dumpOption { get; set; }

    [CliOption(Description = "Sync song rating with iTunes")]
    public bool ratingOption { get; set; }

    [CliOption(Description = "Sync song tags with iTunes")]
    public bool tagsOption { get; set; }

    [CliOption(Alias = "-p", Description = "Add changed songs to playlist", Required = false)]
    public string? playlistOption { get; set; }

    const string NO_RATING = "☆☆☆☆☆";

    private string ratingToStar(int rating)
    {
        return (rating switch
        {
            < 10 => NO_RATING,
            < 30 => "★",
            < 50 => "★★",
            < 70 => "★★★",
            < 90 => "★★★★",
            <= 100 => "★★★★★",
            _ => throw new ArgumentOutOfRangeException(nameof(rating), "Rating must be between 0 and 100")
        });
    }

    private string ratingToStar(float? rating)
    {
        int intRating = (int)((rating ?? 0) * 100);
        return ratingToStar(intRating);
    }

    private int convertRating(float rating)
    {
        return (int)(rating * 100);
    }

    private float convertRating(int rating)
    {
        return rating / 100.0f;
    }

    private float getITunesRating(IITFileOrCDTrack track)
    {
        // do not take album rating into account
        return track.ratingKind == ITRatingKind.ITRatingKindUser ? convertRating(track.Rating) : 0.0f;
    }

    private IITUserPlaylist CreatePlaylist(iTunesApp app)
    {
        foreach (IITSource source in app.Sources)
        {
            if (source.Kind == ITSourceKind.ITSourceKindLibrary)
            {
                foreach (IITPlaylist playlist in source.Playlists)
                {
                    if (playlist.Name == playlistOption)
                    {
                        Console.WriteLine($"Using existing playlist {playlistOption}");
                        return (IITUserPlaylist)playlist;
                    }
                }
                Console.WriteLine($"Creating playlist {playlistOption}");
                return (IITUserPlaylist)app.CreatePlaylistInSource(playlistOption, source);
            }
        }
        throw new Exception("Could not find library source");
    }

    private float GetTagRating(Track track)
    {
        if (track.AudioFormat.ShortName == "MPEG-4")
        {
            string value;
            if (track.AdditionalFields.TryGetValue("RATING", out value))
                return (float)Double.Parse(value) / 5.0f;
            return 0.0f;
        }
        else if (track.AudioFormat.ShortName == "MPEG") // mp3 
        {
            return track.Popularity ?? 0.0f;
        }
        else throw new Exception($"Unsupported file format: {track.Path}");
    }

    private void SetTagRating(Track track, float rating)
    {
        if (track.AudioFormat.ShortName == "MPEG-4")
        {
            track.AdditionalFields["RATING"] = (convertRating(rating) / 20).ToString();
        }
        else if (track.AudioFormat.ShortName == "MPEG") // mp3 
        {
            track.Popularity = rating;
        }
        else
        {
            throw new Exception($"Unsupported file format: {track.Path}");
        }
    }

    public void Run()
    {
        iTunesApp app = new iTunesApp();

        var library = app.LibraryPlaylist;

        var tracks = selectedOption ? app.SelectedTracks : library.Tracks;

        Console.WriteLine($"Processing {tracks.Count} tracks");

        IITUserPlaylist playlist = null;

        if (playlistOption != null)
        {
            playlist = CreatePlaylist(app);
        }

        foreach (IITTrack anyTrack in tracks)
        {
            if (anyTrack.Kind != ITTrackKind.ITTrackKindFile)
                continue;

            var track = (anyTrack as IITFileOrCDTrack)!;

            Track tag = new(track.Location);
            bool tagChanged = false;
            float tagRating = GetTagRating(tag);

            if (dumpOption)
            {
                Console.WriteLine($"File path: {track.Location}");
                Console.WriteLine($"Audio format: {tag.AudioFormat.Name} ({tag.AudioFormat.ShortName}");
                Console.WriteLine($"Rating: {ratingToStar(tagRating)}");
                foreach (Format format in tag.MetadataFormats)
                {
                    Console.WriteLine($"Metadata format: {format.Name} (short: {format.ShortName})");
                }
                foreach (var pair in tag.AdditionalFields)
                {
                    Console.WriteLine($"[{pair.Key}]: {pair.Value}");
                }
            }

            if (ratingOption)
            {
                float itunesRating = getITunesRating(track);
                string itunesStar = ratingToStar(itunesRating);
                string tagStar = ratingToStar(tagRating);

                if (itunesStar != tagStar)
                {
                    if (itunesStar == NO_RATING)
                    {
                        Console.WriteLine($"{track.Artist} - {track.Name}: set itunes rating to {tagStar}");
                        if (!dryRunOption)
                        {
                            track.Rating = convertRating(tagRating);
                        }
                        if (playlist != null)
                        {
                            playlist.AddTrack(track);
                        }
                    }
                    else if (tagStar == NO_RATING)
                    {
                        Console.WriteLine($"{track.Artist} - {track.Name}: set tag rating to {itunesStar}");
                        if (!dryRunOption)
                        {
                            SetTagRating(tag, itunesRating);
                            tagChanged = true;
                        }
                        if (playlist != null)
                        {
                            playlist.AddTrack(track);
                        }
                    }
                    else
                    {
                        Console.WriteLine($"{track.Artist} - {track.Name}: update tag rating {tagStar} => {itunesStar}");
                        if (!dryRunOption)
                        {
                            SetTagRating(tag, itunesRating);
                            tagChanged = true;
                        }
                        if (playlist != null)
                        {
                            playlist.AddTrack(track);
                        }
                    }
                }
            }

            if (tagsOption)
            {
                if (track.Name != tag.Title)
                {
                    Console.WriteLine($"{track.Artist} - {track.Name}: update tag title '{tag.Title}' => '{track.Name}'");
                }
                if (track.Artist.ToString() != tag.Artist.ToString())
                {
                    Console.WriteLine($"{track.Artist} - {track.Name}: update tag artist '{tag.Artist}' => '{track.Artist}'");
                }
                // || fileTrack.Album != atlTrack.Album || fileTrack.Artist != atlTrack.Artist || fileTrack.Year != atlTrack.Year
            }

            if (tagChanged)
            {
                tag.Save();
            }
        }
    }
}
