using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.LogicalTree;
using Avalonia;
using MessageBox.Avalonia.DTO;
using MessageBox.Avalonia;
using System.Net.Http;
using System.Text.Json;
using System.Globalization;
using Avalonia.Collections;
using System.Threading.Tasks;

namespace LegendaryTrafficLights
{
    /*
     *  Модель:                             
     *                                      
     *         00                 01        
     *     __________         __________    
     *     |        |_________|        |    
     *  04 |    I   |___08____|   II   | 05 
     *     |________|         |________|    
     *       |    |             |    |      
     *       | 11 |             | 09 |          ( -1  для индексов, т. к. с нуля)
     *     __|____|__         __|____|__    
     *     |        |_________|        |    
     *  03 |   III  |____10___|   IV   | 02 
     *     |________|         |________|    
     *         07                 06        
     */

    public partial class MainWindow : Window
    {

        #region Consts

        /// <summary>
        /// Число входящих дорог.
        /// </summary>
        public const int ExternalRoadsCount = 8;

        /// <summary>
        /// Внутренние дороги (между перекрестками.
        /// </summary>
        public const int InternalRoadsCount = 4;
        /// <summary>
        /// Дорог всего.
        /// </summary>
        public const int TotalRoadsCount = ExternalRoadsCount + InternalRoadsCount;

        /// <summary>
        /// Число перекресткв.
        /// </summary>
        public const int CrossroadsCount = 4;

        /// <summary>
        /// Максимальное число итераций, которое нужно, чтобы проехать через участок дороги.<br/>
        /// Предполагается, что <see cref="CrossroadsCount"/> - квадрат целого числа.
        /// </summary>
        //private readonly int MaxRoadTime = ((int)Math.Sqrt(CrossroadsCount) - 1) * 2;
        public const int MaxRoadTime = 2;

        //private const string uri = "http://localhost:8008";

        private const string MinHeader = "Мин.";
        private const string MaxHeader = "Макс.";
        private const string ValueHeader = "Значение";

        private const string NumberHeader = "Номер";
        private const string InterestHeader = "Интересность";

        /// <summary>
        /// Возможные позиции (?) светофоров по отношению к пешеходам.
        /// </summary>
        private readonly PedestriansPosition[] PedestriansPositions = new PedestriansPosition[]
        {
            new(0,              0, 1, 0,
                0, 0, 0,                        0, 0, 0,
                                0, 1, 0,
                2), // --------------------------------------------
            new(1,              1, 1, 0,
                0, 0, 0,                        0, 0, 0,
                                0, 1, 1,
                0), // --------------------------------------------
            new(2,              0, 0, 0,
                0, 1, 0,                        0, 1, 0,
                                0, 0, 0,
                2), // --------------------------------------------
            new(3,              0, 0, 0,
                0, 1, 1,                        1, 1, 0,
                                0, 0, 0,
                0), // --------------------------------------------
            new(4,              0, 0, 0,
                0, 0, 1,                        0, 0, 0,
                                1, 1, 1,
                0), // --------------------------------------------
            new(5,              1, 0, 0,
                1, 1, 1,                        0, 0, 0,
                                0, 0, 0,
                0), // --------------------------------------------
            new(6,              1, 1, 1,
                0, 0, 0,                        1, 0, 0,
                                0, 0, 0,
                0), // --------------------------------------------
            new(7,              0, 0, 0,
                0, 0, 0,                        1, 1, 1,
                                0, 0, 1,
                0), // --------------------------------------------
            new(8,              0, 0, 0,
                0, 0, 1,                        0, 0, 0,
                                1, 0, 0,
                2), // --------------------------------------------
            new(9,              1, 0, 0,
                1, 0, 0,                        0, 0, 0,
                                0, 0, 0,
                2), // --------------------------------------------
            new(10,             0, 0, 1,
                0, 0, 0,                        1, 0, 0,
                                0, 0, 0,
                2), // --------------------------------------------
            new(11,             0, 0, 0,
                0, 0, 0,                        0, 0, 1,
                                0, 0, 1,
                2), // --------------------------------------------
            new(12,             1, 1, 1,
                1, 1, 1,                        1, 1, 1,
                                1, 1, 1,
                4), // --------------------------------------------
        };

        #endregion

        #region Math fields

        /// <summary>
        /// Границы для рандомной генерации входящих машин. Ассоциативный массив. <see langword="object"/>[] - массив из двух элементов, MIN и MAX
        /// </summary>
        private readonly ObservableCollection<Dictionary<string, object>> trafficBorders = new();

        /// <summary>
        /// Входящие машины. Ассрциативный массив. <see langword="object"/>[] - массив из одного элемента - значения. 
        /// </summary>
        private readonly ObservableCollection<Dictionary<string, object>> currentIncomingTraffic = new();

        /// <summary>
        /// Выехавшие на текущей итерации машины.
        /// </summary>
        private readonly int[] currentTrafficOut = new int[ExternalRoadsCount];

        /// <summary>
        /// Вероятности для выездов с участка дороги
        /// </summary>
        private readonly double[][] baseTrafficProbabilities = new double[ExternalRoadsCount][];

