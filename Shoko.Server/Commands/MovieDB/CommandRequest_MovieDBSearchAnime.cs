﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using Nancy.Session;
using Shoko.Commons.Queue;
using Shoko.Models.Azure;
using Shoko.Models.Enums;
using Shoko.Models.Queue;
using Shoko.Models.Server;
using Shoko.Server.Models;
using Shoko.Server.Providers.Azure;
using Shoko.Server.Providers.MovieDB;
using Shoko.Server.Repositories;


namespace Shoko.Server.Commands
{
    [Serializable]
    public class CommandRequest_MovieDBSearchAnime : CommandRequest
    {
        public virtual int AnimeID { get; set; }
        public virtual bool ForceRefresh { get; set; }

        public override CommandRequestPriority DefaultPriority => CommandRequestPriority.Priority6;

        public override QueueStateStruct PrettyDescription => new QueueStateStruct
        {
            queueState = QueueStateEnum.SearchTMDb,
            extraParams = new[] {AnimeID.ToString()}
        };

        public CommandRequest_MovieDBSearchAnime()
        {
        }

        public CommandRequest_MovieDBSearchAnime(int animeID, bool forced)
        {
            AnimeID = animeID;
            ForceRefresh = forced;
            CommandType = (int) CommandRequestType.MovieDB_SearchAnime;
            Priority = (int) DefaultPriority;

            GenerateCommandID();
        }

        public override void ProcessCommand()
        {
            logger.Info("Processing CommandRequest_MovieDBSearchAnime: {0}", AnimeID);

            try
            {
                       // first check if the user wants to use the web cache
                    if (ServerSettings.WebCache_TvDB_Get)
                    {
                        try
                        {
                            Azure_CrossRef_AniDB_Other crossRef =
                                AzureWebAPI.Get_CrossRefAniDBOther(AnimeID,
                                    CrossRefType.MovieDB);
                            if (crossRef != null)
                            {
                                int movieID = int.Parse(crossRef.CrossRefID);
                                MovieDB_Movie movie = Repo.MovieDb_Movie.GetByMovieID(movieID).FirstOrDefault();
                                if (movie==null)
                                {
                                    // update the info from online
                                    MovieDBHelper.UpdateMovieInfo(movieID, true);
                                    movie = Repo.MovieDb_Movie.GetByMovieID(movieID).First();
                                }

                                if (movie != null)
                                {
                                    // since we are using the web cache result, let's save it
                                    MovieDBHelper.LinkAniDBMovieDB(AnimeID, movieID, true);
                                    return;
                                }
                            }
                        }
                        catch (Exception)
                        {
                        }
                    }

                    // Use TvDB setting
                    if (!ServerSettings.TvDB_AutoLink) return;

                    string searchCriteria = string.Empty;
                    SVR_AniDB_Anime anime = Repo.AniDB_Anime.GetByAnimeID(AnimeID);
                    if (anime == null) return;

                    searchCriteria = anime.MainTitle;

                    // if not wanting to use web cache, or no match found on the web cache go to TvDB directly
                    List<MovieDB_Movie_Result> results = MovieDBHelper.Search(searchCriteria);
                    logger.Trace("Found {0} moviedb results for {1} on TheTvDB", results.Count, searchCriteria);
                    if (ProcessSearchResults(results, searchCriteria)) return;


                    if (results.Count == 0)
                    {
                        foreach (AniDB_Anime_Title title in anime.GetTitles())
                        {
                            if (title.TitleType.ToUpper() != Shoko.Models.Constants.AnimeTitleType.Official.ToUpper())
                                continue;

                            if (searchCriteria.ToUpper() == title.Title.ToUpper()) continue;

                            results = MovieDBHelper.Search(title.Title);
                            logger.Trace("Found {0} moviedb results for search on {1}", results.Count, title.Title);
                            if (ProcessSearchResults(results, title.Title)) return;
                        }
                    }

            }
            catch (Exception ex)
            {
                logger.Error("Error processing CommandRequest_TvDBSearchAnime: {0} - {1}", AnimeID, ex);
            }
        }

        private bool ProcessSearchResults(List<MovieDB_Movie_Result> results, string searchCriteria)
        {
            if (results.Count == 1)
            {
                // since we are using this result, lets download the info
                logger.Trace("Found 1 moviedb results for search on {0} --- Linked to {1} ({2})", searchCriteria,
                    results[0].MovieName, results[0].MovieID);

                int movieID = results[0].MovieID;
                MovieDBHelper.UpdateMovieInfo(movieID, true);
                MovieDBHelper.LinkAniDBMovieDB(AnimeID, movieID, false);
                return true;
            }

            return false;
        }

        public override void GenerateCommandID()
        {
            CommandID = $"CommandRequest_MovieDBSearchAnime{AnimeID}";
        }

        public override bool InitFromDB(Shoko.Models.Server.CommandRequest cq)
        {
            CommandID = cq.CommandID;
            CommandRequestID = cq.CommandRequestID;
            CommandType = cq.CommandType;
            Priority = cq.Priority;
            CommandDetails = cq.CommandDetails;
            DateTimeUpdated = cq.DateTimeUpdated;

            // read xml to get parameters
            if (CommandDetails.Trim().Length > 0)
            {
                XmlDocument docCreator = new XmlDocument();
                docCreator.LoadXml(CommandDetails);

                // populate the fields
                AnimeID = int.Parse(TryGetProperty(docCreator, "CommandRequest_MovieDBSearchAnime", "AnimeID"));
                ForceRefresh =
                    bool.Parse(TryGetProperty(docCreator, "CommandRequest_MovieDBSearchAnime", "ForceRefresh"));
            }

            return true;
        }
    }
}