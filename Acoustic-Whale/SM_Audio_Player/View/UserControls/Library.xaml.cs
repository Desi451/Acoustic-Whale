﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using NAudio.Wave;
using Newtonsoft.Json;
using SM_Audio_Player.Music;
using SM_Audio_Player.View.UserControls.buttons;
using SM_Audio_Player.View.Window;
using Binding = System.Windows.Data.Binding;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MessageBox = System.Windows.MessageBox;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;

namespace SM_Audio_Player.View.UserControls;

/// <summary>
/// Klasa reprezentująca bilioteke utworów aplikacji oraz akcje z nią związane.
/// Takie jak dodawanie, usuwanie czy sortowanie utworów.
/// </summary>
public partial class Library
{
    /// <summary>
    /// Zdarzenie odnoszące się do double clicka w wybrany utwór, dzięki któremu w innych miejscach kodu wyniknie reakcja.
    /// Utworzone zostało aby aktualizować poszczególne dane innych klas. 
    /// </summary>
    public delegate void LibraryEventHandler(object sender, EventArgs e);
    public delegate void OnDeleteTrackEventHandler(object sender, EventArgs e);
    
    /// <summary>
    /// Akcja odpowiadająca za resetowanie danych w momencie, gdy odświeżona lista będzie zawierać mniej elementów
    /// niż ta wartość, która została zapisana przed jej odświeżeniem (Przykładowo, gdy ktoś zmieni ścieżkę do plik
    /// w trakcie używania aplikacji mogła by ona wyrzucić wyjątek)
    /// </summary>
    public delegate void ResetEverythingEventHandler(object sender, EventArgs e);
    public delegate void RefreshSelectedItemEventHandler(object sender, EventArgs e);
    public const string JsonPath = @"MusicTrackList.json";
    private string? _prevColumnSorted;
    private int _sortingtype;
    private readonly List<object> _originalHeaders;

    public Library()
    {
        try
        {
            InitializeComponent();
            ButtonPlay.TrackEnd += OnTrackSwitch;
            ButtonNext.NextButtonClicked += OnTrackSwitch;
            ButtonPrevious.PreviousButtonClicked += OnTrackSwitch;
            ButtonPlay.ButtonPlayEvent += OnTrackSwitch;
            Equalizer.FadeInEvent += OnTrackSwitch;

            ButtonPlay.RefreshList += RefreshTrackList;
            ButtonNext.RefreshList += RefreshTrackList;
            ButtonPrevious.RefreshList += RefreshTrackList;
            MainWindow.AddTrack += Add_Btn_Click;
            RefreshTrackListViewAndId();
            SortTracksList(true, "IdByAdd");
            RefreshTrackListViewAndId();
            _originalHeaders = Lv.Columns.Select(c => c.Header).ToList();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Library constructor exception: {ex.Message}");
            throw;
        }
    }

    public static event LibraryEventHandler? DoubleClickEvent;

    public static event ResetEverythingEventHandler? ResetEverything;

    public static event OnDeleteTrackEventHandler? OnDeleteTrack;

    public static event RefreshSelectedItemEventHandler? ResetSelected;
    
    /// <summary>
    /// Istotne odświeżanie listy gdyby scieżka do pliku się zmieniła w trakcie odtwarzania, track z złą ściażka z pliku
    /// JSON jest wyrzucany przed otworzeniem.
    /// </summary>
    private void RefreshTrackList(object sender, EventArgs e)
    {
        try
        {
            var selectedIndex = Lv.SelectedIndex;
            RefreshTrackListViewAndId();
            Lv.SelectedIndex = selectedIndex;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Library RefreshTrackList exception: {ex.Message}");
            throw;
        }
    }
    
