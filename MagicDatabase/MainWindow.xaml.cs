using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.IO;
using System.Threading;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Microsoft.Data.Sqlite;
using Newtonsoft.Json;
using System.Net.Http;

namespace MagicDatabase
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        HttpClient scryfall = new();

        string apiBaseUrl = "https://api.scryfall.com/";

        Window cardSearchWindow;

        StackPanel searchContent;

        TextBox searchBox;
        string searchJson;

        ScrollViewer searchScroller;
        StackPanel cardList;

        Card cardToAdd;

        Grid cardGrid;

        int cardCount = 0;

        int maxColumnCount = 7;
        int currentColumn = 0;

        int rowCount = 0;

        double imageWidth = 110;

        StackPanel currentTransaction;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            InitializeHomeGrid();
            // ***TRANSACTION LOG*** //

            InitializeLogPanel();
        }

        private void InitializeHomeGrid()
        {
            if (cardGrid != null)
            {
                cardCount = 0;
                rowCount = 0;
                currentColumn = 0;
                cardGrid.RowDefinitions.Clear();
                cardGrid.ColumnDefinitions.Clear();
            }
            using (SqliteConnection database = new SqliteConnection("Data Source=magicdb.db;Foreign Keys=True;"))
            {
                database.Open();

                SqliteCommand command = new SqliteCommand("CREATE TABLE IF NOT EXISTS transactions " +
                            "(id INTEGER PRIMARY KEY NOT NULL, " +
                             "type TEXT NOT NULL, " + // Possible values: "Addition", "Removal", "Rollback"
                             "rollback_id INTEGER, " +
                             "card_name TEXT NOT NULL, " +
                             "small_uri TEXT NOT NULL, " +
                             "large_uri TEXT NOT NULL, " +
                             "date TEXT NOT NULL, " +
                             "time TEXT NOT NULL, " +
                             "rolled_back INTEGER NOT NULL)", database);
                command.ExecuteNonQuery();

                command = new SqliteCommand("CREATE TABLE IF NOT EXISTS library " +
                                          "(id INTEGER PRIMARY KEY NOT NULL, " +
                                          "transaction_id INTEGER, " +
                                           "name TEXT NOT NULL, " +
                                           "small_uri TEXT NOT NULL, " +
                                           "large_uri TEXT NOT NULL, " +
                                           "FOREIGN KEY (transaction_id) REFERENCES transactions(id))", database);
                command.ExecuteNonQuery();

                command = new SqliteCommand("SELECT * FROM library", database);
                using SqliteDataReader reader = command.ExecuteReader();

                // Initialize grid where cards will be displayed
                cardGrid = new Grid();
                cardGrid.Name = "Home_Grid";
                cardGrid.HorizontalAlignment = HorizontalAlignment.Center;
                cardGrid.VerticalAlignment = VerticalAlignment.Top;
                cardGrid.Width = 1300;
                cardGrid.Margin = new Thickness(20, 20, 20, 20);

                for (int i = 0; i <= rowCount; i++)
                {
                    cardGrid.RowDefinitions.Add(new RowDefinition());
                }

                for (int i = 0; i < maxColumnCount; i++)
                {
                    cardGrid.ColumnDefinitions.Add(new ColumnDefinition());
                }

                // Get info for each card and display them in grid
                while (reader.Read())
                {
                    string name = reader.GetString(2);
                    string smallUri = reader.GetString(3);
                    string largeUri = reader.GetString(4);

                    if (cardCount == 0)
                    {
                        cardGrid.RowDefinitions.Add(new RowDefinition());
                    }

                    CreateNewCard(rowCount, currentColumn, name, smallUri ?? largeUri ?? "");

                    if (++currentColumn >= maxColumnCount)
                    {
                        currentColumn = 0;
                        cardGrid.RowDefinitions.Add(new RowDefinition());
                        rowCount++;
                    }
                    cardCount++;
                }
                // Load grid into Home page
                Home_Panel.Children.Add(cardGrid);
            }
        }

        private void InitializeLogPanel()
        {
            if (currentTransaction != null)
            {
                Transaction_Log_Panel.Children.Clear();
                Transaction_Log_Panel.Children.Add(Transaction_Log_Title);
            }
            using (SqliteConnection database = new SqliteConnection("Data Source=magicdb.db;Foreign Keys=True;"))
            {
                database.Open();

                SqliteCommand command = new SqliteCommand("SELECT * FROM transactions ORDER BY date, time DESC", database);
                SqliteDataReader reader = command.ExecuteReader();
                while (reader.Read())
                {
                    currentTransaction = new();
                    currentTransaction.Orientation = Orientation.Horizontal;
                    currentTransaction.HorizontalAlignment = HorizontalAlignment.Stretch;

                    int id = reader.GetInt32(0);
                    string type = reader.GetString(1);
                    int rollbackId = 0;
                    if (!reader.IsDBNull(2))
                    {
                        rollbackId = reader.GetInt32(2);
                    }
                    string name = reader.GetString(3);
                    string smallUri = reader.GetString(4);
                    string largeUri = reader.GetString(5);
                    string date = reader.GetString(6);
                    string time = reader.GetString(7);
                    bool rolledBack = reader.GetBoolean(8);

                    TextBlock idBlock = new();
                    idBlock.FontSize = 15;
                    idBlock.FontWeight = FontWeights.Bold;
                    idBlock.Text = $"{id}\n{type}";
                    idBlock.VerticalAlignment = VerticalAlignment.Center;
                    idBlock.HorizontalAlignment = HorizontalAlignment.Center;
                    idBlock.Margin = new Thickness(10);

                    Image cardImage = new();
                    cardImage.Width = imageWidth;
                    BitmapImage cardBMP = new();
                    cardBMP.BeginInit();
                    cardBMP.UriSource = new Uri(smallUri);
                    cardBMP.DecodePixelWidth = (int)imageWidth;
                    cardBMP.EndInit();
                    cardImage.Source = cardBMP;
                    cardImage.Margin = new Thickness(5);

                    TextBlock cardName = new();
                    cardName.FontSize = 15;
                    cardName.Text = name;
                    cardName.VerticalAlignment = VerticalAlignment.Center;
                    cardName.Margin = new Thickness(10, 10, 100, 10);

                    TextBlock dateTime = new();
                    dateTime.FontSize = 15;
                    dateTime.Text = $"{date} {time}";
                    dateTime.VerticalAlignment = VerticalAlignment.Center;
                    dateTime.HorizontalAlignment = HorizontalAlignment.Center;
                    dateTime.Margin = new Thickness(10);

                    Button rollbackButton = new();
                    rollbackButton.Tag = new Dictionary<string, object>()
                    {
                        { "id", id },
                        { "name", name },
                        { "small_uri", smallUri },
                        { "large_uri", largeUri }
                    };
                    rollbackButton.Width = 100;
                    rollbackButton.Height = 30;
                    rollbackButton.Margin = new Thickness(10);
                    rollbackButton.VerticalAlignment = VerticalAlignment.Center;
                    rollbackButton.HorizontalAlignment = HorizontalAlignment.Right;
                    if (rolledBack)
                    {
                        rollbackButton.Content = "Rolled Back";
                        rollbackButton.IsEnabled = false;
                    }
                    else if (type == "Rollback")
                    {
                        rollbackButton.Content = $"{(rollbackId == 0 ? "Can't Roll Back" :  $"Rolls Back #{rollbackId}")}";
                        rollbackButton.IsEnabled = false;
                    }
                    else
                    {
                        rollbackButton.Content = "Roll Back";
                        rollbackButton.Click += new RoutedEventHandler(Rollback_Btn_Click);
                    }
                    currentTransaction.Children.Add(idBlock);
                    currentTransaction.Children.Add(cardImage);
                    currentTransaction.Children.Add(cardName);
                    currentTransaction.Children.Add(dateTime);
                    currentTransaction.Children.Add(rollbackButton);
                    Transaction_Log_Panel.Children.Add(currentTransaction);

                    Separator separator = new();
                    Transaction_Log_Panel.Children.Add(separator);
                }
            }
        }

        private void Rollback_Btn_Click(object sender, RoutedEventArgs e)
        {
            Dictionary<string, object> info = (Dictionary<string, object>)((Button)sender).Tag;
            using (SqliteConnection database = new SqliteConnection("Data Source=magicdb.db;Foreign Keys=True;"))
            {
                database.Open();

                DateTime currentTime = DateTime.UtcNow;

                SqliteCommand command = new SqliteCommand("DELETE FROM library WHERE transaction_id = $id", database);
                command.Parameters.AddWithValue("$id", (int)info["id"]);
                command.ExecuteNonQuery();


                command = new SqliteCommand("UPDATE transactions SET rolled_back = $rolled_back WHERE id = $id", database);
                command.Parameters.AddWithValue("$rolled_back", true);
                command.Parameters.AddWithValue("$id", (int)info["id"]);
                command.ExecuteNonQuery();

                command = new SqliteCommand("INSERT INTO transactions (type, rollback_id, card_name, small_uri, large_uri, date, time, rolled_back) " +
                                            "VALUES ($type, $rollback_id, $card_name, $small_uri, $large_uri, $date, $time, $rolled_back)", database);
                command.Parameters.AddWithValue("$type", "Rollback");
                command.Parameters.AddWithValue("$rollback_id", (int)info["id"]);
                command.Parameters.AddWithValue("$card_name", (string)info["name"]);
                command.Parameters.AddWithValue("$small_uri", (string)info["small_uri"]);
                command.Parameters.AddWithValue("$large_uri", (string)info["large_uri"]);
                command.Parameters.AddWithValue("$date", currentTime.ToString("yyyy-MM-dd"));
                command.Parameters.AddWithValue("$time", currentTime.ToString("HH:mm:ss"));
                command.Parameters.AddWithValue("$rolled_back", false);

                command.ExecuteNonQuery();
            }
            Home_Panel.Children.Clear();
            Home_Panel.Children.Add(Card_Btns_Panel);
            InitializeHomeGrid();

            InitializeLogPanel();
        }

        private void Add_Card_Btn_Click(object sender, RoutedEventArgs e)
        {
            cardSearchWindow = new();
            cardSearchWindow.Title = "Search for a card!";
            cardSearchWindow.Width = 750;
            cardSearchWindow.Height = 500;

            searchContent = new();
            searchContent.Orientation = Orientation.Vertical;

            searchBox = new();
            searchBox.Name = "Search_TextBox";
            searchBox.Width = 150;
            searchBox.Margin = new Thickness(10);

            Button executeSearchButton = new();
            executeSearchButton.Content = "Search";
            executeSearchButton.Width = 85;
            executeSearchButton.Click += new RoutedEventHandler(Search_Card_Btn_Click);



            searchContent.Children.Add(searchBox);
            searchContent.Children.Add(executeSearchButton);

            cardSearchWindow.Content = searchContent;
            cardSearchWindow.ShowDialog();
        }

        private async void Search_Card_Btn_Click(object sender, RoutedEventArgs e)
        {
            // TODO: When user searches more than once before closing the window, the ScrollViewer gets multiplied.
            if (!searchBox.Text.Trim().Equals(""))
            {
                HttpResponseMessage response = await scryfall.GetAsync(apiBaseUrl + $"cards/search?unique=prints&q={Uri.EscapeDataString(searchBox.Text.Trim())}");
                if (response.IsSuccessStatusCode)
                {
                    searchJson = await response.Content.ReadAsStringAsync();
                    CardList searchResults = JsonConvert.DeserializeObject<CardList>(searchJson);
                    if (searchResults.ReturnValue.Equals("list"))
                    {
                        searchScroller = new();
                        searchScroller.Height = 405;
                        searchScroller.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;

                        cardList = new();
                        cardList.Orientation = Orientation.Vertical;
                        foreach (Card card in searchResults.Cards)
                        {
                            StackPanel cardListItem = new();
                            cardListItem.Orientation = Orientation.Horizontal;

                            Image cardImage = new();
                            cardImage.Width = imageWidth;
                            BitmapImage cardBMP = new();
                            cardBMP.BeginInit();
                            cardBMP.UriSource = new Uri(card.GetFrontImage(ImageSize.Small));
                            cardBMP.DecodePixelWidth = (int)imageWidth;
                            cardBMP.EndInit();
                            cardImage.Source = cardBMP;
                            cardImage.Margin = new Thickness(5);

                            TextBlock cardName = new();
                            cardName.FontSize = 15;
                            cardName.Text = $"{card.Name}\n{card.SetName}";
                            cardName.VerticalAlignment = VerticalAlignment.Center;

                            Button selectCard = new();
                            selectCard.Content = "Select";
                            selectCard.Width = 70;
                            selectCard.Height = 25;
                            selectCard.HorizontalAlignment = HorizontalAlignment.Right;
                            selectCard.VerticalAlignment = VerticalAlignment.Center;
                            selectCard.Margin = new Thickness(10);
                            selectCard.Tag = card;
                            selectCard.Click += new RoutedEventHandler(Select_Card_Btn_Click);

                            cardListItem.Children.Add(cardImage);
                            cardListItem.Children.Add(cardName);
                            cardListItem.Children.Add(selectCard);

                            cardList.Children.Add(cardListItem);
                        }

                        searchScroller.Content = cardList;
                        searchContent.Children.Add(searchScroller);
                    }
                }
            }
        }

        private void Select_Card_Btn_Click(object sender, RoutedEventArgs e)
        {
            cardToAdd = (Card)((Button)sender).Tag;
            if (cardCount == 0)
            {
                cardGrid.RowDefinitions.Add(new RowDefinition());
            }
            CreateNewCard(rowCount, currentColumn, cardToAdd.Name, cardToAdd.GetFrontImage(ImageSize.Small));

            if (++currentColumn >= maxColumnCount)
            {
                currentColumn = 0;
                cardGrid.RowDefinitions.Add(new RowDefinition());
                rowCount++;
            }
            cardCount++;

            using (SqliteConnection database = new SqliteConnection("Data Source=magicdb.db;Foreign Keys=True;"))
            {
                database.Open();

                DateTime currentTime = DateTime.UtcNow;

                SqliteCommand command = new SqliteCommand($"INSERT INTO transactions (type, card_name, small_uri, large_uri, date, time, rolled_back) " +
                            $"VALUES ($type, $card_name, $small_uri, $large_uri, $date, $time, $rolled_back)", database);
                command.Parameters.AddWithValue("$type", "Addition");
                command.Parameters.AddWithValue("$card_name", cardToAdd.Name);
                command.Parameters.AddWithValue("$small_uri", cardToAdd.GetFrontImage(ImageSize.Small));
                command.Parameters.AddWithValue("large_uri", cardToAdd.GetFrontImage(ImageSize.Large));
                command.Parameters.AddWithValue("$date", currentTime.ToString("yyyy-MM-dd"));
                command.Parameters.AddWithValue("$time", currentTime.ToString("HH:mm:ss"));
                command.Parameters.AddWithValue("$rolled_back", "FALSE");
                command.ExecuteNonQuery();

                int transactionId = 0;

                command = new SqliteCommand("SELECT id FROM transactions ORDER BY date, time DESC LIMIT 1", database);
                SqliteDataReader reader = command.ExecuteReader();
                while (reader.Read())
                {
                    transactionId = reader.GetInt32(0);
                }


                command = new SqliteCommand($"INSERT INTO library (transaction_id, name, small_uri, large_uri) " +
                                                          $"VALUES ($transaction_id, $name, $small, $large)", database);
                command.Parameters.AddWithValue("$transaction_id", transactionId);
                command.Parameters.AddWithValue("$name", cardToAdd.Name);
                command.Parameters.AddWithValue("$small", cardToAdd.GetFrontImage(ImageSize.Small));
                command.Parameters.AddWithValue("$large", cardToAdd.GetFrontImage(ImageSize.Large));
                command.ExecuteNonQuery();
            }

            InitializeLogPanel();

            cardSearchWindow.Close();
        }

        void CreateNewCard(int row, int column, string name, string uri)
        {
            // Initialize container for data in cell
            StackPanel cell = new();
            cell.Orientation = Orientation.Vertical;
            cell.Margin = new Thickness(5, 5, 5, 5);

            // Initialize Image for cell
            Image cardImage = new();
            cardImage.Name = $"Card_{cardCount}";
            cardImage.Width = imageWidth;

            // Initialize bitmap for image and assign it to image's Source
            BitmapImage cardBMP = new();
            cardBMP.BeginInit();
            if (uri.Equals(""))
            {
                uri = Directory.GetCurrentDirectory() + @"\images\card-backside.jpg";
            }
            cardBMP.UriSource = new Uri(uri);
            cardBMP.DecodePixelWidth = (int)imageWidth;
            cardBMP.EndInit();
            cardImage.Source = cardBMP;

            // Initialize text below image for cell
            TextBlock cardName = new();
            cardName.Text = name;
            cardName.FontSize = 20;
            cardName.TextAlignment = TextAlignment.Center;
            cardName.TextWrapping = TextWrapping.Wrap;

            cell.Children.Add(cardImage);
            cell.Children.Add(cardName);

            Grid.SetRow(cell, row);
            Grid.SetColumn(cell, column);

            cardGrid.Children.Add(cell);
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            
        }

        private void Remove_Card_Btn_Click(object sender, RoutedEventArgs e)
        {

        }
    }

    class CardList
    {
        [JsonProperty("object")]
        public string ReturnValue { get; set; }
        [JsonProperty("total_cards")]
        public int CardCount { get; set; }
        [JsonProperty("data")]
        public Card[] Cards { get; set; }
    }

    class Card
    {
        [JsonProperty("object")]
        public string ReturnValue { get; set; }
        [JsonProperty("name")]
        public string Name { get; set; }
        [JsonProperty("image_uris")]
        public CardImages Images { get; set; }
        [JsonProperty("uri")]
        public string ApiLink { get; set; }
        [JsonProperty("scryfall_uri")]
        public string ScryfallLink { get; set; }
        [JsonProperty("card_faces")]
        public Card[] CardFaces { get; set; }
        [JsonProperty("set_name")]
        public string SetName { get; set; }

        public string GetFrontImage(ImageSize size)
        {
            if (Images != null)
            {
                if (size == ImageSize.Small && Images.Small != null)
                {
                    return Images.Small;
                }
                else if (size == ImageSize.Normal && Images.Normal != null)
                {
                    return Images.Normal;
                }
                else if (Images.Large != null)
                {
                    return Images.Large;
                }
                else return Directory.GetCurrentDirectory() + @"\images\card-backside.jpg";
            }
            else if (CardFaces != null && CardFaces[0].Images != null)
            {
                if (size == ImageSize.Small && CardFaces[0].Images.Small != null)
                {
                    return CardFaces[0].Images.Small;
                }
                else if (size == ImageSize.Normal && CardFaces[0].Images.Normal != null)
                {
                    return CardFaces[0].Images.Normal;
                }
                else if (CardFaces[0].Images.Large != null)
                {
                    return CardFaces[0].Images.Large;
                }
                else return Directory.GetCurrentDirectory() + @"\images\card-backside.jpg";
            }
            else
            {
                return Directory.GetCurrentDirectory() + @"\images\card-backside.jpg";
            }
        }
    }

    class CardImages
    {
        [JsonProperty("small")]
        public string Small { get; set; }
        [JsonProperty("normal")]
        public string Normal { get; set; }
        [JsonProperty("large")]
        public string Large { get; set; }
    }

    enum ImageSize
    {
        Small,
        Normal,
        Large
    }
}
