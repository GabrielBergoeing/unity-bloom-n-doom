using System;
using System.Collections.Generic;

[Serializable]
public class GridData
{
    public List<GridObjectData> objects = new();
}

[Serializable]
public class GridObjectData
{
    public string type;
    public string subtype;
    public int x;
    public int y;
}
