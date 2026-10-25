using System;
using System.Collections.Generic;
using Windows.Storage;

namespace YTMusicClient.Services;

public enum AppLanguage
{
    English,
    Hindi,
    Hinglish
}

public class LocalizationService
{
    private static LocalizationService? _instance;
    public static LocalizationService Instance => _instance ??= new LocalizationService();

    public event EventHandler? LanguageChanged;

    public AppLanguage CurrentLanguage { get; private set; } = AppLanguage.English;

    private readonly Dictionary<string, string> _english = new()
    {
        ["ListenNow"] = "Listen Now",
        ["ListenNowSubtitle"] = "Top picks, personalized mixes, and lossless studio masters",
        ["Home"] = "Home",
        ["Explore"] = "Explore",
        ["Library"] = "Library",
        ["Downloads"] = "Downloads",
        ["Settings"] = "Settings",
        ["PlayAll"] = "Play All",
        ["Shuffle"] = "Shuffle",
        ["Playlists"] = "Playlists",
        ["LikedSongs"] = "Liked Songs",
        ["SearchPlaceholder"] = "Search songs, albums, artists... (Ctrl+E)",
        ["GoogleSync"] = "Google Sync",
        ["SignInToSync"] = "Sign in to sync",
        ["SyncedWithYTM"] = "Synced with YouTube Music",
        ["SearchAndTrending"] = "Search & Trending Results",
        ["NowPlaying"] = "Now Playing",
        ["ChooseSong"] = "Choose a song to start listening",
        ["AudioAndVinyl"] = "Audio & Vinyl",
        ["MusicVideo"] = "Music Video",
        ["PlayingQueue"] = "Playing Queue",
        ["Clear"] = "Clear",
        ["SyncWithGoogle"] = "Sync with Google",
        ["LikeCurrent"] = "Like Current",
        ["ConnectedAs"] = "Google Account Connected:",
        ["General"] = "General",
        ["Language"] = "Language",
        ["Playback"] = "Playback",
        ["AudioQuality"] = "Audio Quality & Streaming",
        ["CrossfadeDuration"] = "Equal-Power Crossfade Duration",
        ["VolumeNormalization"] = "Volume Normalization (ReplayGain)",
        ["StorageAndCache"] = "Storage & Cache",
        ["ClearCache"] = "Clear Local Audio Cache",
        ["DownloadsFolder"] = "Downloads Location",
        ["LyricsProvider"] = "Lyrics Provider",
        ["TranslateLyrics"] = "Translate Lyrics",
        ["About"] = "About FluentMusic"
    };

    private readonly Dictionary<string, string> _hindi = new()
    {
        ["ListenNow"] = "अभी सुनें",
        ["ListenNowSubtitle"] = "आपके पसंदीदा, व्यक्तिगत मिक्स और लॉसलेस स्टूडियो मास्टर्स",
        ["Home"] = "होम",
        ["Explore"] = "खोजें",
        ["Library"] = "लाइब्रेरी",
        ["Downloads"] = "डाउनलोड",
        ["Settings"] = "सेटिंग्स",
        ["PlayAll"] = "सब चलाएं",
        ["Shuffle"] = "रैंडम चलाएं",
        ["Playlists"] = "प्लेलिस्ट",
        ["LikedSongs"] = "पसंद किए गए गाने",
        ["SearchPlaceholder"] = "गाने, एल्बम, कलाकार खोजें... (Ctrl+E)",
        ["GoogleSync"] = "गूगल सिंक",
        ["SignInToSync"] = "सिंक करने के लिए साइन इन करें",
        ["SyncedWithYTM"] = "यूट्यूब म्यूजिक से सिंक किया गया",
        ["SearchAndTrending"] = "खोज और ट्रेंडिंग परिणाम",
        ["NowPlaying"] = "अब बज रहा है",
        ["ChooseSong"] = "सुनना शुरू करने के लिए कोई गाना चुनें",
        ["AudioAndVinyl"] = "ऑडियो और विनाइल",
        ["MusicVideo"] = "म्यूजिक वीडियो",
        ["PlayingQueue"] = "प्लेइंग कतार",
        ["Clear"] = "साफ़ करें",
        ["SyncWithGoogle"] = "गूगल से सिंक करें",
        ["LikeCurrent"] = "पसंद करें",
        ["ConnectedAs"] = "गूगल खाता कनेक्टेड:",
        ["General"] = "सामान्य",
        ["Language"] = "भाषा (Language)",
        ["Playback"] = "प्लेबैक",
        ["AudioQuality"] = "ऑडियो क्वालिटी और स्ट्रीमिंग",
        ["CrossfadeDuration"] = "इक्वल-पावर क्रॉसफ़ेड अवधि",
        ["VolumeNormalization"] = "वॉल्यूम सामान्यीकरण (ReplayGain)",
        ["StorageAndCache"] = "स्टोरेज और कैश",
        ["ClearCache"] = "लोकल ऑडियो कैश साफ़ करें",
        ["DownloadsFolder"] = "डाउनलोड फ़ोल्डर",
        ["LyricsProvider"] = "लिरिक्स प्रोवाइडर",
        ["TranslateLyrics"] = "बोल का अनुवाद करें",
        ["About"] = "FluentMusic के बारे में"
    };

