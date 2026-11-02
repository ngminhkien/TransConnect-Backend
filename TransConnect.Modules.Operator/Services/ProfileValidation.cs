using System.ComponentModel.DataAnnotations;
using TransConnect.Base.Exceptions;

namespace TransConnect.Modules.Operator.Services;

internal static class ProfileValidation
{
    public static void Check(object request)
    {
        var results = new List<ValidationResult>();
        if (!Validator.TryValidateObject(request, new ValidationContext(request), results, true))
            throw new ValidationAppException(results.SelectMany(x => x.MemberNames.DefaultIfEmpty("profile")
                .Select(field => (field, error: x.ErrorMessage ?? "Dữ liệu không hợp lệ.")))
                .GroupBy(x => x.field).ToDictionary(x => x.Key, x => x.Select(y => y.error).ToArray()));
    }
}
