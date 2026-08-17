using System.Diagnostics;
using Jellyfin.Plugin.CollectionSections.Extensions;
using Jellyfin.Plugin.CollectionSections.Model;
using MediaBrowser.Controller.Collections;
using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Playlists;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Querying;
using Microsoft.Extensions.Logging;
using Episode = MediaBrowser.Controller.Entities.TV.Episode;

namespace Jellyfin.Plugin.CollectionSections
{
    public class ResultsHandler
    {
        private readonly ICollectionManager m_collectionManager;
        private readonly IPlaylistManager m_playlistManager;
        private readonly IDtoService m_dtoService;
        private readonly IUserManager m_userManager;
        private readonly ILogger m_logger;
        private readonly ILibraryManager m_libraryManager;

        public ResultsHandler(ICollectionManager collectionManager, IPlaylistManager playlistManager, 
            IUserManager userManager, IDtoService dtoService, ILogger<ResultsHandler> logger, ILibraryManager libraryManager)
        {
            m_collectionManager = collectionManager;
            m_playlistManager = playlistManager;
            m_dtoService = dtoService;
            m_userManager = userManager;
            m_logger = logger;
            m_libraryManager = libraryManager;
        }
        
        public QueryResult<BaseItemDto> GetCollectionResults(HomeScreenSectionPayload payload)
        {
            Stopwatch timer = new Stopwatch();
            timer.Start();
            
            m_logger.LogInformation($"{payload.AdditionalData} - Start: {timer.ElapsedMilliseconds}ms");
            DtoOptions dtoOptions = new DtoOptions
            {
                Fields = new[]
                {
                    ItemFields.PrimaryImageAspectRatio,
                    ItemFields.MediaSourceCount
                },
                ImageTypes = new[]
                {
                    ImageType.Primary,
                    ImageType.Backdrop,
                    ImageType.Banner,
                    ImageType.Thumb
                },
                ImageTypeLimit = 1
            };

            User user = m_userManager.GetUserById(payload.UserId)!;
            m_logger.LogInformation($"{payload.AdditionalData} - User: {timer.ElapsedMilliseconds}ms");
            
            // Looked up on every request rather than from a startup cache. The cache
            // held BoxSet entities for the lifetime of the server, so a collection
            // edited by anything else (an external collection manager, the web UI)
            // kept serving the membership and DisplayOrder it had at startup.
            BoxSet? collection = m_collectionManager.GetCollections(user)
                .FirstOrDefault(x => x.Name == payload.AdditionalData);
            m_logger.LogInformation($"{payload.AdditionalData} - Collection: {timer.ElapsedMilliseconds}ms");
        
            List<BaseItem> items =  collection?.GetChildren(user, true, null).ToList() ?? new List<BaseItem>();
            
            m_logger.LogInformation($"{payload.AdditionalData} - Children: {timer.ElapsedMilliseconds}ms");
            items = items.Take(Math.Min(items.Count, 16)).ToList();
            m_logger.LogInformation($"{payload.AdditionalData} - ToList: {timer.ElapsedMilliseconds}ms");
        
            var results = new QueryResult<BaseItemDto>(m_dtoService.GetBaseItemDtos(items, dtoOptions, user));
            m_logger.LogInformation($"{payload.AdditionalData} - Results: {timer.ElapsedMilliseconds}ms");
            
            timer.Stop();
            return results;
        }

        public QueryResult<BaseItemDto> GetPlaylistResults(HomeScreenSectionPayload payload)
        {
            Stopwatch timer = new Stopwatch();
            timer.Start();
            
            m_logger.LogInformation($"{payload.AdditionalData} - Start: {timer.ElapsedMilliseconds}ms");
            DtoOptions dtoOptions = new DtoOptions
            {
                Fields = new[]
                {
                    ItemFields.PrimaryImageAspectRatio,
                    ItemFields.MediaSourceCount
                },
                ImageTypes = new[]
                {
                    ImageType.Primary,
                    ImageType.Backdrop,
                    ImageType.Banner,
                    ImageType.Thumb
                },
                ImageTypeLimit = 1
            };
            
            // Live lookup, for the same reason as the collection path above.
            Playlist? playlist = m_playlistManager.GetPlaylists(payload.UserId)
                .FirstOrDefault(x => x.Name == payload.AdditionalData);
            m_logger.LogInformation($"{payload.AdditionalData} - Playlist: {timer.ElapsedMilliseconds}ms");

            Tuple<LinkedChild, BaseItem>[] itemsRaw = playlist?.GetManageableItems()
                .Where(i => i.Item2.IsVisible(m_userManager.GetUserById(payload.UserId)))
                .ToArray() ?? Array.Empty<Tuple<LinkedChild, BaseItem>>();

            m_logger.LogInformation($"{payload.AdditionalData} - Items: {timer.ElapsedMilliseconds}ms");
            IGrouping<BaseItem, Tuple<LinkedChild, BaseItem>>[] groupedItems = itemsRaw.GroupBy(x =>
            {
                if (x.Item2 is Episode episode)
                {
                    return episode.Series;
                }

                return x.Item2;
            }).ToArray();
            m_logger.LogInformation($"{payload.AdditionalData} - Grouping: {timer.ElapsedMilliseconds}ms");
        
            IGrouping<BaseItem, Tuple<LinkedChild, BaseItem>>[] items = groupedItems.Take(Math.Min(groupedItems.Count(), 16)).ToArray();
            m_logger.LogInformation($"{payload.AdditionalData} - Limit: {timer.ElapsedMilliseconds}ms");
        
            User user = m_userManager.GetUserById(payload.UserId)!;
            var results = new QueryResult<BaseItemDto>(m_dtoService.GetBaseItemDtos(items.Select(x => x.Key).ToList(), dtoOptions, user));

            m_logger.LogInformation($"{payload.AdditionalData} - Results: {timer.ElapsedMilliseconds}ms");
            return results;
        }
    }
}