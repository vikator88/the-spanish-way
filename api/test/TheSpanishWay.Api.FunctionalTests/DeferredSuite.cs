namespace TheSpanishWay.Api.FunctionalTests;

/// <summary>
/// Scaffolded and deliberately empty. The Api project is a composition root with no
/// endpoints yet. The first real test here should drive a command through the HTTP
/// surface and assert the player never receives hidden state.
/// </summary>
public sealed class DeferredSuite
{
    [Fact(Skip = "No endpoints until the API surface is designed. See application/game-use-cases.")]
    public void Player_facing_responses_carry_no_hidden_state()
    {
    }
}
