using Avalonia;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.Linq;

namespace LegendaryTrafficLights
{
    /// <summary>
    /// Класс перекрестка.
    /// </summary>
#pragma warning disable CS0660 // Type defines operator == or operator != but does not override Object.Equals(object o)
#pragma warning disable CS0661 // Type defines operator == or operator != but does not override Object.GetHashCode()
    public partial class Crossroad : Shape
#pragma warning restore CS0661 // Type defines operator == or operator != but does not override Object.GetHashCode()
#pragma warning restore CS0660 // Type defines operator == or operator != but does not override Object.Equals(object o)
    {
        #region Fields

        /// <summary>
        /// Идентификатор.
        /// </summary>
        public readonly int ID;

        /// <summary>
        /// Положение относительно других перекестков.
        /// </summary>
        public readonly CrossroadPosition position;

        /// <summary>
        /// Список дорог.
        /// </summary>
        public readonly List<Road> Roads;

        /// <summary>
        /// Индекс ситуации по пропуску пешеходов.<br/>
        /// TODO: заменить на объект.
        /// </summary>
        public int PedestrIndex;

        /// <summary>
        /// Визуальная ширина.
        /// </summary>
        public const double ConstWidth = 300;

        /// <summary>
        /// Визуальная ширина вверх.
        /// </summary>
        public const double ConstHeight = 300;

        #endregion

        #region Properties

        /// <summary>
        /// Внутренние дороги.
        /// </summary>
        public List<Road> IntRoads => this.Roads.Where(r => !r.IsExternal).ToList();

        /// <summary>
        /// Внешние дороги.
        /// </summary>
        public List<Road> ExtRoads => this.Roads.Where(r => r.IsExternal).ToList();

        /// <summary>
        /// Идентификаторы внешних дорог.
        /// </summary>
        public int[] IntIDs => this.IntRoads.Select(x => x.ID).ToArray();

        /// <summary>
        /// Идентификаторы внутренних дорог.
        /// </summary>
        public int[] ExtIDs => this.ExtRoads.Select(x => x.ID).ToArray();

        /// <summary>
        /// Перекресток в правой половине.
        /// </summary>
        public bool IsRight => this.position == CrossroadPosition.BottomRight || this.position == CrossroadPosition.TopRight;

        /// <summary>
        /// Перекресток в левой половине.
        /// </summary>
        public bool IsLeft => this.position == CrossroadPosition.BottomLeft || this.position == CrossroadPosition.TopLeft;

        /// <summary>
        /// Перекресток в верхней половине.
        /// </summary>
        public bool IsTop => this.position == CrossroadPosition.TopLeft || this.position == CrossroadPosition.TopRight;

        /// <summary>
        /// Перекресток в нижней половине.
        /// </summary>
        public bool IsBottom => this.position == CrossroadPosition.BottomRight || this.position == CrossroadPosition.BottomLeft;

        /// <summary>
        /// Загруженность перекрестка.
        /// </summary>
        public double Load => this.Roads.Sum(r => r[this].Sum);

        /// <summary>
        /// Текст для отображения.
        /// </summary>
        public string Text => $"{MainWindow.VerticalWide(5)}{this}{Environment.NewLine}Crossroad type: {this.PedestrIndex}{Environment.NewLine}{this.Load:0.00}";

        #endregion

        #region Constructor

        public Crossroad(int ID, CrossroadPosition position)
        {
            this.ID = ID;
            this.Roads = new();
            this.position = position;
            this.PedestrIndex = -1;

            this.Height = ConstHeight;
            this.Width = ConstWidth;
            this.Stroke = Brushes.Black;
            this.StrokeThickness = 3;
        }

        #endregion

        #region Overridings

        public override string ToString() => $"{this.ID} : {this.position}";

        protected override Geometry? CreateDefiningGeometry()
            => new RectangleGeometry(new Rect(new Point(0, 0), new Point(this.Width, this.Height)));

        public static bool operator ==(Crossroad? c1, Crossroad? c2) => c1?.ID == c2?.ID;

        public static bool operator !=(Crossroad? c1, Crossroad? c2) => c1?.ID != c2?.ID;

        #endregion

        #region Public

        /// <summary>
        /// Получить внутреннюю дорогу.
        /// </summary>
        /// <param name="wrongRoad">Дорога, которую брать не нужно.</param>
        /// <returns>Внутренняя дорога.</returns>
        public Road FirstIntNotRoad(Road wrongRoad) => this.IntRoads.First(r => r != wrongRoad);

        /// <summary>
        /// Получить внешнюю дорогу.
        /// </summary>
        /// <param name="wrongRoad">Дорога, которую брать не нужно.</param>
        /// <returns>Внешняя дорога.</returns>
        public Road FirstExtNotRoad(Road wrongRoad) => this.ExtRoads.First(r => r != wrongRoad);

        /// <summary>
        /// Получить дорогу по положению.
        /// </summary>
        /// <param name="position">Положение дороги относительно перекрестка.</param>
        /// <returns>Дорога, соответствующая положению.</returns>
        public Road GetByDirection(RoadPosition position) => this.Roads.First(r => r.A == this && r.APosition == position || r.B == this && r.BPosition == position);

        /// <summary>
        /// Создание копии объекта.
        /// </summary>
        /// <returns>Копия объекта.</returns>
        public Crossroad Clone() => new(this.ID, this.position) { PedestrIndex = this.PedestrIndex };

        #endregion
    }
}