    /// <summary>
    /// Metoda służąca odświeżaniu listView z pliku JSON, do ktorego zapisana zostaje lista piosenek
    /// </summary>
    private void RefreshTrackListViewAndId()
    {
        try
        {
            Lv.Items.Clear();
            TracksProperties.TracksList?.Clear();
            if (File.Exists(JsonPath))
            {
                var json = File.ReadAllText(JsonPath);
                TracksProperties.TracksList = JsonConvert.DeserializeObject<List<Tracks>>(json);
                var coutTracksOnJson = TracksProperties.TracksList?.Count;
                for (var i = 0; i < coutTracksOnJson; i++)
                    if (!File.Exists(TracksProperties.TracksList?.ElementAt(i).Path))
                    {
                        TracksProperties.TracksList?.Remove(TracksProperties.TracksList.ElementAt(i));
                        coutTracksOnJson--;
                        i--;
                    }
                    else
                    {
                        TracksProperties.TracksList.ElementAt(i).Id = i + 1;
                        Lv.Items.Add(TracksProperties.TracksList.ElementAt(i));
                    }

                var newJsonData = JsonConvert.SerializeObject(TracksProperties.TracksList);
                File.WriteAllText(JsonPath, newJsonData);
                Lv.SelectedIndex = -1;
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Refresh Listview error: {ex.Message}");
            throw;
        }
    }
    
    /// <summary>
    /// Metoda odpowiadająca za sortowanie listy z utworami.
    /// </summary>
    public void SortTracksList(bool ascending, string property)
    {
        try
        {
            if (ascending)
                TracksProperties.TracksList = TracksProperties.TracksList?
                    .OrderBy(track => track.GetType().GetProperty(property)?.GetValue(track)).ToList();
            else
                TracksProperties.TracksList = TracksProperties.TracksList?
                    .OrderByDescending(track => track.GetType().GetProperty(property)?.GetValue(track)).ToList();

            // Zapisanie posortowanej listy do JSON
            var newJsonData = JsonConvert.SerializeObject(TracksProperties.TracksList);
            File.WriteAllText(JsonPath, newJsonData);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Sort TrackList error: {ex.Message}");
            throw;
        }
    }
    
    /// <summary>
    /// Metoda odpowiadająca za kliknięcie nagłówka, po którym następuje sortowanie elementów
    /// w liście oraz na listview
    /// </summary>
    private void GridViewColumnHeaderClickedHandler(object sender, RoutedEventArgs e)
    {
        var headerClicked = e.OriginalSource as DataGridColumnHeader;
        if (headerClicked?.Column is DataGridBoundColumn column)
        {
            // Pobierz powiązanie danych z kolumny
            var binding = column.Binding as Binding;
            if (binding != null)
            {
                var bindingPath = binding.Path.Path;

                for (var i = 0; i < Lv.Columns.Count; i++) Lv.Columns[i].Header = _originalHeaders[i];

                // Jeśli ta sama kolumna została kliknięta ponownie, zmień kierunek sortowania
                if (_prevColumnSorted == bindingPath && bindingPath != "Id")
                {
                    if (_sortingtype == 0)
                    {
                        SortTracksList(true, bindingPath);
                        var downArrow = char.ConvertFromUtf32(0x25BC);
                        headerClicked.Column.Header = bindingPath + "  " + downArrow;
                        _sortingtype = 1;
                    }
                    else if (_sortingtype == 1)
                    {
                        SortTracksList(false, bindingPath);
                        var upArrow = char.ConvertFromUtf32(0x25B2);
                        headerClicked.Column.Header = bindingPath + "  " + upArrow;
                        _sortingtype = 2;
                    }
                    else if (_sortingtype == 2)
                    {
                        headerClicked.Column.Header = bindingPath;
                        bindingPath = "IdByAdd";
                        SortTracksList(true, bindingPath);
                        _sortingtype = 0;
                    }

                    _prevColumnSorted = bindingPath;
                }
                // Jeśli inna kolumna została kliknięta, posortuj listę w kolejności rosnącej i ustaw kierunek sortowania na malejący
                else if (_prevColumnSorted != bindingPath && bindingPath != "Id")
                {
                    SortTracksList(true, bindingPath);
                    var downArrow = char.ConvertFromUtf32(0x25BC);
                    headerClicked.Column.Header = bindingPath + "  " + downArrow;
                    _sortingtype = 1;
                    _prevColumnSorted = bindingPath;
                }
            }

            // Odśwież ListView i ustaw zaznaczenie na aktualnie odtwarzanym utworze
            RefreshTrackListViewAndId();
            if (TracksProperties.TracksList != null && TracksProperties.SelectedTrack != null)
                foreach (var track in TracksProperties.TracksList)
                    if (TracksProperties.SelectedTrack.Title == track.Title)
                        Lv.SelectedIndex = track.Id - 1;
        }
    }
    
    /// <summary>
    /// Obsługa zdarzenia kliknięcia na przycisk dodawania utworu.
    /// Dodaje utwór bądź utwory do bilbioteki utworów wraz ze wszystkimi metadanymi.
    /// </summary>
    private void Add_Btn_Click(object sender, EventArgs e)
    {
        try
        {
            // Utworzenie okna dialogowego umożliwiającego wybór plików muzycznych
            var openFileDialog = new OpenFileDialog();
            openFileDialog.Multiselect = true;
            openFileDialog.Filter =
                "Music files (*.mp3)|*.mp3|Waveform Audio File Format (.wav)|.wav|Windows Media Audio Professional (.wma)|.wma|MPEG-4 Audio (.mp4)|.mp4|" +
                "Free Lossless Audio Codec (.flac)|.flac|All files (*.*)|*.*";

            // Wyświetlenie okna dialogowego i dodanie wybranych plików do biblioteki
            if (openFileDialog.ShowDialog() == true)
                foreach (var filePath in openFileDialog.FileNames)
                {
                    var title = Path.GetFileNameWithoutExtension(filePath);
                    var newPath = filePath;

                    // Sprawdzenie, czy plik został już dodany do biblioteki
                    if (TracksProperties.TracksList != null &&
                        TracksProperties.TracksList.Any(track => track.Path == newPath))
                    {
                        var duplicateTrack = Path.GetFileNameWithoutExtension(newPath);
                        var result = MessageBoxYesNo.Show(MessageBoxYesNo.AddTxt + duplicateTrack);
                        if (result == DialogResult.No) continue;
                    }

                    // Pobranie metadanych z pliku muzycznego
                    var file = TagLib.File.Create(newPath);
                    var newTitle = file.Tag.Title ?? title;
                    var newAuthor = file.Tag.FirstPerformer ?? "Unknown";
                    var newAlbum = file.Tag.Album ?? "Unknown";
                    var duration = (int)file.Properties.Duration.TotalSeconds;
                    var albumCover = file.Tag.Pictures.FirstOrDefault();
                    string albumCoverPath;

                    // Jeśli plik posiada okładkę, to zostaje ona zapisana w folderze aplikacji
                    if (albumCover != null)
                    {
                        var albumCoverImage = new BitmapImage();
                        albumCoverImage.BeginInit();
                        albumCoverImage.StreamSource = new MemoryStream(albumCover.Data.Data);
                        albumCoverImage.EndInit();

                        var albumCoverName = $"{Guid.NewGuid()}.jpg";
                        albumCoverPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, albumCoverName);
                        using var fileStream = new FileStream(albumCoverPath, FileMode.Create);
                        var encoder = new JpegBitmapEncoder();
                        encoder.Frames.Add(BitmapFrame.Create(albumCoverImage));
                        encoder.Save(fileStream);
                    }
                    else
                    {
                        albumCoverPath = "..\\..\\assets\\default.png";
                    }

                    // Konwersja czasu trwania utworu na format hh:mm:ss lub mm:ss, w zależności od długości utworu
                    string formattedTime;
                    if (duration >= 3600)
                    {
                        var hours = duration / 3600;
                        var minutes = duration % 3600 / 60;
                        var seconds = duration % 60;
                        formattedTime = string.Format("{0:D2}:{1:D2}:{2:D2}", hours, minutes, seconds);
                    }
                    else
                    {
                        var minutes = duration / 60;
                        var seconds = duration % 60;
                        formattedTime = string.Format("{0:D2}:{1:D2}", minutes, seconds);
                    }

                    // Dodanie utworu do listy utworów
                    if (TracksProperties.TracksList != null)
                    {
                        var newId = TracksProperties.TracksList.Count + 1;
                        var newTrack = new Tracks(newId, newTitle, newAuthor, newAlbum, newPath, formattedTime,
                            albumCoverPath, newId);
                        TracksProperties.TracksList.Add(newTrack);
                    }

                    // Aktualizacja pliku JSON zawierającego dane o utworach
                    var newJsonData = JsonConvert.SerializeObject(TracksProperties.TracksList);
                    File.WriteAllText(JsonPath, newJsonData);
                    RefreshTrackListViewAndId();
                }

            if (Lv.SelectedIndex != -1)
            {
                var elementId = Lv.SelectedIndex;
                TracksProperties.SelectedTrack = TracksProperties.TracksList?.ElementAt(elementId);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Add track exception {ex.Message}");
            throw;
        }
    }
    
    /// <summary>
    /// Obsługa zdarzenia kliknięcia na przycisk usuwania utworu.
    /// Usuwa utwór bądź utwory z bilbioteki utworów.
    /// </summary>
    private void Delete_Btn_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // Sprawdzenie, czy coś jest zaznaczone na liście utworów
            if (Lv.SelectedItems.Count > 0)
            {
                // Wyświetlenie okna dialogowe z potwierdzeniem usunięcia wybranych utworów
                var result = MessageBoxYesNo.Show(MessageBoxYesNo.DelTxt + Lv.SelectedItems.Count);

                // Jeśli użytkownik potwierdzi, usuń wybrane utwory
                if (result == DialogResult.Yes)
                {
                    var selectedIndices = new List<int>();
                    foreach (var item in Lv.SelectedItems) selectedIndices.Add(Lv.Items.IndexOf(item));

                    // Posortowanie indeksów w porządku malejącym, aby uniknąć problemów z usuwaniem wielu elementów
                    selectedIndices.Sort((a, b) => b.CompareTo(a));

                    // Sortowanie indeksów w kolejności malejącej, aby uniknąć problemów z usuwaniem wielu elementów
                    foreach (var index in selectedIndices) TracksProperties.TracksList?.RemoveAt(index);

                    // Zapisanie zaktualizowanej listy utworów do pliku JSON
                    var newJsonData = JsonConvert.SerializeObject(TracksProperties.TracksList);
                    File.WriteAllText(JsonPath, newJsonData);

                    if (TracksProperties.SecWaveOut?.PlaybackState == PlaybackState.Playing
                             && TracksProperties.SecAudioFileReader?.FileName == TracksProperties.SelectedTrack?.Path)
                    {
                        TracksProperties.SecWaveOut.Stop();
                        TracksProperties.SecWaveOut = null;
                        TracksProperties.AudioFileReader = null;
                    }

                    // Resetowanie wybranego utwór i odświeżanie ListView oraz ID utworów
                    RefreshTrackListViewAndId();

                    // Zaznaczenie utworu na indeksie kolejnego elementu poniżej ostatnio zaznaczonego elementu
                    if (Lv.Items.Count > 0)
                    {
                        foreach(Tracks x in TracksProperties.TracksList)
                        {
                            if(x.Path == TracksProperties.AudioFileReader?.FileName)
                            {
                                Lv.SelectedIndex = x.Id -1; break;
                            }
                        }
                    }
                    OnDeleteTrack?.Invoke(this, EventArgs.Empty);
                }
            }
        }
        catch (Exception ex)
        {
            // Obsłużenie i wyświetlenie wszystkich wyjątków, które mogą wystąpić podczas operacji usuwania
            MessageBox.Show($"Delete track exception {ex.Message}");
            throw;
        }
    }

    // Metoda wywoływana po zmianie zaznaczenia na liście utworów
    private void Lv_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        try
        {
            // Jeśli jakiś utwór jest zaznaczony, zaktualizuj wybrany utwór w TracksProperties
            if (Lv.SelectedIndex != -1 && TracksProperties.WaveOut == null && TracksProperties.SecWaveOut == null)
            {
                var elementId = Lv.SelectedIndex;
                TracksProperties.SelectedTrack = TracksProperties.TracksList?.ElementAt(elementId);
                if (TracksProperties.AudioFileReader == null)
                    ResetSelected?.Invoke(this, EventArgs.Empty);
            }
        }
        catch (Exception ex)
        {
            // Obsłuż i wyświetl wszystkie wyjątki, które mogą wystąpić podczas zmiany zaznaczenia utworu
            MessageBox.Show($"Track selectedIndex changing exception {ex.Message}");
            throw;
        }
    }
    
    /// <summary>
    /// Metoda wywoływana po zmianie aktualnie odtwarzanego utworu
    /// </summary>
    private void OnTrackSwitch(object sender, EventArgs e)
    {
        try
        {
            // Jeśli wybrany utwór nie jest pusty, ustaw zaznaczenie ListView na indeks odpowiadający ID wybranego utworu
            if (TracksProperties.SelectedTrack != null)
                Lv.SelectedIndex = TracksProperties.SelectedTrack.Id - 1;
        }
        catch (Exception ex)
        {
            // Obsłuż i wyświetl wszystkie wyjątki, które mogą wystąpić podczas zmiany aktualnie odtwarzanego utworu
            MessageBox.Show($"Track switch Library class exception {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Metoda obsługująca zdarzenie podwójnego kliknięcia na wiersz utworu w liście utworów. Aktualizuje listę utworów,
    /// porównuje wartość przed i po odświeżeniu listy w celu sprawdzenia, czy ścieżka do któregoś z utworów nie uległa zmianie.
    /// Jeśli tak, czyści wszelkie dane związane z utworem i wyświetla komunikat o błędzie. W przeciwnym razie odświeża widok
    /// listy utworów i odtwarza nowo wybrany utwór.
    /// </summary>
    private void OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        try
        {
            /*
            * Walidacja odświeżania listy, zapisuje bieżącą wartość posiadanych utworów na liście, a następnie
            * wykonane zostanie jej odświeżenie poprzez wywołanie 'RefreshList', następnie porównywana jest wartość
            * odświeżonej listy oraz zapisanej, w celu sprawdzenia czy ścieżka, któreś z piosenek nie uległa zmianie.
            * Jeżeli wartość piosenek uległa zmianie, następuje wyczyszczenie wszelkich danych związanych z piosenką
            * zarówno tych w widoku poprzez wywołanie zdarzenia ResetEverything.
            */
            if (TracksProperties.TracksList != null)
            {
                var dg = sender as DataGrid;
                if (dg != null && dg.SelectedItems.Count == 1)
                {
                    var dgr = dg.ItemContainerGenerator.ContainerFromItem(dg.SelectedItem) as DataGridRow;
                    if (dgr != null)
                    {
                        // Zmień styl wiersza na taki sam jak dla właściwości IsSelected
                        dgr.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#45a7bc"));
                        dgr.Foreground = Brushes.White;
                    }
                }

                if (TracksProperties.SecWaveOut != null &&
                    TracksProperties.SecWaveOut.PlaybackState == PlaybackState.Playing)
                {
                    TracksProperties.AudioFileReader = TracksProperties.SecAudioFileReader;
                    TracksProperties.WaveOut?.Stop();
                    TracksProperties.WaveOut?.Init(TracksProperties.AudioFileReader);
                    TracksProperties.SecWaveOut.Stop();
                    TracksProperties.SecWaveOut.Dispose();
                    TracksProperties.SecAudioFileReader = null;
                }

                var trackListBeforeRefresh = TracksProperties.TracksList.Count;
                RefreshTrackList(sender, e);
                if (trackListBeforeRefresh != TracksProperties.TracksList.Count)
                {
                    if (TracksProperties.WaveOut != null && TracksProperties.AudioFileReader != null)
                    {
                        TracksProperties.WaveOut.Pause();
                        TracksProperties.WaveOut.Dispose();
                        TracksProperties.AudioFileReader = null;
                        TracksProperties.SelectedTrack = null;
                    }

                    MessageBox.Show("Ups! Któryś z odtwarzanych utworów zmienił swoją ścieżkę do pliku :(");
                    ResetEverything?.Invoke(this, EventArgs.Empty);
                }
                else
                {
                    var selectedIndex = Lv.SelectedIndex;
                    RefreshTrackListViewAndId();
                    Lv.SelectedIndex = selectedIndex;
                    TracksProperties.SelectedTrack = TracksProperties.TracksList.ElementAt(selectedIndex);
                    var btnPlay = new ButtonPlay();
                    btnPlay.PlayNewTrack();
                    DoubleClickEvent?.Invoke(this, EventArgs.Empty);
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"On double click track {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Metoda obsługująca zdarzenie naciśnięcia klawisza na klawiaturze. Jeśli naciśnięto klawisz Delete, wywołuje metodę
    /// Delete_Btn_Click w celu usunięcia zaznaczonych utworów z listy. Jeśli klawisz CTRL + A został naciśnięty, zaznacza
    /// wszystkie utwory na liście.
    /// </summary>
    private new void KeyDownEvent(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Delete) Delete_Btn_Click(sender, e);

        if (Keyboard.IsKeyDown(Key.LeftCtrl))
            if (e.Key == Key.A)
                Lv.SelectAll();
    }
}