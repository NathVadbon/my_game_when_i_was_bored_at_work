using System.Numerics;

public enum ItemType { Coin, Heart, Shield, Rainbow }

public struct Item
{
    public Vector2 pos;
    public ItemType type;
    public float lifetime;
}
