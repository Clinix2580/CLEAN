using AutoMapper;
using OS.Application.DTOs;
using OS.Domain.Entities;

namespace OS.Application.Mappings;

public class MappingProfile : Profile
{
    public MappingProfile()
    {
        // User mappings
        CreateMap<User, UserDto>();
        CreateMap<CreateUserDto, User>();
        CreateMap<UpdateUserDto, User>()
            .ForAllMembers(opts => opts.Condition((src, dest, srcMember) => srcMember != null));

        // Patient mappings
        CreateMap<Patient, PatientDto>();
        CreateMap<Patient, PatientListDto>()
            .ForMember(dest => dest.FullName, opt => opt.MapFrom(src => $"{src.FirstName} {src.LastName}"));
        CreateMap<CreatePatientDto, Patient>();
        CreateMap<UpdatePatientDto, Patient>()
            .ForAllMembers(opts => opts.Condition((src, dest, srcMember) => srcMember != null));

        // MedicalRecord mappings
        CreateMap<MedicalRecord, MedicalRecordDto>()
            .ForMember(dest => dest.Doctor, opt => opt.MapFrom(src => src.DoctorName));

        CreateMap<CreateMedicalRecordDto, MedicalRecord>()
            .ForMember(dest => dest.DoctorName, opt => opt.MapFrom(src => src.Doctor));

        // Product mappings
        CreateMap<Product, ProductDto>();
        CreateMap<Product, ProductListDto>();
        CreateMap<CreateProductDto, Product>();
        CreateMap<UpdateProductDto, Product>()
            .ForAllMembers(opts => opts.Condition((src, dest, srcMember) => srcMember != null));
    }
}
