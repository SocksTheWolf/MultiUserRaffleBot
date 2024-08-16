using System;
using System.Threading;
using System.Threading.Tasks;
using Tiltify;
using Tiltify.Exceptions;
using Tiltify.Models;
using MultiUserRaffleBot.Types;

namespace MultiUserRaffleBot.Models
{
    public class OnAuthUpdateArgs(string oAuthToken, string refreshToken)
    {
        public string OAuthToken = oAuthToken;
        public string RefreshToken = refreshToken;
    }

    public class TiltifyService : BaseServiceTickable
    {
        private readonly Tiltify.Tiltify? Campaign;
        private readonly string CampaignId = string.Empty;
        private double CurrentAmountRaised = 0.0;
        private int CurrentFactorAmount = 0;
        private readonly int PollInterval;

        // Fires whenever the authorization updated for Tiltify
        public Action<OnAuthUpdateArgs>? OnAuthUpdate { private get; set; }

        public TiltifyService(TiltifySettings config)
        {
            ApiSettings apiSettings = new ApiSettings
            {
                ClientID = config.ClientID,
                ClientSecret = config.ClientSecret
            };

            Campaign = new Tiltify.Tiltify(null, null, apiSettings);
            CampaignId = config.CampaignID;
            PollInterval = config.PollingInterval;
        }

        public override ConsoleSources GetSource() => ConsoleSources.Tiltify;

        private async Task Login()
        {
            if (OnAuthUpdate == null || Campaign == null)
                return;

            try
            {
                AuthorizationResponse resp = await Campaign.Auth.Authorize();
                if (resp != null)
                {
                    string refreshToken = "";
                    if (!string.IsNullOrEmpty(resp.RefreshToken))
                        refreshToken = resp.RefreshToken;

                    OnAuthUpdate.Invoke(new OnAuthUpdateArgs(resp.AccessToken, refreshToken));
                }
            }
            catch (Exception ex)
            {
                PrintMessage($"Login hit exception: {ex}");
            }
        }

        protected override async Task Tick()
        {
            if (Campaign == null)
                return;

            await Login();

            PrintMessage("Tiltify Ready!");
            using PeriodicTimer timer = new(TimeSpan.FromSeconds(PollInterval));
            while (ShouldRun)
            {
                if (Campaign == null || OnAuthUpdate == null)
                {
                    await timer.WaitForNextTickAsync(default);
                    continue;
                }

                try
                {
                    GetTeamCampaignResponse resp = await Campaign.TeamCampaigns.GetCampaign(CampaignId);
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