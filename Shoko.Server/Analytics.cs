﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Shoko.Server.Extensions;
using NLog;
using System.Globalization;
using Shoko.Server.Settings;

namespace Shoko.Server
{
    internal static class Analytics
    {
        private const string AnalyticsId = "UA-128934547-1";
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

//#if !DEBUG
        //private const string Endpoint = "https://www.google-analytics.com/debug";
//#else
        private const string Endpoint = "https://www.google-analytics.com";
//#endif
        /// <summary>
        /// Send the event to Google.
        /// </summary>
        /// <param name="eventCategory">The category to store this under</param>
        /// <param name="eventAction">The action of the event</param>
        /// <param name="eventLabel">The label for the event</param>
        /// <param name="extraData">as per: https://developers.google.com/analytics/devguides/collection/protocol/v1/parameters#ec </param>
        /// <returns></returns>
        internal static bool PostEvent(string eventCategory, string eventAction, string eventLabel = null) => PostData("event", new Dictionary<string, string>
            {
                {"ea", eventAction},
                {"ec", eventCategory},
                {"el", eventLabel ?? "" }
            });

        internal static bool PostException(Exception ex, bool fatal = false) => PostData("exception",
            new Dictionary<string, string>
            {
                {"exd", ex.GetType().FullName},
                {"exf", (fatal ? 1 : 0).ToString()}
            });

        private static bool PostData(string type, IDictionary<string, string> extraData)
        {
            if (ServerSettings.Instance.GA_OptOutPlzDont) return false;

            try
            {
                using (var client = new HttpClient())
                {
                    var data = new Dictionary<string, string>
                    {
                        {"t", type},
                        {"an", "Shoko Server"},
                        {"tid", AnalyticsId},
                        {"cid", ServerSettings.Instance.GA_ClientId.ToString()},
                        {"v", "1"},
                        {"av", Utils.GetApplicationVersion()},
                        {"ul", CultureInfo.GetCultureInfo(ServerSettings.Instance.Culture).DisplayName},
                        {"aip", "1"}
                    };
                    data.AddRange(extraData);

                    var resp = client.PostAsync($"{Endpoint}/collect", new FormUrlEncodedContent(data))
                        .ConfigureAwait(false).GetAwaiter().GetResult();

                    return true;
                }
            }
            catch (HttpRequestException ex)
            {
                Logger.Error("There was an error posting to Google Analytics", ex);
                return false;
            }
        }

        public static void Startup()
        {
            PostEvent("Server", "Startup");
        }
    }
}
