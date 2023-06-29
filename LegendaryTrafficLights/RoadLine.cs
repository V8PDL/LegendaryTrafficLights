using System;

namespace LegendaryTrafficLights
{
    /// <summary>
    /// Класс дорожных полос (возможны три направления движения - налево, вперед, направо).
    /// </summary>
    public class RoadLine
    {
        /// <summary>
        /// Число машин, едуших налево.
        /// </summary>
        public double left;

        /// <summary>
        /// Число машин, едущих направо.
        /// </summary>
        public double right;

        /// <summary>
        /// Число машин, едущих прямо.
        /// </summary>
        public double straight;

        public RoadLine(double left, double right, double straight)
        {
            this.left = left;
            this.right = right;
            this.straight = straight;
        }
        
        /// <summary>
        /// Сумма по всем направлениям.
        /// </summary>
        public double Sum => this.left + this.right + this.straight;

        /// <summary>
        /// Получить значение по направлению.<br/>
        /// TODO: сделать четвертое направление - назад, и избавиться от костылей.
        /// </summary>
        /// <param name="direction">Направление.</param>
        /// <returns>Количество машин, едущих в направлении.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Задано неверное направление.</exception>
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