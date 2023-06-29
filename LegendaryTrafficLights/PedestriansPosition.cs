using System;

namespace LegendaryTrafficLights
{
    /// <summary>
    /// Класс ситуации с пешеходными светофорами.
    /// </summary>
    public class PedestriansPosition
    {
        /// <summary>
        /// Получить коэффициент по типу ситуации.
        /// </summary>
        /// <param name="type">Тип.</param>
        /// <returns>Коэффициент.</returns>
        public static double GetCoeff(int type)
        => type switch
            {
                4 => 0.1,
                2 => 1,
                1 => 0.85,
                0 => 0.75,
                _ => 0
            };

        /// <summary>
        /// Идентификатор.
        /// </summary>
        public readonly double ID;

        private readonly double Top2Left;
        private readonly double Top2Right;
        private readonly double Top2Bottom;

        private readonly double Left2Top;
        private readonly double Left2Bottom;
        private readonly double Left2Right;

        private readonly double Right2Top;
        private readonly double Right2Bottom;
        private readonly double Right2Left;

        private readonly double Bottom2Top;
        private readonly double Bottom2Left;
        private readonly double Bottom2Right;

        public RoadLine Bottom => new(this.Bottom2Left, this.Bottom2Right, this.Bottom2Top);
        public RoadLine Top => new(this.Top2Right, this.Top2Left, this.Top2Bottom);
        public RoadLine Left => new(this.Left2Top, this.Left2Bottom, this.Left2Right);
        public RoadLine Right => new(this.Right2Bottom, this.Right2Top, this.Right2Left);

        /// <summary>
        /// Получить коэффициенты зажженных светофоров по положению их относительно перекрестка.
        /// </summary>
        /// <param name="direction">Положение.</param>
        /// <returns><see cref="RoadLine"/> с коэффициентами направления.</returns>
        /// <exception cref="NotImplementedException"></exception>
        public RoadLine CoefByDirection(RoadPosition direction)
            => direction switch
            {
                RoadPosition.Left => this.Left,
                RoadPosition.Top => this.Top,
                RoadPosition.Right => this.Right,
                RoadPosition.Bottom => this.Bottom,
                _ => throw new NotImplementedException()
            };

        public readonly int Type;

        public PedestriansPosition(
            double ID,
            double Top2Left,
            double Top2Bottom,
            double Top2Right,
            double Left2Top,
            double Left2Right,
            double Left2Bottom,
            double Right2Top,
            double Right2Left,
            double Right2Bottom,
            double Bottom2Left,
            double Bottom2Top,
            double Bottom2Right,
            int Type)
        {
            this.ID = ID;
            this.Top2Left = Top2Left;
            this.Top2Right = Top2Right;
            this.Top2Bottom = Top2Bottom;
            this.Left2Top = Left2Top;
            this.Left2Bottom = Left2Bottom;
            this.Left2Right = Left2Right;
            this.Right2Top = Right2Top;
            this.Right2Bottom = Right2Bottom;
            this.Right2Left = Right2Left;
            this.Bottom2Top = Bottom2Top;
            this.Bottom2Left = Bottom2Left;
            this.Bottom2Right = Bottom2Right;
            this.Type = Type;
        }
    }
}