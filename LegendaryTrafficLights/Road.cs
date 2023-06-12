using Avalonia;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using System;
using System.Linq;

namespace LegendaryTrafficLights
{
    public class Road : Shape
    {
        #region Fields

        /// <summary>
        /// Идентификатор.
        /// </summary>
        public readonly int ID;

        /// <summary>
        /// <see langword="null"/> при <see cref="IsExternal"/>.
        /// </summary>
        public readonly Crossroad? A;

        /// <summary>
        /// Всегда должен иметь значение.
        /// </summary>
        public readonly Crossroad B;

        /// <summary>
        /// Положение относительно перекрестка А.
        /// </summary>
        public readonly RoadPosition APosition;

        /// <summary>
        /// Положение относительно перекрестка Б.
        /// </summary>
        public readonly RoadPosition BPosition;

        /// <summary>
        /// Нереализованные идеи.
        /// </summary>
        public readonly bool IsA2B;

        /// <summary>
        /// Нереализованные идеи.
        /// </summary>
        public readonly bool IsB2A;

        /// <summary>
        /// Количество машин, направляющихся из А в Б. В начале они считаются вместе, после - отдельно по направлениям.<br/>
        /// TODO: сделать b2a.start == a2b.finish.back, сделав RoadLine на четыре полосы.
        /// </summary>
        public (double start, RoadLine finish) a2b;

        /// <summary>
        /// Количество машин, направляющихся из Б в А. В начале они считаются вместе, после - отдельно по направлениям.<br/>
        /// TODO: сделать b2a.start == a2b.finish.back, сделав RoadLine на четыре полосы.
        /// </summary>
        public (double start, RoadLine finish) b2a;

        #endregion

        #region Properties

        /// <summary>
        /// Количество машин на доороге.
        /// </summary>
        public double CarsCount => this.a2b.start + this.a2b.finish.Sum + this.b2a.start + this.b2a.finish.Sum;

        /// <summary>
        /// Является ли дорога горизонтальной.
        /// </summary>
        public bool IsHorizontal => this.BPosition == RoadPosition.Left || this.BPosition == RoadPosition.Right;

        /// <summary>
        /// Является ли дорога вертикальной.
        /// </summary>
        public bool IsVertical => !this.IsHorizontal;

        /// <summary>
        /// Является ли дорога внешней.
        /// </summary>
        public bool IsExternal => this.A is null;

        /// <summary>
        /// Является ли дорога внутренней.
        /// </summary>
        public bool IsInternal => !this.IsExternal;

        /// <summary>
        /// Ширина дороги.
        /// </summary>
        public const double SmallWidth = 130;

        /// <summary>
        /// Длина дороги.
        /// </summary>
        public const double BigWidth = 300;

        /// <summary>
        /// Перекрестки дороги списком.
        /// </summary>
        public Crossroad?[] Crossroads => new[] { this.A, this.B };

        public bool BIsLeft => this.BPosition == RoadPosition.Left;
        public bool BIsRight => this.BPosition == RoadPosition.Right;
        public bool BIsTop => this.BPosition == RoadPosition.Top;
        public bool BIsBottom => this.BPosition == RoadPosition.Bottom;
        public bool AIsLeft => this.APosition == RoadPosition.Left;
        public bool AIsRight => this.APosition == RoadPosition.Right;
        public bool AIsTop => this.APosition == RoadPosition.Top;
        public bool AIsBottom => this.APosition == RoadPosition.Bottom;

        /// <summary>
        /// Текст для отображения.
        /// </summary>
        public string Text => this.IsHorizontal
            ? $"→:|{(BIsLeft ? a2b.start : b2a.start):0.00}|{MainWindow.WideInterval(30)}|{(BIsLeft ? a2b.finish.Sum : b2a.finish.Sum):0.00}|:→{Environment.NewLine}"
                + $"←:|{(BIsRight ? a2b.finish.Sum : b2a.finish.Sum):0.00}|{MainWindow.WideInterval(30)}|{(BIsRight ? a2b.start : b2a.start):0.00}|:←"
            : $"↓:|{(BIsTop ? a2b.start : b2a.start):0.00}|--|{(BIsBottom ? a2b.finish.Sum : b2a.finish.Sum):0.00}|:↑{MainWindow.VerticalWide(10)}"
                + $"↓:|{(BIsTop ? a2b.finish.Sum : b2a.finish.Sum):0.00}|--|{(BIsBottom ? a2b.start : b2a.start):0.00}|:↑";

        /// <summary>
        /// Номер среди внутренних дорог.
        /// </summary>
        public int IntID => this.IsExternal ? this.ID : this.ID - MainWindow.ExternalRoadsCount;

        #endregion

        #region Constructors

