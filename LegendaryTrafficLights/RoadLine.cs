using System;

namespace LegendaryTrafficLights
{
    public class RoadLine
    {
        public double left;
        public double right;
        public double straight;

        public RoadLine(double left, double right, double straight)
        {
            this.left = left;
            this.right = right;
            this.straight = straight;
        }

        public double Sum => this.left + this.right + this.straight;

        public double GetByDirection(RoadPosition direction)
            => direction switch
                {
                    RoadPosition.Left => this.left,
                    RoadPosition.Right => this.right,
                    RoadPosition.Top => this.straight,
                    _ => throw new ArgumentOutOfRangeException(nameof(direction))
                };
}
}