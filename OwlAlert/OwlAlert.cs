using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.Entities;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;

namespace OwlAlert
{
    public class OwlAlert
    {
        private DateTime CurrentDatetime { get; set; }
        private ILogger Logger { get; set; }

        private ulong ChannelId = 00000000000000;

        [FunctionName("OwlAlert")]
        public async Task Run([TimerTrigger("1,31 * * * *", RunOnStartup = false)]TimerInfo myTimer, ILogger log)
        {

            try
            {
                Logger = log;
                SetCurrentDatetime();

                Logger.LogInformation($"C# Timer trigger function executed at: {CurrentDatetime.ToLongDateString()}");

                var connectionString = "";
                
                Logger.LogInformation("Creating connection to database...");
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                
                    conn.Open();
                    log.LogInformation("Connection to database created");

                    TimeZoneInfo easternZone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
                    var text = $"SELECT * FROM [games] where start_date BETWEEN '{CurrentDatetime.ToString()}' and '{CurrentDatetime.AddMinutes(30).ToString()}'";
                    List<Game> list = new List<Game>();
                    Logger.LogInformation("Getting games from database...");

                    using (SqlCommand cmd = new SqlCommand(text, conn))
                    {
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                list.Add(new Game
                                {
                                    Id = reader.GetInt32(0),
                                    Title = reader.GetString(1),
                                    StartDate = reader.GetDateTime(2),
                                    TeamOne = reader.GetString(3),
                                    TeamTwo = reader.GetString(5)

                                });
                            }
                        }
                    }

                    Logger.LogInformation(list.Count + " games received");

                    foreach (Game game in list)
                    {
                        Logger.LogInformation("!--------- Processing game with ID: " + game.Id + "---------!");
                        var currentDatetimePlus = CurrentDatetime.AddHours(1);
                        if (game.StartDate > CurrentDatetime & game.StartDate < currentDatetimePlus)
                            await SendDiscordMessage(game);
                    }
                }
                Logger.LogInformation("Finished process");
            }
            catch (Exception e)
            {
                Logger.LogError("Error occurred at " + this.CurrentDatetime);
                Logger.LogInformation(e.Message + "\n" + e.StackTrace);
            }
        }

        public void SetCurrentDatetime()
        {
            var timeUtc = DateTime.UtcNow;

            TimeZoneInfo easternZone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
            DateTime easternTime = TimeZoneInfo.ConvertTimeFromUtc(timeUtc, easternZone);
            CurrentDatetime = easternTime;
        }

        public async Task<bool> SendDiscordMessage(Game game)
        {
            Logger.LogInformation("Preparing to send Discord message. Game ID: " + game.Id);

            Logger.LogInformation("Creating Discord client...");
            var discordClient = new DiscordClient(new DiscordConfiguration
            {
                Token = "",
                TokenType = TokenType.Bot
            });

            Logger.LogInformation("Discord client created");
            Logger.LogInformation("Getting Discord channel with ID: " + ChannelId);
            DiscordChannel discordChannel = await discordClient.GetChannelAsync(ChannelId);
            Logger.LogInformation("Channel received");

            Logger.LogInformation("Creating embed object...");
            var embed = new DiscordEmbedBuilder
            {
                Title = "OWL Alert",
                Description = game.Title + $" starts at {game.StartDate.ToShortTimeString()} EST!",
                Url = "https://www.youtube.com/overwatchleague/live",
                ImageUrl = "https://owllogo.blob.core.windows.net/logo/logo.jpg"

            };
            Logger.LogInformation("Embed object created");

            try
            {
                Logger.LogInformation("Sending message to Discord channel...");
                var response = await discordChannel.SendMessageAsync(embed: embed);
                if(response != null)
                {
                    Logger.LogInformation("Message sent");
                    return true;
                } else
                {
                    Logger.LogError("Message response null");
                    return false;
                }
            }
            catch(Exception e)
            {
                Logger.LogError("Error occured during process");
                Logger.LogError(e.Message + "\n" + e.StackTrace);
                return false;
            }
        }

        public class Game
        {
            public int Id { get; set; }
            public string Title { get; set; }
            public DateTime StartDate { get; set; }
            public string TeamOne { get; set; }
            public string TeamTwo { get; set; }

        }
    }
}
