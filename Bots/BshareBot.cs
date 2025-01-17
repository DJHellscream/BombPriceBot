﻿using BombMoney.Configurations;
using BombMoney.Database;
using BombMoney.SmartContracts;
using Discord;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

namespace BombMoney.Bots
{
    public class BshareBot : BotBase
    {
        Timer EpochTimer = null;
        private static readonly string boardroomHistory = "boardroom-history";

        public BshareBot(TokenConfig config, DiscordSocketClient client, BombMoneyOracle moneyOracle, BombMoneyTreasury moneyTreasury,
            IReadOnlyCollection<SocketGuild> socketGuilds)
            : base(config, client, moneyOracle, moneyTreasury, socketGuilds)
        {
            Logging.WriteToConsole("Loading BshareBot...");
        }

        public override void Start()
        {
            base.Start();

            Client.MessageReceived += _client_MessageReceived;
            _ = StartEpochTimer();
        }

        private async Task StartEpochTimer()
        {
            EpochTimer = new Timer(30500);
            EpochTimer.Elapsed += new ElapsedEventHandler(UpdateEpochTimer);
            EpochTimer.Start();
            await Task.CompletedTask;
        }

        private void UpdateEpochTimer(object sender, ElapsedEventArgs e)
        {
            try
            {
                DateTime day = DateTime.Today;
                int addHour = 0;
                bool isDST = TimeZoneInfo.Local.IsDaylightSavingTime(DateTime.Now);
                if (isDST)
                    addHour++;
                int nextEpochHour;
                int currentHour = DateTime.Now.TimeOfDay.Hours;
                if (currentHour >= 18 || (isDST && currentHour == 0) || (isDST && currentHour == 12))
                {
                    nextEpochHour = addHour;
                    day = DateTime.Today + new TimeSpan(1, 0, 0, 0);
                }
                else if (isDST && currentHour == 12)
                {
                    nextEpochHour = addHour;
                }
                else
                    nextEpochHour = currentHour + (6 - currentHour % 6) + addHour;

                TimeSpan timeRemaining = new DateTime(day.Year, day.Month, day.Day, nextEpochHour, 0, 0).Subtract(DateTime.Now);
                string format = @"hh\:mm\:ss";
                string displayString = timeRemaining.ToString(format);

                Logging.WriteToConsole($"Epoch timer: {displayString}");
                Client.SetActivityAsync(new Game($"EPOCH: {displayString}", ActivityType.Watching, ActivityProperties.None, string.Empty));

                RecordEpochData();
            }
            catch (Exception ex)
            {
                Logging.WriteToConsole($"Error updating epoch time: {ex}");
            }
        }

        private async void RecordEpochData()
        {
            try
            {
                // Waiting 5 seconds
                await Task.Delay(5000);
                int epoch = ((BombMoneyOracle)Oracle).GetCurrentEpoch() - 1;
                BoardroomDatum newRecord = BoardroomDatum.RecordBoardRoomData(epoch, ((BombMoneyTreasury)Treasury).PreviousEpochBombPrice(), null);

                if (newRecord != null)
                {
                    if (SocketGuilds != null && SocketGuilds.Count > 0)
                    {
                        foreach (var guild in SocketGuilds)
                        {
                            // follow the AsyncVerifyRoles method but for channels instead.
                            SocketGuildChannel channel = guild.Channels.FirstOrDefault(x => x.Name.Contains(boardroomHistory));

                            if (channel != null)
                            {
                                Color c = Color.Gold;
                                var chnl = Client.GetChannel(channel.Id) as IMessageChannel;

                                if (newRecord.TWAP >= (decimal)1.01)
                                    c = Color.Green;
                                else if (newRecord.TWAP < 1)
                                    c = Color.Red;

                                EmbedBuilder embed = new()
                                {
                                    Title = $"Epoch {newRecord.Epoch}"
                                };
                                embed.AddField("TWAP:", newRecord.TWAP, true);
                                embed.Timestamp = newRecord.Created;
                                embed.WithColor(c);

                                await chnl.SendMessageAsync(null, false, embed.Build(), null, null, null, null, null, null);
                            }
                            else
                                Logging.WriteToConsole($"Channel '{boardroomHistory}' not found.");
                        }
                    }
                }
                else
                {
                    Logging.WriteToConsole($"Epoch {epoch} already recorded.");
                }
            }
            catch (Exception e)
            {
                Logging.WriteToConsole(e.ToString());
            }
        }

        private async Task _client_MessageReceived(SocketMessage arg)
        {
            Embed embed = this.ProcessMessage(arg, out _);

            if (embed != null)
            {
                MessageReference message = new(arg.Id, arg.Channel.Id);
                await arg.Channel.SendMessageAsync(null, false, null, null, null, message, null, null, new Embed[] { embed });
            }
        }

        public override Embed ProcessMessage(SocketMessage arg, out bool authorIsBot)
        {
            Embed embed = base.ProcessMessage(arg, out authorIsBot);

            return embed;
        }
    }
}