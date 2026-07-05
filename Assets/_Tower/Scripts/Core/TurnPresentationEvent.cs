using System;

namespace Tower.Core
{
    public enum TurnPresentationEventType
    {
        Move = 0,
        Ability = 1,
        Skip = 2
    }

    public readonly struct TurnPresentationEvent
    {
        public TurnPresentationEvent(
            TurnPresentationEventType type,
            string unitId,
            int moveDistance = 0,
            string abilityId = null,
            string targetUnitId = null)
        {
            Type = type;
            UnitId = unitId;
            MoveDistance = moveDistance;
            AbilityId = abilityId;
            TargetUnitId = targetUnitId;
        }

        public TurnPresentationEventType Type { get; }
        public string UnitId { get; }
        public int MoveDistance { get; }
        public string AbilityId { get; }
        public string TargetUnitId { get; }
    }

    public interface IActionPresenter
    {
        // Presenters must invoke completion within five seconds and must not block engine progression.
        void Present(TurnPresentationEvent presentationEvent, Action completion);
    }

    public sealed class NullPresenter : IActionPresenter
    {
        public void Present(TurnPresentationEvent presentationEvent, Action completion)
        {
            completion?.Invoke();
        }
    }

    public static class ActionPresenterTimeout
    {
        public static Result PresentWithin(
            IActionPresenter presenter,
            TurnPresentationEvent presentationEvent,
            TimeSpan timeout)
        {
            if (presenter == null)
            {
                return Result.Failure("Presenter is required.");
            }

            var completed = false;
            presenter.Present(presentationEvent, () => completed = true);

            if (!completed)
            {
                return Result.Failure("Presenter did not complete synchronously for timeout validation.");
            }

            return timeout <= TimeSpan.FromSeconds(5)
                ? Result.Success()
                : Result.Failure("Presenter timeout must be five seconds or less.");
        }
    }
}
