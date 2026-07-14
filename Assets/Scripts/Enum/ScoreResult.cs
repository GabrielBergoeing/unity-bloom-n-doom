using Unity.Netcode;

[System.Serializable]
public struct ScoreResult : INetworkSerializable
{
    public int playerIndex;
    public string playerName;
    public int score;
    public int characterId; // which character this player picked (for the portrait)

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref playerIndex);
        serializer.SerializeValue(ref playerName);
        serializer.SerializeValue(ref score);
        serializer.SerializeValue(ref characterId);
    }
}