        /// <summary>
        /// Вероятности для въездов на внутренние дороги.<br/>
        /// <see langword="["/>Номер перекрестка по очереди въезда<see langword="]"/>
        /// <see langword="["/>Индекс въезда минус восемь (сдвиг, так как внешние не нужно учитывать)<see langword="]"/>
        /// <see langword="["/>Индекс выезда<see langword="]"/>
        /// </summary>
        private readonly double[][][] extendedTrafficProbabilities = new double[MaxRoadTime][][];

        /// <summary>
        /// Предыдущие ситуации.
        /// </summary>
        private readonly Queue<(double a2bCurr, double b2aCurr, double a2bPrev, double b2aPrev)[]> PreviousSituaions = new();

        ///// <summary>
        ///// Коэфициенты загрузки светофоров для дорог.
        ///// </summary>
        //private Dictionary<(int, bool), RoadLine>? LoadingCoefficients;

        ///// <summary>
        ///// Мафия.
        ///// </summary>
        //private double[][]? Mafia;

        /// <summary>
        /// Как же хочется светофорочку…						
        /// </summary>
        private PedestriansPosition[] CrossroadPedestrianPositions = new PedestriansPosition[CrossroadsCount];

        #endregion

        #region Fields

        private readonly Crossroad[] Crossroads = new Crossroad[CrossroadsCount];

        private readonly Road[] Roads = new Road[TotalRoadsCount];
        private bool isRunning = false;
        private bool isStarted = false;

        private readonly double maxTraffic = 50;

        public ObservableCollection<Dictionary<string, object>> Source = new();

        #endregion

        //#region Properties

        //private int[]? MafiaResult => this.Mafia?.Select(m => m.ToList().IndexOf(m.Max())).ToArray();

        //#endregion

        #region Constructors

        public MainWindow()
        {
            this.InitializeComponent();

            this.InitializeFields();
            //this.FillIncomingCars();
            this.UseCertainRadiobutton.IsChecked = true;

            this.InitializeProbabilities();
            this.ChangeStateIsRunning(false);
            this.PaintIt();
            this.SpeedSlider.Value = 2d;
        }

        #endregion

        #region Math

        private void InitializeFields()
        {
            for (var i = 0; i < CrossroadsCount; i++)
                this.Crossroads[i] = new(i, (CrossroadPosition)i);

            this.trafficBorders.Clear();
            this.currentIncomingTraffic.Clear();
            for (var i = 0; i < ExternalRoadsCount; i++)
            {
                this.trafficBorders.Add(new() { [NumberHeader] = i, [InterestHeader] = 5, [MinHeader] = 5, [MaxHeader] = 10 });
                
                this.currentIncomingTraffic.Add(new() { [NumberHeader] = i, [InterestHeader] = 5, [ValueHeader] = 5});

                this.Roads[i] = new(i, null, this.Crossroads[i % CrossroadsCount], i < 4);
                this.baseTrafficProbabilities[i] = new double[TotalRoadsCount];
                this.currentTrafficOut[i] = 0;
            }

            for (var t = 0; t < MaxRoadTime; t++)
            {
                this.extendedTrafficProbabilities[t] = new double[InternalRoadsCount][];
                for (int i = 0; i < InternalRoadsCount; i++)
                    this.extendedTrafficProbabilities[t][i] = new double[TotalRoadsCount];
            }

            // Работает только для четырех светофоров, чуть лучше, чем хардкод
            for (int i = ExternalRoadsCount; i < TotalRoadsCount; i++)
                this.Roads[i] = new(i, this.Crossroads[(i - ExternalRoadsCount) % CrossroadsCount], this.Crossroads[(i - ExternalRoadsCount + 1) % CrossroadsCount], i % 2 == 0);
        }

