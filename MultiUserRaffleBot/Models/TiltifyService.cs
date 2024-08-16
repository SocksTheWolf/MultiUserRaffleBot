using System;
using System.Threading;
using System.Threading.Tasks;
using Tiltify;
using Tiltify.Exceptions;
using Tiltify.Models;
using MultiUserRaffleBot.Types;

namespace MultiUserRaffleBot.Models
{
    public class TiltifyService : BaseServiceTickable
    {
        private readonly Tiltify.Tiltify? Campaign;
        private TiltifySettings settings;
        private double CurrentAmountRaised = 0.0;
        private int CurrentFactorAmount = 0;
        private bool HasLogin = false;
        private int LoginAttempts = 0;

        public TiltifyService(TiltifySettings config)
        {
            settings = config;
            ApiSettings apiSettings = new ApiSettings
            {
                ClientID = config.ClientID,
                ClientSecret = config.ClientSecret
            };

            Campaign = new Tiltify.Tiltify(null, null, apiSettings);
        }

        public override ConsoleSources GetSource() => ConsoleSources.Tiltify;

        protected override bool Internal_Start()
        {
            if (!settings.IsValid())
            {
                PrintMessage("Tiltify settings are invalid, please fix and restart.");
                return false;
            }
            return base.Internal_Start();
        }

        private async Task<bool> Login()
        {
            ++LoginAttempts;
            if (Campaign == null)
                return false;

            try
            {
                AuthorizationResponse resp = await Campaign.Auth.Authorize();
                if (resp != null)
                {
                    string refreshToken = "";
                    if (!string.IsNullOrEmpty(resp.RefreshToken))
                        refreshToken = resp.RefreshToken;

                    // Clear login attempts on login success
                    LoginAttempts = 0;
                    HasLogin = true;
                    return true;
                }
            }
            catch (Exception ex)
            {
                PrintMessage($"Login hit exception: {ex}");
            }

            return false;
        }

        protected override async Task Tick()
        {
            using PeriodicTimer timer = new(TimeSpan.FromSeconds(settings.PollingInterval));
            while (ShouldRun)
            {
                if (Campaign == null)
                {
                    await timer.WaitForNextTickAsync(default);
                    continue;
                }

                if (!HasLogin)
                {
                    PrintMessage("Logging into Tiltify...");
                    if (await Login())
                    {
                        PrintMessage("Tiltify Ready!");
                        continue;
                    }
                    // Exponential backoff up to 10 min
                    await Task.Delay(Math.Min(1000 * (int)Math.Pow(2, LoginAttempts) / 2, 600000));
                    continue;
                }

                try
                {
                    GetTeamCampaignResponse resp = await Campaign.TeamCampaigns.GetCampaign(settings.CampaignID);
                    if (double.TryParse(resp.Data.TotalAmountRaised?.Value, out CurrentAmountRaised))
                    {
                        int factorValue = (int)(double.Floor(CurrentAmountRaised / 100.0));
                        if (CurrentFactorAmount < factorValue)
                        {
                            int increaseOfFactor = factorValue - CurrentFactorAmount;
                            for (int i = 1; i <= increaseOfFactor; ++i)
                            {
                                double milestone = CurrentFactorAmount + (100.0 * i);
                                Invoke(new SourceEvent(SourceEventType.GoalEvent)
                                {
                                    Amount = milestone,
                                    Currency = resp.Data.CurrencyCode
                                }, true);
                            }
                            CurrentFactorAmount = factorValue;
                        }
                    }
                }
                catch (TokenExpiredException)
                {
                    // If the token expires, get a new one.
                    PrintMessage("Fetching a new token from Tiltify..");
                    await Login();
                    continue;
                }
                catch (Exception ex)
                {
                    PrintMessage($"Loop hit exception: {ex}");
                }

                await timer.WaitForNextTickAsync(default);
            }
        }
    }
}