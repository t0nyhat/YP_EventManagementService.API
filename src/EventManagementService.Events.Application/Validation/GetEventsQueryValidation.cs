using System.ComponentModel.DataAnnotations;
using EventManagementService.Events.Application.Dtos;

namespace EventManagementService.Events.Application.Validation;

public static class GetEventsQueryValidation
{
    public const int MinPage = 1;
    public const int MinPageSize = 1;
    public const int MaxPageSize = 100;

    public static IReadOnlyList<ValidationResult> Validate(GetEventsQuery query)
    {
        var errors = new List<ValidationResult>();

        if (query.Page < MinPage)
        {
            errors.Add(new ValidationResult(
                "Номер страницы должен быть не меньше 1.",
                [nameof(GetEventsQuery.Page)]));
        }

        if (query.PageSize < MinPageSize)
        {
            errors.Add(new ValidationResult(
                "Размер страницы должен быть не меньше 1.",
                [nameof(GetEventsQuery.PageSize)]));
        }

        if (query.PageSize > MaxPageSize)
        {
            errors.Add(new ValidationResult(
                "Размер страницы должен быть не больше 100.",
                [nameof(GetEventsQuery.PageSize)]));
        }

        return errors;
    }
}