        /// <summary>
        /// Зануления и базовая инициализация всех значений.
        /// </summary>
        private void InitializeProbabilities()
        {
            int totalInterest = this.currentIncomingTraffic.Sum(x => x[ValueHeader].ToInt());

            for (var i = 0; i < ExternalRoadsCount; i++)
                for (var j = 0; j < ExternalRoadsCount; j++)
                    this.baseTrafficProbabilities[i][j] = i != j
                        ? 1d * this.Source[j][InterestHeader].ToInt() / (totalInterest - this.Source[i][InterestHeader].ToInt())
                        : 0;

            for (var i = 0; i < ExternalRoadsCount; i++)
                for (var j = ExternalRoadsCount; j < TotalRoadsCount; j++)
                    this.AssignProbability(this.baseTrafficProbabilities, i, j, i, j,
                        (_, cur, next) => this.baseTrafficProbabilities[i].Where((_, index) => cur!.ExtIDs.Contains(index)).Sum()
                                        + 0.5 * this.baseTrafficProbabilities[i].Where((_, index) => next!.ExtIDs.Contains(index)).Sum());

            /// TODO: обобщить правила для разных <see cref="extendedTrafficProbabilities"/>[t]
            /// Тогда достаточно будет обернуть в цикл и переработать <see cref="AssignProbability"/>
            for (var i = 0; i < InternalRoadsCount; i++)
            {
                var inRoadIndex = i + ExternalRoadsCount;

                for (var j = 0; j < ExternalRoadsCount; j++)
                    this.AssignProbability(this.extendedTrafficProbabilities[0], inRoadIndex, j, i, j, (prev, _, _) =>
                    {
                        var summingRows = this.baseTrafficProbabilities.Where((_, index) => prev!.ExtIDs.Contains(index));
                        return summingRows.Sum(a => a[j]) / summingRows.Sum(a => a[inRoadIndex]);
                    });

                for (var j = ExternalRoadsCount; j < TotalRoadsCount; j++)
                    this.AssignProbability(this.extendedTrafficProbabilities[0], inRoadIndex, j, i, j, (prev, cur, _) =>
                    {
                        var summingRows = this.baseTrafficProbabilities.Where((_, index) => prev!.ExtIDs.Contains(index));
                        return 0.5 * summingRows.Sum(a => a.Where((_, index) => cur!.ExtIDs.Contains(index)).Sum()) / summingRows.Sum(a => a[inRoadIndex]);
                    });
            }

            /// TODO: обобщить правила для разных <see cref="extendedTrafficProbabilities"/>[t]
            /// Тогда достаточно будет обернуть в цикл и переработать <see cref="AssignProbability"/>
            for (var i = 0; i < InternalRoadsCount; i++)
            {
                var inRoadIndex = i + ExternalRoadsCount;

                for (var j = 0; j < ExternalRoadsCount; j++)
                    this.AssignProbability(this.extendedTrafficProbabilities[1], inRoadIndex, j, i, j, (prev, _, _) =>
                    {
                        var summingRows = this.baseTrafficProbabilities.Where((_, index) => prev!.ExtIDs.Contains(index));
                        return summingRows.Sum(a => a[j]) / (summingRows.Sum(a => a[i]) + summingRows.Sum(a => a[j]));
                    }, true);

                //for (var j = ExternalRoadsCount; j < TotalRoadsCount; j++)
                //    this.AssignProbability(this.internalNeighbourTrafficProbabilities[0], inRoadIndex, j, i, j, (prev, cur, _) =>
                //    {
                //        var summingRows = this.baseTrafficProbabilities.Where((_, index) => prev.ExtIDs.Contains(index));
                //        return 0.5 * summingRows.Sum(a => a.Where((_, index) => cur.ExtIDs.Contains(index)).Sum()) / summingRows.Sum(a => a[i]);
                //    });
            }
        }

        private void FillIncomingCars()
        {
            if (this.UseCertainRadiobutton.IsChecked == true)
                return;

            if (this.FromExternalServeRadiobuttonr.IsChecked == true)
            {
                this.GetDataFromServer();
                return;
            }

            var rand = this.DisableIncomeRadiobutton.IsChecked == true ? null : new Random();
            for (var i = 0; i < ExternalRoadsCount; i++)
                this.currentIncomingTraffic[i][ValueHeader] = rand?.Next(this.trafficBorders[i][MinHeader].ToInt(), this.trafficBorders[i][MaxHeader].ToInt()) ?? 0;
        }

        private async void GetDataFromServer()
        {
            using var client = new HttpClient();
            try
            {
                //var result = await client.GetStringAsync(uri);
                var result = /*lang=json,strict*/ await Task.Run(() => @"{
    ""start_time"": ""20230608225051"",
    ""end_time"": ""20230608225121"",
    ""time_passed"": ""0:00:05.051019"",
    ""statistics"": [
        {
            ""id"": 1,
            ""count_cars"": 0
        },
        {
            ""id"": 2,
            ""count_cars"": 0
        },
        {
            ""id"": 3,
            ""count_cars"": 0
        },
        {
            ""id"": 4,
            ""count_cars"": 0
        }
    ]
}");

                if (string.IsNullOrWhiteSpace(result))
                    return;

                var results = JsonSerializer.Deserialize<StatisticReport>(result);

                if (results?.EndTime is null)
                    throw new ArgumentNullException(nameof(StatisticReport.EndTime));

                results.statistics?.Where(s => s.id < ExternalRoadsCount && s.id > -1).ToList().ForEach(s => { this.currentIncomingTraffic[s.id][ValueHeader] = s.count_cars; });

            }
            catch (Exception ex)
            {
                this.MessageBoxShow(ex.Message);
            }
        }

