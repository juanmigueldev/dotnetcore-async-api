using AutoMapper;
using System.Collections.Generic;

namespace Books.Api.Profiles
{
    public class BooksProfile : Profile
    {
        public BooksProfile()
        {
            CreateMap<Entities.Book, Models.BookDto>()
                .ForMember(dest => dest.Author, opt => opt.MapFrom(src =>
                $"{src.Author.FirstName} {src.Author.LastName}"));

            CreateMap<Models.BookForCreationDto, Entities.Book>();

            CreateMap<Entities.Book, Models.BookWithCoversDto>()
                .ForMember(dest => dest.Author, opt => opt.MapFrom(src =>
                $"{src.Author.FirstName} {src.Author.LastName}"));

            CreateMap<ExternalModels.BookCover, Models.BookCoverDto>();

            CreateMap<IEnumerable<ExternalModels.BookCover>, Models.BookWithCoversDto>()
                .ForMember(dest => dest.BookCovers, opt => opt.MapFrom(src =>
                src));
        }
    }
}
