namespace YTMusicClient.Models;

public class Playlist
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ThumbnailUrl { get; set; } = string.Empty;
    public int TrackCount { get; set; }
    public List<Song> Songs { get; set; } = new();
}
