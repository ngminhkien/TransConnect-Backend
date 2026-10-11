using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;
using Microsoft.Extensions.DependencyInjection;
using TransConnect.Base.Exceptions;

namespace TransConnect.Common.Response;

public static class ApiValidationExtensions
{
    public static IMvcBuilder AddVietnameseApiValidation(this IMvcBuilder builder) => builder
        .AddMvcOptions(options =>
        {
            // DTO khai báo [Required] với thông báo tiếng Việt; tránh lỗi implicit bằng tiếng Anh.
            options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
            options.ModelMetadataDetailsProviders.Add(new SystemTextJsonValidationMetadataProvider());
            var messages = options.ModelBindingMessageProvider;
            messages.SetMissingRequestBodyRequiredValueAccessor(() => "Vui lòng cung cấp nội dung yêu cầu.");
            messages.SetMissingBindRequiredValueAccessor(field => $"Vui lòng cung cấp trường {field}.");
            messages.SetMissingKeyOrValueAccessor(() => "Thiếu dữ liệu bắt buộc.");
            messages.SetValueMustNotBeNullAccessor(_ => "Trường này không được để trống.");
            messages.SetAttemptedValueIsInvalidAccessor((_, field) => $"Giá trị của trường {field} không hợp lệ.");
            messages.SetNonPropertyAttemptedValueIsInvalidAccessor(_ => "Giá trị không hợp lệ.");
            messages.SetUnknownValueIsInvalidAccessor(field => $"Giá trị của trường {field} không hợp lệ.");
            messages.SetNonPropertyUnknownValueIsInvalidAccessor(() => "Giá trị không hợp lệ.");
            messages.SetValueIsInvalidAccessor(_ => "Giá trị không hợp lệ.");
            messages.SetValueMustBeANumberAccessor(field => $"Trường {field} phải là số.");
            messages.SetNonPropertyValueMustBeANumberAccessor(() => "Giá trị phải là số.");
        })
        .AddJsonOptions(options => options.AllowInputFormatterExceptionMessages = false)
        .ConfigureApiBehaviorOptions(options =>
        {
            // Lỗi 404/405/415 không có body được StatusCodePages xử lý cùng envelope.
            options.SuppressMapClientErrors = true;
            options.InvalidModelStateResponseFactory = context =>
            {
                var errors = context.ModelState
                    .Where(entry => entry.Value?.Errors.Count > 0)
                    .ToDictionary(
                        entry => string.IsNullOrEmpty(entry.Key) ? "body" : entry.Key,
                        entry => entry.Value!.Errors.Select(error =>
                            string.IsNullOrWhiteSpace(error.ErrorMessage)
                                ? "Dữ liệu không hợp lệ. Vui lòng kiểm tra kiểu dữ liệu và định dạng JSON."
                                : error.ErrorMessage).ToArray());

                return new BadRequestObjectResult(ApiResponse<object>.Failure(
                    "Dữ liệu không hợp lệ.", AppErrorCodes.ValidationError, errors));
            };
        });
}