        /// <summary>
        /// Присвоить значение вероятности выбора пути.
        /// </summary>
        /// <param name="array">Матрица вероятностей.</param>
        /// <param name="inRoadIndex">Индекс входной дороги.</param>
        /// <param name="outRoadIndex">Индекс выходной дороги.</param>
        /// <param name="i">Индекс строки матрицы.</param>
        /// <param name="j">Индекс столбца матрицы.</param>
        /// <param name="assignFunc">Функция присвоения. Аргументы:<br/>
        /// <see cref="Crossroad"/> Перед InRoad (удаленный на <see langword="depth"/>;<br/>
        /// <see cref="Crossroad"/> После OutRoad;<br/>
        /// <see cref="Crossroad"/> Внутренний перекресток после OutRoad
        /// </param>
        /// <param name="depth">Удаленность первого учитываемого перекрестка от входной дороги.</param>
        private void AssignProbability(
            double[][] array,
            int inRoadIndex,
            int outRoadIndex,
            int i,
            int j,
            Func<Crossroad?, Crossroad?, Crossroad?, double> assignFunc,
            bool prevIsMaxDistant = false)
        {
            var inRoad = this.Roads[inRoadIndex];
            var outRoad = this.Roads[outRoadIndex];

            var aCrossroad = inRoad.Crossroads.FirstOrDefault(c => c == outRoad.A || c == outRoad.B);

            if (aCrossroad is null || inRoad == outRoad)
            {
                array[i][j] = 0;
                return;
            }

            var bCrossroad = outRoad.A == aCrossroad ? outRoad.B : outRoad.A;

            var prevCrossroad = inRoad.A == aCrossroad ? inRoad.B : inRoad.A;
            prevCrossroad = prevIsMaxDistant
                ? prevCrossroad?.FirstIntNotRoad(inRoad)?.Crossroads.First(c => c != prevCrossroad)
                : prevCrossroad;

            var nextRoad = bCrossroad?.IntRoads.First(r => !r.Crossroads.Contains(aCrossroad));
            var nextCrossroad = nextRoad?.A == bCrossroad ? nextRoad?.B : nextRoad?.A;

            array[i][j] = assignFunc(prevCrossroad, bCrossroad, nextCrossroad);
        }

