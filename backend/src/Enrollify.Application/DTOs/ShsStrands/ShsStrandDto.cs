namespace Enrollify.Application.DTOs.ShsStrands;

public record ShsStrandDto(Guid Id, string Track, string Strand, bool IsActive, int DisplayOrder);

public record CreateShsStrandRequest(string Track, string Strand, int DisplayOrder = 0);

public record UpdateShsStrandRequest(string Track, string Strand, bool IsActive, int DisplayOrder);
