using ATL;
using DotMake.CommandLine;
using iTunesLib;

Cli.Run<RootCliCommand>(args);

[CliCommand(
    Description = "iTunes helper utility",
    NameCasingConvention = CliNameCasingConvention.SnakeCase,
    NamePrefixConvention = CliNamePrefixConvention.DoubleHyphen,
    ShortFormPrefixConvention = CliNamePrefixConvention.SingleHyphen
)]
class RootCliCommand
{

    [CliOption(Alias = "-s", Description = "Process only selected tracks (by default process all)")]
    public bool selected { get; set; }

    [CliOption(Alias = "-n", Description = "Do not do any actual changes, just print actions")]
    public bool dry { get; set; }

    [CliOption(Alias = "-u", Description = "Force update iTunes metadata from file tag")]
    public bool update { get; set; }

    [CliOption(Description = "Sync file tag with iTunes")]
    public bool sync { get; set; }

    private string ratingToStar(int rating)
    {
        return (rating switch
        {
            < 10 => "☆☆☆☆☆",
            < 30 => "★☆☆☆☆",
            < 50 => "★★☆☆☆",
            < 70 => "★★★☆☆",
            < 90 => "★★★★☆",
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

    public void Run()
    {
        iTunesApp app = new iTunesApp();

        var library = app.LibraryPlaylist;

        var tracks = selected ? app.SelectedTracks : library.Tracks;

        foreach (IITTrack anyTrack in tracks)
        {
            if (anyTrack.Kind != ITTrackKind.ITTrackKindFile)
                continue;

            var track = (anyTrack as IITFileOrCDTrack)!;

            Track tag = new(track.Location);

            float itunesRating = getITunesRating(track);
            string itunesStar = ratingToStar(itunesRating);
            string tagStar = ratingToStar(tag.Popularity);

            if (itunesStar != tagStar)
            {
                //Console.WriteLine($"{track.Artist} - {track.Name}: update tag rating {tagStar} => {itunesStar}");
            }

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
    }
}