        /// <summary>
        /// Итерация.
        /// </summary>
        private void Iterate()
        {
            // New Enemy wave has arrived		
            this.FillIncomingCars();

            var lastSituation = this.PreviousSituaions.Count == 2 ? this.PreviousSituaions.Dequeue() : null;
            this.PreviousSituaions.TryPeek(out var prevSituation);

            #region Update situation

            // Copy!!!
            var crossTmp = this.Crossroads.Select(c => c.Clone()).ToArray();
            var roadsTmp = this.Roads.Select(r => r.Clone(crossTmp)).ToArray();

            foreach (var road in this.Roads)
            {
                var roadCopy = roadsTmp.First(r => r.ID == road.ID);

                if (roadCopy.IsExternal)
                {
                    var traffic = this.currentIncomingTraffic[road.ID][ValueHeader].ToInt();

                    var position = this.PedestriansPositions.ElementAtOrDefault(road.B.PedestrIndex);
                    var roadPosition = position?.CoefByDirection(roadCopy.BPosition);

                    road.a2b.finish.left += traffic * this.baseTrafficProbabilities[road.ID][roadCopy.GetRoad(RoadPosition.Left).ID]
                        - (roadCopy.a2b.finish.left * roadPosition?.left ?? 0);
                    road.a2b.finish.straight += traffic * this.baseTrafficProbabilities[road.ID][roadCopy.GetRoad(RoadPosition.Top).ID]
                        - (roadCopy.a2b.finish.straight * roadPosition?.straight ?? 0);
                    road.a2b.finish.right += traffic * this.baseTrafficProbabilities[road.ID][roadCopy.GetRoad(RoadPosition.Right).ID]
                        - (roadCopy.a2b.finish.right * roadPosition?.right ?? 0);

                    var (start, finish) = roadCopy.b2a;
                    road.b2a.finish = new(finish.left + start / 3, finish.right + start / 3, finish.straight + start / 3);
                    road.b2a.start = roadCopy.B.Roads
                        .Where(r => r != roadCopy)
                        .Sum(r =>
                            position?.CoefByDirection(r.PosByCross(roadCopy.B)).GetByDirection(r.GetDirection(roadCopy))
                                * r[roadCopy.B].GetByDirection(r.GetDirection(roadCopy)) ?? 0);

                    continue;
                }

                foreach (var crossroad in road.Crossroads)
                {
                    if (crossroad is null)
                        throw new ArgumentNullException(nameof(crossroad));

                    var leftRoad = roadCopy.GetRoad(RoadPosition.Left, road.B == crossroad);
                    var rightRoad = roadCopy.GetRoad(RoadPosition.Right, road.B == crossroad);
                    var straightRoad = roadCopy.GetRoad(RoadPosition.Top, road.B == crossroad);

                    road[crossroad].left += lastSituation?[road.IntID].a2bCurr * this.extendedTrafficProbabilities[0][road.IntID][leftRoad.ID] ?? 0
                            + lastSituation?[road.IntID].a2bPrev * this.extendedTrafficProbabilities[1][road.IntID][leftRoad.ID] ?? 0
                            - (leftRoad.IsExternal
                                ? road.a2b.finish.left * this.PedestriansPositions.ElementAtOrDefault(crossroad.PedestrIndex)?.CoefByDirection(road.BPosition).GetByDirection(RoadPosition.Left) ?? 0
                                : leftRoad.A == crossroad ? prevSituation?[leftRoad.IntID].a2bPrev : prevSituation?[leftRoad.IntID].b2aPrev) ?? 0;

                    road[crossroad].right += lastSituation?[road.IntID].a2bCurr * this.extendedTrafficProbabilities[0][road.IntID][rightRoad.ID] ?? 0
                            + lastSituation?[road.IntID].a2bPrev * this.extendedTrafficProbabilities[1][road.IntID][rightRoad.ID] ?? 0
                            - (rightRoad.IsExternal
                                ? road.a2b.finish.left * this.PedestriansPositions.ElementAtOrDefault(crossroad.PedestrIndex)?.CoefByDirection(road.BPosition).GetByDirection(RoadPosition.Left) ?? 0
                                : rightRoad.A == crossroad ? prevSituation?[rightRoad.IntID].a2bPrev : prevSituation?[rightRoad.IntID].b2aPrev) ?? 0;

                    road[crossroad].straight += lastSituation?[road.IntID].a2bCurr * this.extendedTrafficProbabilities[0][road.IntID][straightRoad.ID] ?? 0
                            + lastSituation?[road.IntID].a2bPrev * this.extendedTrafficProbabilities[1][road.IntID][straightRoad.ID] ?? 0
                            - (straightRoad.IsExternal
                                ? road.a2b.finish.left * this.PedestriansPositions.ElementAtOrDefault(crossroad.PedestrIndex)?.CoefByDirection(road.BPosition).GetByDirection(RoadPosition.Left) ?? 0
                                : straightRoad.A == crossroad ? prevSituation?[straightRoad.IntID].a2bPrev : prevSituation?[straightRoad.IntID].b2aPrev) ?? 0;

                    if (crossroad == road.B)
                        road.b2a.start = prevSituation?[road.IntID].b2aCurr + prevSituation?[road.IntID].b2aPrev ?? 0;
                    else
                        road.a2b.start = prevSituation?[road.IntID].a2bCurr + prevSituation?[road.IntID].a2bPrev ?? 0;
                }
            }

            #endregion

            #region Loading coefficients

            var loadingCoefficients = new Dictionary<(int, bool), RoadLine>();
            foreach (var road in this.Roads)
            {
                foreach (var crossroad in road.Crossroads.Where(c => c is not null))
                {
                    var leftRoad = road.B.Roads.First(r => r != road && road.GetDirection(r) == RoadPosition.Left);
                    var rightRoad = road.B.Roads.First(r => r != road && road.GetDirection(r) == RoadPosition.Right);
                    var straighRoad = road.B.Roads.First(r => r != road && road.GetDirection(r) == RoadPosition.Top);

                    loadingCoefficients[(road.ID, crossroad == road.B)] = new(
                        leftRoad.IsExternal ? 1 : crossroad!.Load == 0 ? 0 : (8 / (Math.Pow(leftRoad.FirstNotCrossroad(crossroad).Load / crossroad!.Load, 3) + 8)),
                        rightRoad.IsExternal ? 1 : crossroad!.Load == 0 ? 0 : (8 / (Math.Pow(rightRoad.FirstNotCrossroad(crossroad).Load / crossroad!.Load, 3) + 8)),
                        straighRoad.IsExternal ? 1 : crossroad!.Load == 0 ? 0 : (8 / (Math.Pow(straighRoad.FirstNotCrossroad(crossroad).Load / crossroad!.Load, 3) + 8)));
                }
            }

            #endregion

            #region Mafia

            var crossroadPedestrianPositions = new PedestriansPosition[CrossroadsCount];
            foreach (var crossroad in this.Crossroads)
            {
                if (crossroad.PedestrIndex < 0)
                    continue;

                var position = this.PedestriansPositions[crossroad.PedestrIndex];
                var cpp = this.CrossroadPedestrianPositions?[crossroad.ID];
                crossroadPedestrianPositions[crossroad.ID] = new(
                    crossroad.ID,
                    1 + (0.01 * Math.Pow((1 + (cpp?.Top.left ?? 0)) * (position.Top.left == 1 ? 0 : 1), 2)),
                    1 + (0.01 * Math.Pow((1 + (cpp?.Top.straight ?? 0)) * (position.Top.straight == 1 ? 0 : 1), 2)),
                    1 + (0.01 * Math.Pow((1 + (cpp?.Top.right ?? 0)) * (position.Top.right == 1 ? 0 : 1), 2)),
                    1 + (0.01 * Math.Pow((1 + (cpp?.Left.left ?? 0)) * (position.Top.left == 1 ? 0 : 1), 2)),
                    1 + (0.01 * Math.Pow((1 + (cpp?.Left.straight ?? 0)) * (position.Top.straight == 1 ? 0 : 1), 2)),
                    1 + (0.01 * Math.Pow((1 + (cpp?.Left.right ?? 0)) * (position.Top.right == 1 ? 0 : 1), 2)),
                    1 + (0.01 * Math.Pow((1 + (cpp?.Right.left ?? 0)) * (position.Top.left == 1 ? 0 : 1), 2)),
                    1 + (0.01 * Math.Pow((1 + (cpp?.Right.straight ?? 0)) * (position.Top.straight == 1 ? 0 : 1), 2)),
                    1 + (0.01 * Math.Pow((1 + (cpp?.Right.right ?? 0)) * (position.Top.right == 1 ? 0 : 1), 2)),
                    1 + (0.01 * Math.Pow((1 + (cpp?.Bottom.left ?? 0)) * (position.Top.left == 1 ? 0 : 1), 2)),
                    1 + (0.01 * Math.Pow((1 + (cpp?.Bottom.straight ?? 0)) * (position.Top.straight == 1 ? 0 : 1), 2)),
                    1 + (0.01 * Math.Pow((1 + (cpp?.Bottom.right ?? 0)) * (position.Top.right == 1 ? 0 : 1), 2)),
                    0);
            }

            var mafia = new double[CrossroadsCount][];
            for (var i = 0; i < CrossroadsCount; i++)
            {
                mafia[i] = new double[this.PedestriansPositions.Length];
                var crossroad = this.Crossroads[i];
                for (int j = 0; j < this.PedestriansPositions.Length; j++)
                {
                    mafia[i][j] = PedestriansPosition.GetCoeff(this.PedestriansPositions[j].Type)
                        * crossroad.Roads
                            .Select(r =>
                            {
                                var position = r.PosByCross(crossroad);

                                var roadLine = r[crossroad];
                                var pedestrLine = this.PedestriansPositions[j].CoefByDirection(position);
                                var crossroadLine = crossroadPedestrianPositions.ElementAtOrDefault(crossroad.ID)?.CoefByDirection(position);

                                return roadLine.left * loadingCoefficients[(r.ID, true)].left * pedestrLine.left * (crossroadLine?.left ?? 1)
                                    + roadLine.right * loadingCoefficients[(r.ID, true)].right * pedestrLine.right * (crossroadLine?.right ?? 1)
                                    + roadLine.straight * loadingCoefficients[(r.ID, true)].straight * pedestrLine.straight * (crossroadLine?.straight ?? 1);

                            })
                            .Sum();
                }
                crossroad.PedestrIndex = mafia[i].ToList().LastIndexOf(mafia[i].Max());
            }

            #endregion

            #region Cars wroom-wroom

            (double a2bCurr, double b2aCurr, double a2bPrev, double b2aPrev)[] cars = new (double, double, double, double)[InternalRoadsCount];

            foreach (Road road in this.Roads.Where(r => r.IsInternal))
            {
                var position = this.PedestriansPositions[road.A!.PedestrIndex];
                var v1 = position.CoefByDirection(road.A.ExtRoads.First().BPosition).GetByDirection(road.A.ExtRoads.First().GetDirection(road));
                var v2 = position.CoefByDirection(road.A.ExtRoads.Last().BPosition).GetByDirection(road.A.ExtRoads.Last().GetDirection(road));

                cars[road.IntID].a2bCurr = road.A.ExtRoads.Select(r => r.a2b.finish.GetByDirection(r.GetDirection(road)) * position.CoefByDirection(r.BPosition).GetByDirection(r.GetDirection(road))).Sum();
                cars[road.IntID].b2aCurr = road.B.ExtRoads.Select(r => r.a2b.finish.GetByDirection(r.GetDirection(road)) * position.CoefByDirection(r.BPosition).GetByDirection(r.GetDirection(road))).Sum();

                var a2bRoad = road.A.FirstIntNotRoad(road);
                var b2aRoad = road.B.FirstIntNotRoad(road);

                var a2bValue = a2bRoad[road.A].GetByDirection(a2bRoad.GetDirection(road));
                var b2aValue = b2aRoad[road.B].GetByDirection(a2bRoad.GetDirection(road));

                cars[road.IntID].a2bPrev = a2bValue * position.CoefByDirection(a2bRoad.PosByCross(road.A)).GetByDirection(a2bRoad.GetDirection(road));
                cars[road.IntID].b2aPrev = b2aValue * position.CoefByDirection(b2aRoad.PosByCross(road.B)).GetByDirection(b2aRoad.GetDirection(road));
            }

            #endregion

            //this.LoadingCoefficients = loadingCoefficients;
            this.CrossroadPedestrianPositions = crossroadPedestrianPositions;
            //this.Mafia = mafia;
            this.PreviousSituaions.Enqueue(cars);

            this.RecoloringRoads();
        }