        public Road(int ID, Crossroad? A, Crossroad B, bool IsHorizontal, bool IsA2B = true, bool IsB2A = true)
        {
            if (B is null)
                throw new ArgumentNullException(nameof(this.B));

            this.ID = ID;
            this.IsA2B = IsA2B;
            this.IsB2A = IsB2A;
            (this.A, this.B) = this.IsExternal ? (null, A ?? B) : (A, B);

            this.Stroke = Brushes.Black;
            this.StrokeThickness = 3;
            this.Height = IsHorizontal ? SmallWidth : BigWidth;
            this.Width = IsHorizontal ? BigWidth : SmallWidth;
            this.a2b.finish = new(0, 0, 0);
            this.b2a.finish = new(0, 0, 0);

            this.BPosition = true switch
            {
                true when IsHorizontal && ((this.IsExternal && this.B.IsLeft) || (!this.IsExternal && this.B.IsRight)) => RoadPosition.Left,
                true when IsHorizontal && ((this.IsExternal && this.B.IsRight) || (!this.IsExternal && this.B.IsLeft)) => RoadPosition.Right,
                true when !IsHorizontal && ((this.IsExternal && this.B.IsTop) || (!this.IsExternal && this.B.IsBottom)) => RoadPosition.Top,
                true when !IsHorizontal && ((this.IsExternal && this.B.IsBottom) || (!this.IsExternal && this.B.IsTop)) => RoadPosition.Bottom,
                _ => throw new NotImplementedException(),
            };

            this.APosition = this.BPosition switch
            {
                RoadPosition.Left => RoadPosition.Right,
                RoadPosition.Top => RoadPosition.Bottom,
                RoadPosition.Right => RoadPosition.Left,
                RoadPosition.Bottom => RoadPosition.Top,
                _ => throw new NotImplementedException(),
            };

            if (this.A?.Roads.Any(r => r == this) == false)
                this.A.Roads.Add(this);
            if (!this.B.Roads.Any(r => r == this))
                this.B.Roads.Add(this);
        }

        #endregion

        #region Overridings

        #region Base overridings

        public override string ToString() => $"{this.ID} : FROM {this.A} ({this.IsA2B}) TO {this.B} ({this.IsB2A})";

        public RoadLine this[Crossroad crossroad]
            => this.B == crossroad ? this.a2b.finish : this.A == crossroad ? this.b2a.finish : throw new ArgumentOutOfRangeException(nameof(crossroad));

        public Road Clone(Crossroad[] cross)
            => new(this.ID, cross.FirstOrDefault(c => c.ID == this.A?.ID), cross.First(c => c.ID == this.B.ID), this.IsHorizontal, this.IsA2B, this.IsB2A)
            {
                a2b = new(this.a2b.start, new(this.a2b.finish?.left ?? 0, this.a2b.finish?.right ?? 0, this.a2b.finish?.straight ?? 0)),
                b2a = new(this.b2a.start, new(this.b2a.finish?.left ?? 0, this.b2a.finish?.right ?? 0, this.b2a.finish?.straight ?? 0)),
            };

        #endregion

        #region Shape overridings

        protected override Geometry? CreateDefiningGeometry()
        => new CombinedGeometry(
                GeometryCombineMode.Union,
                new RectangleGeometry(new Rect(new Point(0, 0), new Point(this.Width, this.Height))),
                new LineGeometry(
                    this.IsHorizontal ? new Point(0, this.Height / 2) : new Point(this.Width / 2, 0),
                    this.IsHorizontal ? new Point(this.Width, this.Height / 2) : new Point(this.Width / 2, this.Height)),
                default);

        #endregion

        #endregion

        #region Public

        /// <summary>
        /// Получить дорогу, на которой будет продолжено движение в определенном направлении.
        /// </summary>
        /// <param name="direction">Направление движения относительно первоначальной дороги.</param>
        /// <param name="useB">Признак того, что необходимо использвать перекресток Б.</param>
        /// <returns>Дорога, по которой будет продолжено движение.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Неверно задан параметр направления.</exception>
        /// <exception cref="NotImplementedException">Не удалось получитьп новое направление.</exception>
        /// <exception cref="ArgumentNullException">Новая дорога не найдена.</exception>
        public Road GetRoad(RoadPosition direction, bool useB = true)
        {
            if (direction == RoadPosition.Bottom)
                throw new ArgumentOutOfRangeException(nameof(direction));

            var newPos = GetPosition(useB ? this.BPosition : this.APosition, direction);

            return (useB
                ? this.B.Roads.FirstOrDefault(r => r.A == this.B && r.APosition == newPos || r.B == this.B && r.BPosition == newPos)
                : this.A?.Roads.FirstOrDefault(r => r.A == this.A && r.APosition == newPos || r.B == this.A && r.BPosition == newPos))
                 ?? throw new ArgumentNullException(nameof(direction));
        }

