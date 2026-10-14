using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using OptimalApi.Api.Http;

namespace OptimalApi.Api.Filters;

/// <summary>
/// Runs the FluentValidation validator for every action argument that has one, before the action executes.
/// Invalid requests get a 400 in the standard <see cref="Contracts.ApiResponse{T}"/> shape,
/// so controllers never have to validate by hand.
/// </summary>
public sealed class FluentValidationFilter(IServiceProvider services) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        var errors = new ModelStateDictionary();

        foreach (var argument in context.ActionArguments.Values)
        {
            if (argument is null)
            {
                continue;
            }

            var validatorType = typeof(IValidator<>).MakeGenericType(argument.GetType());
            if (services.GetService(validatorType) is not IValidator validator)
            {
                continue;
            }

            var result = await validator.ValidateAsync(
                new ValidationContext<object>(argument), context.HttpContext.RequestAborted);

            foreach (var failure in result.Errors)
            {
                errors.AddModelError(failure.PropertyName, failure.ErrorMessage);
            }
        }

        if (errors.IsValid)
        {
            await next();
            return;
        }

        context.Result = new BadRequestObjectResult(ApiErrors.Validation(context.HttpContext, errors));
    }
}