        #endregion

        #region UI

        /// <summary>
        /// Начальное рисование перекрестков
        /// </summary>
        private void PaintIt()
        {
            foreach (var crossroad in this.Crossroads)
            {
                var element = new TextBlock
                {
                    Text = crossroad.Text,
                    TextAlignment = TextAlignment.Center,
                    Tag = crossroad.ToString(),
                    Height = crossroad.Height,
                    Width = crossroad.Width,
                    Background = new VisualBrush { Visual = crossroad }
                };
                this.Canvas.Children.Add(element);
                var left = crossroad.IsRight
                    ? Crossroad.ConstWidth + 2 * Road.BigWidth
                    : Road.BigWidth;
                Canvas.SetLeft(element, left);
                var top = crossroad.IsBottom
                    ? Crossroad.ConstHeight + 2 * Road.BigWidth
                    : Road.BigWidth;
                Canvas.SetTop(element, top);
            }

            foreach (var road in this.Roads)
            {
                var element = new TextBlock
                {
                    Tag = road.ToString(),
                    Text = road.Text,
                    TextAlignment = TextAlignment.Center,
                    Height = road.Height,
                    Width = road.Width,
                    Background = new VisualBrush { Visual = road }
                };

                var left = true switch
                {
                    true when road.IsExternal && road.IsHorizontal && road.B.IsLeft => 0,
                    true when !road.IsHorizontal && road.B.IsLeft => Road.BigWidth + (Crossroad.ConstWidth - Road.SmallWidth) / 2,
                    true when road.IsInternal && road.IsHorizontal => Road.BigWidth + Crossroad.ConstWidth,
                    true when !road.IsHorizontal && road.B.IsRight => 2 * Road.BigWidth + Crossroad.ConstWidth + (Crossroad.ConstWidth - Road.SmallWidth) / 2,
                    _ => 2 * Road.BigWidth + 2 * Crossroad.ConstWidth,
                };
                var top = true switch
                {
                    true when road.IsExternal && !road.IsHorizontal && road.B.IsTop => 0,
                    true when road.IsHorizontal && road.B.IsTop => Road.BigWidth + (Crossroad.ConstWidth - Road.SmallWidth) / 2,
                    true when road.IsInternal && !road.IsHorizontal => Road.BigWidth + Crossroad.ConstWidth,
                    true when road.IsHorizontal && road.B.IsBottom => 2 * Road.BigWidth + Crossroad.ConstWidth + (Crossroad.ConstWidth - Road.SmallWidth) / 2,
                    _ => 2 * Road.BigWidth + 2 * Crossroad.ConstWidth,
                };

                this.Canvas.Children.Add(element);
                Canvas.SetLeft(element, left);
                Canvas.SetTop(element, top);
            }
        }

