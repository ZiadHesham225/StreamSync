using System.ComponentModel.DataAnnotations;
using StreamSync.DTOs;

namespace StreamSync.Validations
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Property, AllowMultiple = false)]
    public class RequirePasswordIfPrivateAttribute : ValidationAttribute
    {
        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            var instance = validationContext.ObjectInstance;
            if (instance is IPrivateRoomDto roomDto)
            {
                if (roomDto.IsPrivate && string.IsNullOrWhiteSpace(roomDto.Password))
                {
                    return new ValidationResult("Password is required for private rooms.",
                        new[] { nameof(IPrivateRoomDto.Password) });
                }
            }

            return ValidationResult.Success;
        }
    }
}
