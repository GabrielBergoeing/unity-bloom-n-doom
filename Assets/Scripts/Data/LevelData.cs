using System;
using System.Collections.Generic;

[Serializable]
public class LevelData
{
    public List<LevelObjectData> objects = new();
}

[Serializable]
public class LevelObjectData
{
    public string type;
    public string subtype;
    public int x;
    public int y;
}