        /// <summary>
        /// Реинициализация UI таблицы.
        /// </summary>
        /// <param name="source">Таблица в коде.</param>
        /// <param name="headerNames">Названия столбцов.</param>
        private void RedeclareHeaders(ObservableCollection<Dictionary<string, object>> source)
        {
             this.IncomingCarsDataGrid.Columns.Clear();
            
            foreach (var key in source.FirstOrDefault()?.Keys.ToArray() ?? new[] { NumberHeader, InterestHeader })
            {
                this.IncomingCarsDataGrid.Columns.Add(new DataGridTextColumn()
                {
                    Header = key,
                    Binding = new Binding($"[{key}]") { Mode = BindingMode.TwoWay },
                    IsReadOnly = key == NumberHeader,
                    CanUserResize = true,
                    CanUserSort = false,
                    Width = DataGridLength.SizeToHeader
                });
            }

            this.IncomingCarsDataGrid.Items = new DataGridCollectionView(source);
            this.Source = source;
        }

        /// <summary>
        /// Запустить итерации.
        /// </summary>
        private void Iterations()
        {
            while (this.isRunning)
            {
                var timePerMove = 2000;
                Dispatcher.UIThread.Post(() => timePerMove = (int)(this.SpeedSlider.Value * 1000), DispatcherPriority.Background);
                Dispatcher.UIThread.Post(() => this.Iterate());
                Thread.Sleep(timePerMove);
            }
        }

        /// <summary>
        /// Изменить состояние симуляции.
        /// </summary>
        /// <param name="isRunning">Какое состояние установить.</param>
        private void ChangeStateIsRunning(bool? isRunning = null)
        {
            this.isRunning = isRunning ?? !this.isRunning;

            this.ContinueButton.IsEnabled = !this.isRunning && this.isStarted;
            this.StartButton.IsEnabled = !this.isRunning && !this.isStarted;
            this.StopButton.IsEnabled = this.isRunning || this.isStarted;
            this.PauseButton.IsEnabled = this.isRunning;
            this.DebugButton.IsEnabled = !this.isRunning;
        }

        /// <summary>
        /// Переопределение ветов дорог в зависимости от загруженности.
        /// </summary>
        private void RecoloringRoads()
        {
            foreach (var element in this.Canvas.Children)
            {
                var children = element.GetLogicalChildren();

                if (element is not TextBlock tb
                    || tb.Tag is not string tag) // sic!
                    continue;

                var road = this.Roads.FirstOrDefault(r => r.ToString() == tag);
                var crossroad = this.Crossroads.FirstOrDefault(c => c.ToString() == tag);

                if (road is null && crossroad is null)
                    continue;

                if (crossroad is not null)
                {
                    tb.Text = crossroad.Text;
                    continue;
                }

                var a2bStart = (int)(road!.a2b.start > this.maxTraffic ? 0 : 255 * (this.maxTraffic - road.a2b.start) / this.maxTraffic);
                var a2bFinish = (int)(road.a2b.finish.Sum > this.maxTraffic ? 0 : 255 * (this.maxTraffic - road.a2b.finish.Sum) / this.maxTraffic);
                var b2aStart = (int)(road.b2a.start > this.maxTraffic ? 0 : 255 * (this.maxTraffic - road.b2a.start) / this.maxTraffic);
                var b2aFinish = (int)(road.b2a.finish.Sum > this.maxTraffic ? 0 : 255 * (this.maxTraffic - road.b2a.finish.Sum) / this.maxTraffic);

                var lgb = new LinearGradientBrush
                {
                    StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                    EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                    GradientStops = road.IsExternal
                        ? new()
                            {
                                new(Color.FromRgb((byte)(255 - a2bFinish), (byte)a2bFinish, 0), 0),
                                new(Color.FromRgb((byte)(255 - b2aFinish), (byte)b2aFinish, 0), 0.5),
                            }
                        : new()
                            {
                                new(Color.FromRgb((byte)(255 - a2bStart), (byte)a2bStart, 0), 0),
                                new(Color.FromRgb((byte)(255 - a2bFinish), (byte)a2bFinish, 0), 0.25),
                                new(Color.FromRgb((byte)(255 - b2aStart), (byte)b2aStart, 0), 0.5),
                                new(Color.FromRgb((byte)(255 - b2aFinish), (byte)b2aFinish, 0), 0.75),
                            }
                };

                road.Fill = lgb;

                tb.Background = new VisualBrush { Visual = road };
                tb.Text = road.Text;
            }
        }

