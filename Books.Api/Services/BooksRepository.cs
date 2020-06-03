using Books.Api.Context;
using Books.Api.Entities;
using Books.Api.ExternalModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Books.Api.Services
{
    public class BooksRepository : IBooksRepository, IDisposable
    {
        private BooksContext _context;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<BooksRepository> _logger;
        private CancellationTokenSource _cancellationTokenSource;

        public BooksRepository(BooksContext context,
            IHttpClientFactory httpClientFactory,
            ILogger<BooksRepository> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public IEnumerable<Book> GetBooks()
        {
            // simulates delay
            _context.Database.ExecuteSqlCommand("WAITFOR DELAY '00:00:02';");
            return _context.Books.Include(b => b.Author).ToList();
        }

        public async Task<IEnumerable<Book>> GetBooksAsync()
        {
            // simulates delay
            await _context.Database.ExecuteSqlCommandAsync("WAITFOR DELAY '00:00:02';");
            return await _context.Books.Include(b => b.Author).ToListAsync();
        }

        public async Task<IEnumerable<Book>> GetBooksAsync(IEnumerable<Guid> bookIds)
        {
            return await _context.Books.Where(b => bookIds.Contains(b.Id))
                .Include(b => b.Author).ToListAsync();
        }

        public async Task<BookCover> GetBookCoverAsync(string coverId)
        {
            var httpClient = _httpClientFactory.CreateClient();

            var response = await httpClient.GetAsync($"http://localhost:58482/api/bookcovers/{coverId}");

            if (response.IsSuccessStatusCode)
            {
                return JsonConvert
                    .DeserializeObject<BookCover>(await response.Content.ReadAsStringAsync());
            }

            return null;
        }

        public async Task<Book> GetBookAsync(Guid id)
        {
            // Pitfall #1: using Task.Run() on the server
            //_logger.LogInformation($"ThreadId when entering GetBookAsync: {System.Threading.Thread.CurrentThread.ManagedThreadId}");
            // var bookPages = await GetBookPages();

            return await _context.Books.Include(b => b.Author)
                .FirstOrDefaultAsync(b => b.Id == id);
        }

        // Multiple async calls to external API
        public async Task<IEnumerable<BookCover>> GetBookCoversAsync(Guid bookId)
        {
            var httpClient = _httpClientFactory.CreateClient();
            var bookCovers = new List<BookCover>();
            _cancellationTokenSource = new CancellationTokenSource();

            var bookCoverUrls = new[]
            {
                $"http://localhost:58482/api/bookcovers/{bookId}-dummycover1",
                // $"http://localhost:58482/api/bookcovers/{bookId}-dummycover2?returnFault=true", // simulates an external error to cancel this task
                $"http://localhost:58482/api/bookcovers/{bookId}-dummycover3",
                $"http://localhost:58482/api/bookcovers/{bookId}-dummycover4",
                $"http://localhost:58482/api/bookcovers/{bookId}-dummycover5"
            };

            //foreach (var bookCoverUrl in bookCoverUrls)
            //{
            //    var response = await httpClient
            //    .GetAsync(bookCoverUrl);

            //    if (response.IsSuccessStatusCode)
            //    {
            //        bookCovers.Add(JsonConvert.DeserializeObject<BookCover>(
            //            await response.Content.ReadAsStringAsync()));
            //    }                
            //}

            // create the task
            var downloadBookCoverTasksQuery = from bookCoverUrl 
                                              in bookCoverUrls 
                                              select DownloadBookCoverAsync(httpClient,
                                                                              bookCoverUrl,
                                                                              _cancellationTokenSource.Token);

            // start the tasks
            var downloadBookCoverTasks = downloadBookCoverTasksQuery.ToList();

            // executes the tasks parallely
            try
            {
                return await Task.WhenAll(downloadBookCoverTasks);
            }
            catch (OperationCanceledException operationCanceledException)
            {
                _logger.LogInformation($"{operationCanceledException.Message}");
                foreach (var task in downloadBookCoverTasks)
                {
                    _logger.LogInformation($"Task {task.Id} has status {task.Status}");
                }

                return new List<BookCover>();
            }
            catch(Exception exception)
            {
                _logger.LogError($"{exception.Message}");
                throw;
            }
        }

        public void AddBook(Book book)
        {
            if (book == null)
                throw new ArgumentNullException(nameof(book));

            _context.AddAsync(book);
        }

        public async Task<bool> SaveChangesAsync()
        {
            return (await _context.SaveChangesAsync() > 0);
        }

        // Pitfall #1: using Task.Run() on the server
        //private Task<int> GetBookPages()
        //{
        //    return Task.Run(() =>
        //    {
        //        _logger.LogInformation($"ThreadId when calculating the amount of pages: {System.Threading.Thread.CurrentThread.ManagedThreadId}");

        //        var pageCalculator = new Books.Legacy.ComplicatedPageCalculator();
        //        return pageCalculator.CalculateBookPages();                
        //    });
        //}

        private async Task<BookCover> DownloadBookCoverAsync(HttpClient httpClient,
            string bookCoverUlr, CancellationToken cancellationToken)
        {
            //throw new Exception("Generic exception");

            var response = await httpClient.GetAsync(bookCoverUlr, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var bookCover = JsonConvert.DeserializeObject<BookCover>(
                    await response.Content.ReadAsStringAsync());

                return bookCover;
            }

            _cancellationTokenSource.Cancel();

            return null;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if(_context != null)
                {
                    _context.Dispose();
                    _context = null;
                }

                if(_cancellationTokenSource != null)
                {
                    _cancellationTokenSource.Dispose();
                    _cancellationTokenSource = null;
                }
            }
        }        
    }
}
