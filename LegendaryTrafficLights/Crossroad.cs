using Avalonia;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.Linq;

namespace LegendaryTrafficLights
{
    public partial class Crossroad : Shape
    {
        #region Fields

        public readonly int ID;

        public CrossroadPosition position;

        public readonly List<Road> Roads;

        public int PedestrIndex;

        public const double ConstWidth = 300;
        public const double ConstHeight = 300;

        #endregion

        #region Properties

        public List<Road> IntRoads => this.Roads.Where(r => !r.IsExternal).ToList();
        public List<Road> ExtRoads => this.Roads.Where(r => r.IsExternal).ToList();
        public int[] IntIDs => this.IntRoads.Select(x => x.ID).ToArray();
        public int[] ExtIDs => this.ExtRoads.Select(x => x.ID).ToArray();

        public bool IsRight => this.position == CrossroadPosition.BottomRight || this.position == CrossroadPosition.TopRight;
        public bool IsLeft => this.position == CrossroadPosition.BottomLeft || this.position == CrossroadPosition.TopLeft;
        public bool IsTop => this.position == CrossroadPosition.TopLeft || this.position == CrossroadPosition.TopRight;
        public bool IsBottom => this.position == CrossroadPosition.BottomRight || this.position == CrossroadPosition.BottomLeft;

        public double Load => this.Roads.Sum(r => r[this].Sum);

        public string Text => $"{MainWindow.VerticalWide(5)}{this}{Environment.NewLine}Crossroad type: {this.PedestrIndex}{Environment.NewLine}{this.Load}";

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

        public Crossroad Clone() => new(this.ID, this.position) { PedestrIndex = this.PedestrIndex };

        #endregion

        #region Public

        public Road FirstIntNotRoad(Road wrongRoad) => this.IntRoads.First(r => r != wrongRoad);
        public Road FirstExtNotRoad(Road wrongRoad) => this.ExtRoads.First(r => r != wrongRoad);

        public Road GetByDirection(RoadPosition position) => this.Roads.First(r => r.A == this && r.APosition == position || r.B == this && r.BPosition == position);

        #endregion
    }
}
