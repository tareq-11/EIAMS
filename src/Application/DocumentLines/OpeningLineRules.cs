using Domain.Common;
using Domain.DocumentLines;
using SharedKernel;

namespace Application.DocumentLines;

internal static class OpeningLineRules
{
    public static Result Validate(DocumentType documentType, Guid documentId, OpeningType? openingType)
    {
        if (openingType is not null && !Enum.IsDefined(openingType.Value))
        {
            return Result.Failure(DocumentLineErrors.OpeningTypeInvalid(documentId, openingType.Value));
        }

        if (documentType == DocumentType.Opening && openingType is null)
        {
            return Result.Failure(DocumentLineErrors.OpeningTypeRequired(documentId));
        }

        if (documentType != DocumentType.Opening && openingType is not null)
        {
            return Result.Failure(DocumentLineErrors.OpeningTypeNotAllowed(documentId));
        }

        return Result.Success();
    }
}
