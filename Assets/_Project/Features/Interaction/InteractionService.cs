public class InteractionService
{
    private readonly IPlayerInput _input;

    public InteractionService(IPlayerInput input)
    {
        _input = input;
    }

    public void Tick(IInteractable target)
    {
        if (target != null && _input.InteractPressed)
            target.Interact();
    }
}
