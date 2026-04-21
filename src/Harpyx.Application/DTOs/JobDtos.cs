using Harpyx.Domain.Enums;

namespace Harpyx.Application.DTOs;

public record JobDto(Guid Id, Guid DocumentId, string JobType, JobState State, int AttemptCount);
