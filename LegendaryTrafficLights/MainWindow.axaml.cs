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
using Avalonia;
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
     *       | 11 |             | 09 |     
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

        /// <summary>
        /// Ссылка на веб-сервис, предоставляющий информацию о статистике проезжающих машин.
        /// </summary>
        //private const string uri = "http://localhost:8008";

        /// <summary>
        /// Заголовок столбца минимальных значений.
        /// </summary>
        private const string MinHeader = "Мин.";

        /// <summary>
        /// Заголовок столбца максимальных значений.
        /// </summary>
        private const string MaxHeader = "Макс.";

        /// <summary>
        /// Загловок столбца значений.
        /// </summary>
        private const string ValueHeader = "Значение";

        /// <summary>
        /// Заголовок столбца номеров дорог.
        /// </summary>
        private const string NumberHeader = "Номер";

        /// <summary>
        /// Заголовок столбца интересности дорог.
        /// </summary>
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
        private PedestriansPosition[][] CrossroadPedestrianPositions = new PedestriansPosition[CrossroadsCount][];

        #endregion

        #region Fields

        /// <summary>
        /// Перекрестки системы.
        /// </summary>
        private readonly Crossroad[] Crossroads = new Crossroad[CrossroadsCount];

        /// <summary>
        /// Дороги системы.
        /// </summary>
        private readonly Road[] Roads = new Road[TotalRoadsCount];

        /// <summary>
        /// Признак того, что симуляция активна.
        /// </summary>
        private bool isRunning = false;

        /// <summary>
        /// Признак того, что симуляция была начата (что она не в исходном состоянии).
        /// </summary>
        private bool isStarted = false;

        /// <summary>
        /// Источник данных в таблице.
        /// </summary>
        public ObservableCollection<Dictionary<string, object>>? Source = null;

        #endregion

        //#region Properties

        //private int[]? MafiaResult => this.Mafia?.Select(m => m.ToList().IndexOf(m.Max())).ToArray();

        //#endregion

        #region Constructors

        public MainWindow()
        {
            this.InitializeComponent();

            this.InitializeFields();
            this.UseCertainRadiobutton.IsChecked = true;

            this.InitializeProbabilities();
            this.ChangeStateIsRunning(false);
            this.PaintIt();
            this.SpeedSlider.Value = 2d;
        }

        #endregion

        #region Math

        /// <summary>
        /// Инициализация полей стандартными значениями.
        /// </summary>
        private void InitializeFields()
        {
            for (var i = 0; i < CrossroadsCount; i++)
                this.Crossroads[i] = new(i, (CrossroadPosition)i);

            this.PreviousSituaions.Clear();

            for (var i = 0; i < ExternalRoadsCount; i++)
            {
                if (this.trafficBorders.Count != ExternalRoadsCount)
                {
                    this.trafficBorders.Add(
                    new()
                    {
                        [NumberHeader] = this.Source?.ElementAtOrDefault(i)?[NumberHeader] ?? i,
                        [InterestHeader] = this.Source?.ElementAtOrDefault(i)?[InterestHeader] ?? 5,
                        [MinHeader] = 5,
                        [MaxHeader] = 10
                    });
                }

                if (this.currentIncomingTraffic.Count != ExternalRoadsCount)
                {
                    this.currentIncomingTraffic.Add(
                    new()
                    {
                        [NumberHeader] = this.Source?.ElementAtOrDefault(i)?[NumberHeader] ?? i,
                        [InterestHeader] = this.Source?.ElementAtOrDefault(i)?[InterestHeader] ?? 5,
                        [ValueHeader] = 5
                    });
                }

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
                        ? 1d * this.Source![j][InterestHeader].ToInt() / (totalInterest - this.Source[i][InterestHeader].ToInt())
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

        /// <summary>
        /// Заполнение списка входящих машин.
        /// </summary>
        private async Task<int[]> GetIncomingCars()
        {
            if (this.UseCertainRadiobutton.IsChecked == true)
            {
                return this.currentIncomingTraffic.Select(d => d[ValueHeader].ToInt()).ToArray();
            }
            else if (this.FromExternalServeRadiobuttonr.IsChecked == true)
            {
                return await this.GetDataFromServer();
            }
            else if (this.DisableIncomeRadiobutton.IsChecked == true)
            {
                return Enumerable.Repeat(0, ExternalRoadsCount).ToArray();
            }
            else if (this.UseRandomRadiobutton.IsChecked == true)
            {
                var rand = new Random();
                return this.trafficBorders.Select(d => rand.Next(d[MinHeader].ToInt(), d[MaxHeader].ToInt())).ToArray();
            }
            else throw new ArgumentOutOfRangeException(nameof(this.Radiobutton_Checked));
        }

        /// <summary>
        /// Получение информации о входящих машинах из запроса к серверу слежения.
        /// </summary>
        private async Task<int[]> GetDataFromServer()
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
                    throw new FormatException(nameof(result));

                var results = JsonSerializer.Deserialize<StatisticReport>(result);

                if (results?.EndTime is null)
                    throw new ArgumentNullException(nameof(StatisticReport.EndTime));

                return Enumerable.Range(0, ExternalRoadsCount).Select(r => results.statistics?.FirstOrDefault(s => s.id == r)?.count_cars ?? 0).ToArray();

            }
            catch (Exception ex)
            {
                this.MessageBoxShow(ex.Message);
            }

            return Enumerable.Repeat(0, ExternalRoadsCount).ToArray();
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
        private async void Iterate()
        {
            // New Enemy wave has arrived		
            var incomingTraffic = await this.GetIncomingCars();

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
                    var traffic = incomingTraffic[road.ID];

                    var position = this.PedestriansPositions.ElementAtOrDefault(roadCopy.B.PedestrIndex);
                    var roadPosition = position?.CoefByDirection(roadCopy.BPosition);

                    road.A2B.finish.left += traffic * this.baseTrafficProbabilities[roadCopy.ID][roadCopy.GetRoad(RoadPosition.Left).ID]
                        - (roadCopy.A2B.finish.left * roadPosition?.left ?? 0);
                    road.A2B.finish.straight += traffic * this.baseTrafficProbabilities[roadCopy.ID][roadCopy.GetRoad(RoadPosition.Top).ID]
                        - (roadCopy.A2B.finish.straight * roadPosition?.straight ?? 0);
                    road.A2B.finish.right += traffic * this.baseTrafficProbabilities[roadCopy.ID][roadCopy.GetRoad(RoadPosition.Right).ID]
                        - (roadCopy.A2B.finish.right * roadPosition?.right ?? 0);

                    var (start, finish) = roadCopy.B2A;
                    road.B2A.finish = new(finish.left + start / 3, finish.right + start / 3, finish.straight + start / 3);
                    road.B2A.start = roadCopy.B.Roads
                        .Where(r => r != roadCopy)
                        .Sum(r => position?.CoefByDirection(r.PosByCross(roadCopy.B)).GetByDirection(r.GetDirection(roadCopy))
                            * r[roadCopy.B].GetByDirection(r.GetDirection(roadCopy)) ?? 0);

                    continue;
                }

                foreach (var cross in road.Crossroads)
                {
                    if (cross is null)
                        throw new ArgumentNullException(nameof(cross));

                    var leftRoad = roadCopy.GetRoad(RoadPosition.Left, road.B == cross);
                    var rightRoad = roadCopy.GetRoad(RoadPosition.Right, road.B == cross);
                    var straightRoad = roadCopy.GetRoad(RoadPosition.Top, road.B == cross);

                    var lastCurr = (road.B == cross ? lastSituation?[road.IntID].a2bCurr : lastSituation?[road.IntID].b2aCurr) ?? 0;
                    var lastPrev = (road.B == cross ? lastSituation?[road.IntID].a2bPrev : lastSituation?[road.IntID].b2aPrev) ?? 0;

                    var copyLine = roadCopy[cross];

                    var s1 = lastCurr * this.extendedTrafficProbabilities[0][road.IntID][leftRoad.ID];
                    var s2 = lastPrev * this.extendedTrafficProbabilities[1][road.IntID][leftRoad.ID];
                    var s3 = (leftRoad.IsExternal
                                ? (copyLine.left * this.PedestriansPositions.ElementAtOrDefault(cross.PedestrIndex)
                                    ?.CoefByDirection(road.PosByCross(cross)).GetByDirection(RoadPosition.Left) ?? 0)
                                : ((leftRoad.A == cross ? prevSituation?[leftRoad.IntID].a2bPrev : prevSituation?[leftRoad.IntID].b2aPrev) ?? 0));

                    road[cross].left = copyLine.left
                            + lastCurr * this.extendedTrafficProbabilities[0][road.IntID][leftRoad.ID]
                            + lastPrev * this.extendedTrafficProbabilities[1][road.IntID][leftRoad.ID]
                            - (leftRoad.IsExternal
                                ? (copyLine.left * this.PedestriansPositions.ElementAtOrDefault(cross.PedestrIndex)
                                    ?.CoefByDirection(road.PosByCross(cross)).GetByDirection(RoadPosition.Left) ?? 0)
                                : ((leftRoad.A == cross ? prevSituation?[leftRoad.IntID].a2bPrev : prevSituation?[leftRoad.IntID].b2aPrev) ?? 0));

                    road[cross].right = copyLine.right
                            + lastCurr * this.extendedTrafficProbabilities[0][road.IntID][rightRoad.ID]
                            + lastPrev * this.extendedTrafficProbabilities[1][road.IntID][rightRoad.ID]
                            - (rightRoad.IsExternal
                                ? (copyLine.right * this.PedestriansPositions.ElementAtOrDefault(cross.PedestrIndex)
                                    ?.CoefByDirection(road.PosByCross(cross)).GetByDirection(RoadPosition.Right) ?? 0)
                                : ((rightRoad.A == cross ? prevSituation?[rightRoad.IntID].a2bPrev : prevSituation?[rightRoad.IntID].b2aPrev) ?? 0));

                    road[cross].straight = copyLine.straight
                            + lastCurr * this.extendedTrafficProbabilities[0][road.IntID][straightRoad.ID]
                            + lastPrev * this.extendedTrafficProbabilities[1][road.IntID][straightRoad.ID]
                            - (straightRoad.IsExternal
                                ? (copyLine.straight * this.PedestriansPositions.ElementAtOrDefault(cross.PedestrIndex) 
                                    ?.CoefByDirection(road.PosByCross(cross)).GetByDirection(RoadPosition.Top) ?? 0)
                                : ((straightRoad.A == cross ? prevSituation?[straightRoad.IntID].a2bPrev : prevSituation?[straightRoad.IntID].b2aPrev) ?? 0));

                    if (cross == road.B)
                        road.B2A.start = prevSituation?[road.IntID].b2aCurr + prevSituation?[road.IntID].b2aPrev ?? 0;
                    else
                        road.A2B.start = prevSituation?[road.IntID].a2bCurr + prevSituation?[road.IntID].a2bPrev ?? 0;
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

            var crossPedPositions = new PedestriansPosition[CrossroadsCount][];
            foreach (var crossroad in this.Crossroads)
            {
                crossPedPositions[crossroad.ID] = new PedestriansPosition[2];

                if (crossroad.PedestrIndex < 0)
                    continue;

                var position = this.PedestriansPositions[crossroad.PedestrIndex];
                var cpp = this.CrossroadPedestrianPositions?[crossroad.ID];
                crossPedPositions[crossroad.ID][0] = new(
                    crossroad.ID,
                    (1 + (cpp?[0]?.Top.left ?? 0)) *         (position.Top.left == 1 ? 0 : 1),
                    (1 + (cpp?[0]?.Top.straight ?? 0)) *     (position.Top.straight == 1 ? 0 : 1),
                    (1 + (cpp?[0]?.Top.right ?? 0)) *        (position.Top.right == 1 ? 0 : 1),
                    (1 + (cpp?[0]?.Left.left ?? 0)) *        (position.Left.left == 1 ? 0 : 1),
                    (1 + (cpp?[0]?.Left.straight ?? 0)) *    (position.Left.straight == 1 ? 0 : 1),
                    (1 + (cpp?[0]?.Left.right ?? 0)) *       (position.Left.right == 1 ? 0 : 1),
                    (1 + (cpp?[0]?.Right.left ?? 0)) *       (position.Right.left == 1 ? 0 : 1),
                    (1 + (cpp?[0]?.Right.straight ?? 0)) *   (position.Right.straight == 1 ? 0 : 1),
                    (1 + (cpp?[0]?.Right.right ?? 0)) *      (position.Right.right == 1 ? 0 : 1),
                    (1 + (cpp?[0]?.Bottom.left ?? 0)) *      (position.Bottom.left == 1 ? 0 : 1),
                    (1 + (cpp?[0]?.Bottom.straight ?? 0)) *  (position.Bottom.straight == 1 ? 0 : 1),
                    (1 + (cpp?[0]?.Bottom.right ?? 0)) *     (position.Bottom.right == 1 ? 0 : 1),
                    0);

                crossPedPositions[crossroad.ID][1] = new(
                    crossroad.ID,
                    1 + (0.01 * Math.Pow(crossPedPositions[crossroad.ID][0].Top.left, 2)),
                    1 + (0.01 * Math.Pow(crossPedPositions[crossroad.ID][0].Top.straight, 2)),
                    1 + (0.01 * Math.Pow(crossPedPositions[crossroad.ID][0].Top.right, 2)),
                    1 + (0.01 * Math.Pow(crossPedPositions[crossroad.ID][0].Left.left, 2)),
                    1 + (0.01 * Math.Pow(crossPedPositions[crossroad.ID][0].Left.straight, 2)),
                    1 + (0.01 * Math.Pow(crossPedPositions[crossroad.ID][0].Left.right, 2)),
                    1 + (0.01 * Math.Pow(crossPedPositions[crossroad.ID][0].Right.left, 2)),
                    1 + (0.01 * Math.Pow(crossPedPositions[crossroad.ID][0].Right.straight, 2)),
                    1 + (0.01 * Math.Pow(crossPedPositions[crossroad.ID][0].Right.right, 2)),
                    1 + (0.01 * Math.Pow(crossPedPositions[crossroad.ID][0].Bottom.left, 2)),
                    1 + (0.01 * Math.Pow(crossPedPositions[crossroad.ID][0].Bottom.straight, 2)),
                    1 + (0.01 * Math.Pow(crossPedPositions[crossroad.ID][0].Bottom.right, 2)),
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
                                var crossroadLine = crossPedPositions.ElementAtOrDefault(crossroad.ID)?[1]?.CoefByDirection(position);

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

            foreach (var road in this.Roads.Where(r => r.IsInternal))
            {
                var aPosition = this.PedestriansPositions[road.A!.PedestrIndex];
                var bPosition = this.PedestriansPositions[road.B.PedestrIndex];

                cars[road.IntID].a2bCurr = road.A.ExtRoads.Select(r =>
                    r.A2B.finish.GetByDirection(r.GetDirection(road)) * aPosition.CoefByDirection(r.BPosition).GetByDirection(r.GetDirection(road))).Sum();
                cars[road.IntID].b2aCurr = road.B.ExtRoads.Select(r =>
                    r.A2B.finish.GetByDirection(r.GetDirection(road)) * bPosition.CoefByDirection(r.BPosition).GetByDirection(r.GetDirection(road))).Sum();

                var a2bRoad = road.A.FirstIntNotRoad(road);
                var b2aRoad = road.B.FirstIntNotRoad(road);

                var a2bValue = a2bRoad[road.A].GetByDirection(a2bRoad.GetDirection(road));
                var b2aValue = b2aRoad[road.B].GetByDirection(b2aRoad.GetDirection(road));

                var v1 = aPosition.CoefByDirection(a2bRoad.PosByCross(road.A)).GetByDirection(a2bRoad.GetDirection(road));
                var v2 = bPosition.CoefByDirection(b2aRoad.PosByCross(road.B)).GetByDirection(b2aRoad.GetDirection(road));

                cars[road.IntID].a2bPrev = a2bValue * aPosition.CoefByDirection(a2bRoad.PosByCross(road.A)).GetByDirection(a2bRoad.GetDirection(road));
                cars[road.IntID].b2aPrev = b2aValue * bPosition.CoefByDirection(b2aRoad.PosByCross(road.B)).GetByDirection(b2aRoad.GetDirection(road));
                }

            #endregion

            this.CrossroadPedestrianPositions = crossPedPositions;
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
            
            foreach (var key in source.First().Keys.ToArray())
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
            var maxTraffic = 100 + this.Crossroads.Max(c => c.Load) * 1.2;

            foreach (var element in this.Canvas.Children)
            {
                if (element is not TextBlock tb
                    || tb.Tag is not string tag) // sic!
                    continue;

                var road = this.Roads.FirstOrDefault(r => r.ToString() == tag);
                var crossroad = this.Crossroads.FirstOrDefault(c => c.ToString() == tag);

                if (crossroad is not null)
                {
                    tb.Text = crossroad.Text;
                    crossroad.Fill = new SolidColorBrush(Color.FromRgb((byte)(crossroad.Load / maxTraffic * 255), (byte)((1 - crossroad.Load / maxTraffic) * 255), 0));
                    continue;
                }

                if (road is null)
                    continue;

                var colorA = Color.FromRgb((byte)(road.A?.Load / maxTraffic * 255 ?? 0), (byte)((1 - (road.A?.Load / maxTraffic ?? 0)) * 255), 0);
                var colorB = Color.FromRgb((byte)(road.B.Load / maxTraffic * 255), (byte)((1 - road.B.Load / maxTraffic) * 255), 0);

                if (road.AIsLeft && road.BIsRight || road.AIsTop && road.BIsBottom)  
                    (colorA, colorB) = (colorB, colorA);

                var lgb = new LinearGradientBrush
                {
                    StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                    EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                    GradientStops = new() { new(colorA, 0), new(colorB, 1) }
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
                new()
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
                ? new(this.currentIncomingTraffic.Select(i =>
                    new Dictionary<string, object>() { [NumberHeader] = i[NumberHeader], [InterestHeader] = i[InterestHeader] }))
                : sender == this.UseCertainRadiobutton
                    ? this.currentIncomingTraffic
                    : sender == this.UseRandomRadiobutton
                        ? this.trafficBorders
                        : new(this.currentIncomingTraffic.Select(i =>
                            new Dictionary<string, object>() { [NumberHeader] = i[NumberHeader], [InterestHeader] = i[InterestHeader] }));

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

        /// <summary>
        /// Широкий интервал (пробельный).
        /// </summary>
        /// <param name="interval">Ширина в символах.</param>
        /// <returns></returns>
        public static string WideInterval(int interval) => new(Enumerable.Repeat(' ', interval).ToArray());

        /// <summary>
        /// Широкий в высоту интервал.
        /// </summary>
        /// <param name="interval">Ширина в символах.</param>
        /// <returns></returns>
        public static string VerticalWide(int interval) => string.Join(string.Empty, Enumerable.Repeat(Environment.NewLine, interval));

        #endregion
    }

    #region Classes

    /// <summary>
    /// Класс для десериализации данных с сервера.
    /// </summary>
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

    /// <summary>
    /// Класс расширений.
    /// </summary>
    public static class ExtensionsCLass
    {
        /// <summary>
        /// Привести объект к целочисленному типу.
        /// </summary>
        /// <param name="obj">Объект для приведения.</param>
        /// <returns></returns>
        /// <exception cref="ArgumentOutOfRangeException">Невозможно привести объект к целочисленному типу.</exception>
        public static int ToInt(this object obj) => obj is int i ? i : int.TryParse(obj.ToString(), out var s) ? s : throw new ArgumentOutOfRangeException(nameof(obj));

    }

    #endregion
}