        /// <summary>
        /// Мессаджбокс.
        /// </summary>
        /// <param name="message">Сообщение.</param>
        private void MessageBoxShow(string message)
            => MessageBoxManager.GetMessageBoxStandardWindow(
                new MessageBoxStandardParams
                {
                    ButtonDefinitions = MessageBox.Avalonia.Enums.ButtonEnum.Ok,
                    ContentMessage = "Message: " + message,
                    CanResize = true,
                    Width = this.Width,
                    MaxWidth = this.MaxWidth / 2,
                    ShowInCenter = true
                }).ShowDialog(this);

        #endregion

        #region UI Controls handlers

        private void SpeedSlider_PropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property.Name == nameof(this.SpeedSlider.Value) && this.SpeedTextBlock is not null)
                this.SpeedTextBlock.Text = $"Скорость: {e.NewValue:0.000} секунд на ход";
        }

        private void Radiobutton_Checked(object sender, RoutedEventArgs e)
        {
            var source = sender == this.DisableIncomeRadiobutton
                ? new()
                : sender == this.UseCertainRadiobutton
                    ? this.currentIncomingTraffic
                    : sender == this.UseRandomRadiobutton
                        ? this.trafficBorders
                        : new();

            this.RedeclareHeaders(source);
        }

        private void IncomingCarsDataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            var row = e.Row.GetIndex();
            var column = e.Column.DisplayIndex;
            if (row > -1
                && row < this.IncomingCarsDataGrid.Items.Cast<object>().Count()
                && e.EditingElement is TextBox tb)
            {
                if (!int.TryParse(tb.Text, out var value))
                {
                    e.Cancel = true;
                    MessageBoxShow($"Not a number");
                }
                if (value < 0)
                {
                    e.Cancel = true;
                    MessageBoxShow("Positive numbers please");
                }
                else if (this.UseRandomRadiobutton.IsChecked == true
                    && new[] { 2, 3 }.Contains(column)
                    && (column == 2 ? this.trafficBorders[row][MaxHeader].ToInt() < value : this.trafficBorders[row][MinHeader].ToInt() > value))
                {
                    e.Cancel = true;
                    MessageBoxShow($"Maxval should be greater then minval");
                }
            }
        }

        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            this.isStarted = true;
            this.ContinueButton_Click(sender, e);
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            this.isStarted = false;
            this.ChangeStateIsRunning(false);
            this.InitializeFields();
            this.InitializeProbabilities();
            this.Canvas.Children.Clear();
            this.PaintIt();
        }

        private void PauseButton_Click(object sender, RoutedEventArgs e) => this.ChangeStateIsRunning(false);

        private void ContinueButton_Click(object sender, RoutedEventArgs e)
        {
            this.ChangeStateIsRunning(true);
            new Thread(() => this.Iterations()).Start();
        }

        private void DebugButton_Click(object sender, RoutedEventArgs e)
        {
            this.isStarted = true;
            this.ChangeStateIsRunning(false);
            this.Iterate();
        }

        #endregion

        #region Static

        public static string WideInterval(int interval) => new(Enumerable.Repeat(' ', interval).ToArray());

        public static string VerticalWide(int interval) => string.Join(string.Empty, Enumerable.Repeat(Environment.NewLine, interval));

        #endregion
    }

    #region Classes

    public class StatisticReport
    {
        public string? start_time { get; set; }
        public string? end_time { get; set; }
        public string? time_passed { get; set; }

        public DateTime? StartTime => DateTime.TryParseExact(this.start_time, "yyyyMMddHHmmss", null, DateTimeStyles.None, out var time) ? time : null;
        public DateTime? EndTime => DateTime.TryParseExact(this.end_time, "yyyyMMddHHmmss", null, DateTimeStyles.None, out var time) ? time : null;
        public TimeSpan? TimePassed => TimeSpan.TryParseExact(this.time_passed, @"h\:mm\:ss\.FFFFFF", null, TimeSpanStyles.None, out var time) ? time : null;

        public Statistic[]? statistics { get; set; }

        public class Statistic
        {
            public int id { get; set; }
            public int count_cars { get; set; }
        }

        public StatisticReport() { }
    }

    public static class ExtensionsCLass
    {
        public static int ToInt(this object obj) => obj is int i ? i : int.TryParse(obj.ToString(), out var s) ? s : throw new ArgumentOutOfRangeException(nameof(obj));
    }

    #endregion
}