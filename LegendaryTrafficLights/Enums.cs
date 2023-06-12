namespace LegendaryTrafficLights
{
    /// <summary>
    /// Нахождение дороги относительно светофора.
    /// </summary>
    public enum RoadPosition
    {
        Left,
        Top,
        Right,
        Bottom
    }

    /// <summary>
    /// Нахождение светофора относительно других на карте.
    /// </summary>
    public enum CrossroadPosition
    {
        TopLeft,
        TopRight,
        BottomRight,
        BottomLeft,
    }
}