        /// <summary>
        /// Получить направление движение, которое нужно держать, чтобы с текущей дороги перейти на новую.
        /// </summary>
        /// <param name="road">Дорога, до которой строится путь.</param>
        /// <returns>Направление движения.</returns>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public RoadPosition GetDirection(Road road)
        {
            var pos = road.Crossroads.Contains(this.A) ? this.APosition : this.BPosition;
            var roadPos = this.Crossroads.Contains(road.A) ? road.APosition : this.Crossroads.Contains(road.B) ? road.BPosition : throw new ArgumentOutOfRangeException(nameof(road));

            return GetDirection(pos, roadPos);
        }

        /// <summary>
        /// Получить перекресток, не равный заданному.
        /// </summary>
        /// <param name="notCrossroad">Перекесток, который не нужно возвращать.</param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public Crossroad FirstNotCrossroad(Crossroad notCrossroad) => this.Crossroads.FirstOrDefault(c => c != notCrossroad) ?? throw new ArgumentNullException(nameof(notCrossroad));

        /// <summary>
        /// Получить положение относительно перекрестка.
        /// </summary>
        /// <param name="cross">Перекресток, положение относительно которого нужно получить.</param>
        /// <returns>Проложение.</returns>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public RoadPosition PosByCross(Crossroad cross) => this.A == cross ? this.APosition : this.B == cross ? this.BPosition : throw new ArgumentOutOfRangeException(nameof(cross));

        #endregion

        #region Static

        /// <summary>
        /// Два положения являются противоположными.
        /// </summary>
        /// <param name="p1">Одно положение.</param>
        /// <param name="p2">Другое положение.</param>
        /// <returns>Истина, если противоположны.</returns>
        public static bool IsContr(RoadPosition p1, RoadPosition p2)
            => p1 == RoadPosition.Bottom && p2 == RoadPosition.Top
                || p1 == RoadPosition.Left && p2 == RoadPosition.Right
                || p2 == RoadPosition.Bottom && p1 == RoadPosition.Top
                || p2 == RoadPosition.Left && p1 == RoadPosition.Right;

        /// <summary>
        /// Получить направление движения при перемещении из одной стороны перекрестка в другую.
        /// </summary>
        /// <param name="source">Исходная сторона.</param>
        /// <param name="dest">Сторона назначения.</param>
        /// <returns>Направление движения.</returns>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public static RoadPosition GetDirection(RoadPosition source, RoadPosition dest)
            => true switch
            {
                true when IsContr(source, dest) => RoadPosition.Top,
                true when source == RoadPosition.Top && dest == RoadPosition.Right
                    || source == RoadPosition.Bottom && dest == RoadPosition.Left
                    || source == RoadPosition.Left && dest == RoadPosition.Top
                    || source == RoadPosition.Right && dest == RoadPosition.Bottom => RoadPosition.Left,
                true when source == RoadPosition.Top && dest == RoadPosition.Left
                    || source == RoadPosition.Bottom && dest == RoadPosition.Right
                    || source == RoadPosition.Left && dest == RoadPosition.Bottom
                    || source == RoadPosition.Right && dest == RoadPosition.Top => RoadPosition.Right,
                _ => throw new ArgumentOutOfRangeException(nameof(dest))
            };

        /// <summary>
        /// Получить положение при движении.
        /// </summary>
        /// <param name="position">Изначальное положение.</param>
        /// <param name="direction">Направление движения.</param>
        /// <returns>Новое положение.</returns>
        /// <exception cref="NotImplementedException"></exception>
        private static RoadPosition GetPosition(RoadPosition position, RoadPosition direction)
            => true switch
            {
                true when position == RoadPosition.Top && direction == RoadPosition.Top
                    || position == RoadPosition.Left && direction == RoadPosition.Right
                    || position == RoadPosition.Right && direction == RoadPosition.Left => RoadPosition.Bottom,
                true when position == RoadPosition.Bottom && direction == RoadPosition.Top
                    || position == RoadPosition.Left && direction == RoadPosition.Left
                    || position == RoadPosition.Right && direction == RoadPosition.Right => RoadPosition.Top,
                true when position == RoadPosition.Top && direction == RoadPosition.Right
                    || position == RoadPosition.Right && direction == RoadPosition.Top
                    || position == RoadPosition.Bottom && direction == RoadPosition.Left => RoadPosition.Left,
                true when position == RoadPosition.Top && direction == RoadPosition.Left
                    || position == RoadPosition.Left && direction == RoadPosition.Top
                    || position == RoadPosition.Bottom && direction == RoadPosition.Right => RoadPosition.Right,
                _ => throw new NotImplementedException(),
            };

        #endregion
    }
}