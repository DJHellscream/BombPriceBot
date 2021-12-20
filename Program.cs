﻿using Discord;
using Discord.WebSocket;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace BombPriceBot
{
    internal class Program
    {
        AConfigurationClass _configClass;
        DiscordSocketClient _client;

        static void Main(string[] args) => new Program().MainAsync().GetAwaiter().GetResult();

        public async Task MainAsync()
        {
            try
            {
                GatewayIntents gatewayIntents = new GatewayIntents();
                gatewayIntents = GatewayIntents.Guilds;

                var _config = new DiscordSocketConfig() { MessageCacheSize = 100, GatewayIntents = gatewayIntents };
                _client = new DiscordSocketClient(_config);
                _client.Log += Log;

                _configClass = JsonConvert.DeserializeObject<AConfigurationClass>(File.ReadAllText("config.json"));

                try
                {
                    string discordToken = File.ReadAllText("token.txt");
                    await _client.LoginAsync(TokenType.Bot, discordToken);
                }
                catch
                {
                    WriteToConsole("Unable to read discord token from token.txt. " +
                        "Please ensure file exists and correct access token is there.");
                }
                await _client.StartAsync();

                _client.Ready += () => { Console.WriteLine("Bot is connected!"); return Task.CompletedTask; };
                await Task.Delay(5000);

                await AsyncGetPrice();

                // Block this task until the program is closed.
                await Task.Delay(-1);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        private async Task AsyncGetPrice()
        {
            WriteToConsole("Getting price");

            HttpClient client = new()
            {
                BaseAddress = new Uri("https://api.pancakeswap.info/api/v2/tokens/" + _configClass.TokenContract)
            };

            // Add an Accept header for JSON format.
            client.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));

            string newNick = "";
            while (true)
            {
                try
                {
                    if (_client.Guilds.Count > 0)
                        foreach (var guild in _client.Guilds)
                        {
                            var user = guild.GetUser(_client.CurrentUser.Id);
                            await user.ModifyAsync(x =>
                            {
                                newNick = GetPricePCS<PancakeSwapToken>(client);
                                x.Nickname = newNick;
                            });
                        }
                    else
                        WriteToConsole("Bot is not a part of any guilds.");

                    await Task.Delay(15000);
                }
                catch (Exception e)
                {
                    WriteToConsole(e.ToString());
                    break;
                }
            }

            client.Dispose();
        }

        private string GetPricePCS<T>(HttpClient httpClient) where T : Token
        {
            string s = null;
            StringBuilder result = new StringBuilder();
            HttpClient client = httpClient;

            HttpResponseMessage response = client.GetAsync(s).Result;
            if (response.IsSuccessStatusCode)
            {
                // Parse the response body.
                string dataObjects = response.Content.ReadAsStringAsync().Result;
                T pcsToken = JsonConvert.DeserializeObject<T>(dataObjects);//💣
                result.Append("$" + Decimal.Round(Decimal.Parse(pcsToken.Data.Price), 2) + " " + _configClass.TokenImage + " " + pcsToken.Data.Symbol);
                WriteToConsole(result.ToString());
            }
            else
            {
                WriteToConsole(String.Format("{0} ({1})", (int)response.StatusCode, response.ReasonPhrase));
            }

            return result.ToString();
        }

        private Task Log(LogMessage msg)
        {
            Console.WriteLine(msg.ToString());
            return Task.CompletedTask;
        }

        private void WriteToConsole(String message)
        {
#if DEBUG
            Console.WriteLine(message);
#endif   
        }
    }
}
