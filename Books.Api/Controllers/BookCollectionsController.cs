using AutoMapper;
using Books.Api.Filters;
using Books.Api.Helpers;
using Books.Api.Models;
using Books.Api.Services;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Books.Api.Controllers
{
    [ApiController]
    [Route("api/bookcollections")]
    [BooksResultFilter]
    public class BookCollectionsController : ControllerBase
    {
        private readonly IBooksRepository _booksRepository;
        private readonly IMapper _mapper;

        public BookCollectionsController(IBooksRepository booksRepository,
            IMapper mapper)
        {
            _booksRepository = booksRepository ?? throw new ArgumentNullException(nameof(booksRepository));
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        }

        [HttpGet("{bookIds}", Name = "GetBookCollection")]        
        public async Task<IActionResult> GetBookCollection(
            [ModelBinder(BinderType = typeof(ArrayModelBinder))] IEnumerable<Guid> bookIds)
        {
            var books = await _booksRepository.GetBooksAsync(bookIds);

            if (bookIds.Count() != books.Count())
                return NotFound();

            return Ok(books);
        }

        [HttpPost]
        public async Task<IActionResult> CreateBookCollection(
            [FromBody]IEnumerable<BookForCreationDto> bookCollection)
        {
            var bookEntities = _mapper.Map<IEnumerable<Entities.Book>>(bookCollection);

            foreach(var bookEntity in bookEntities)
            {
                _booksRepository.AddBook(bookEntity);
            }

            await _booksRepository.SaveChangesAsync();

            // refill authors
            var booksToReturn = await _booksRepository.GetBooksAsync(bookEntities.Select(b => b.Id));

            var bookIds = string.Join(",", booksToReturn.Select(b => b.Id));

            return CreatedAtRoute("GetBookCollection",
                new { bookIds },
                booksToReturn);
        }
    }
}