    private readonly Dictionary<string, string> _hinglish = new()
    {
        ["ListenNow"] = "Listen Now (Abhi Sune)",
        ["ListenNowSubtitle"] = "Aapke top picks, personalized mixes aur studio lossless masters",
        ["Home"] = "Home",
        ["Explore"] = "Explore (Khojein)",
        ["Library"] = "Apni Library",
        ["Downloads"] = "Downloads",
        ["Settings"] = "Settings",
        ["PlayAll"] = "Play All (Sab Chalao)",
        ["Shuffle"] = "Shuffle Gaane",
        ["Playlists"] = "Playlists",
        ["LikedSongs"] = "Liked Gaane",
        ["SearchPlaceholder"] = "Gaane, albums, artists search karein... (Ctrl+E)",
        ["GoogleSync"] = "Google Account Sync",
        ["SignInToSync"] = "Sign in karke sync karein",
        ["SyncedWithYTM"] = "YouTube Music ke saath synced",
        ["SearchAndTrending"] = "Search & Trending Gaane",
        ["NowPlaying"] = "Now Playing (Ab Baj Raha)",
        ["ChooseSong"] = "Sunne ke liye koi gaana chunein",
        ["AudioAndVinyl"] = "Audio & Vinyl",
        ["MusicVideo"] = "Music Video",
        ["PlayingQueue"] = "Up Next Queue",
        ["Clear"] = "Queue Clear",
        ["SyncWithGoogle"] = "Google Sync Karein",
        ["LikeCurrent"] = "Gaana Like Karein",
        ["ConnectedAs"] = "Google Connected Account:",
        ["General"] = "General Settings",
        ["Language"] = "Language (Bhasha)",
        ["Playback"] = "Audio Playback",
        ["AudioQuality"] = "Stream Fidelity & Quality",
        ["CrossfadeDuration"] = "Equal-Power Smooth Crossfade",
        ["VolumeNormalization"] = "Volume Normalization (ReplayGain)",
        ["StorageAndCache"] = "Storage aur Cache",
        ["ClearCache"] = "Audio Cache Clear Karein",
        ["DownloadsFolder"] = "Downloads Folder",
        ["LyricsProvider"] = "Lyrics Source",
        ["TranslateLyrics"] = "Lyrics Translate Karein",
        ["About"] = "FluentMusic Info"
    };

    public LocalizationService()
    {
        LoadSavedLanguage();
    }

    private void LoadSavedLanguage()
    {
        try
        {
            var localSettings = ApplicationData.Current.LocalSettings;
            if (localSettings.Values["SelectedLanguage"] is string langStr)
            {
                if (Enum.TryParse<AppLanguage>(langStr, out var parsed))
                {
                    CurrentLanguage = parsed;
                }
            }
        }
        catch { }
    }

    public void SetLanguage(AppLanguage language)
    {
        CurrentLanguage = language;
        try
        {
            var localSettings = ApplicationData.Current.LocalSettings;
            localSettings.Values["SelectedLanguage"] = language.ToString();
        }
        catch { }

        LanguageChanged?.Invoke(this, EventArgs.Empty);
    }

    public string GetString(string key)
    {
        var dict = CurrentLanguage switch
        {
            AppLanguage.Hindi => _hindi,
            AppLanguage.Hinglish => _hinglish,
            _ => _english
        };

        if (dict.TryGetValue(key, out var val))
        {
            return val;
        }

        if (_english.TryGetValue(key, out var fallback))
        {
            return fallback;
        }

        return key;
    }
}
