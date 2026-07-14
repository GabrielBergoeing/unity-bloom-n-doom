using Unity.Netcode.Components;

/// <summary>
/// NetworkTransform driven by the OWNER instead of the server.
/// Used for player characters: each client simulates its own movement locally
/// and replicates it to everyone else.
/// </summary>
public class ClientNetworkTransform : NetworkTransform
{
    protected override bool OnIsServerAuthoritative() => false;
}
