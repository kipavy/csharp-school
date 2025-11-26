using BattleShip.Models.Game;
using FluentValidation;

namespace BattleShip.API.Validation;

public class AttackRequestValidator : AbstractValidator<AttackRequestDto>
{
    public AttackRequestValidator()
    {
        RuleFor(x => x.GameId).NotEmpty();
        RuleFor(x => x.X).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Y).GreaterThanOrEqualTo(0);
    }
}

