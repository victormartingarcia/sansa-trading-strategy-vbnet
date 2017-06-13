Imports System.Collections.Generic
Imports TradingMotion.SDKv2.Markets.Charts
Imports TradingMotion.SDKv2.Markets.Orders
Imports TradingMotion.SDKv2.Markets.Indicators.OverlapStudies
Imports TradingMotion.SDKv2.Algorithms
Imports TradingMotion.SDKv2.Algorithms.InputParameters

Namespace Sansa

    Public Class SansaStrategy
        Inherits Strategy

        ''' <summary>
        ''' Strategy required constructor
        ''' </summary>
        ''' <param name="mainChart">The Chart over the Strategy will run</param>
        ''' <param name="secondaryCharts">Secondary charts that the Strategy can use</param>
        Public Sub New(ByVal mainChart As Chart, ByVal secondaryCharts As List(Of Chart))
            MyBase.New(mainChart, secondaryCharts)
        End Sub

        ''' <summary>
        ''' Strategy Name
        ''' </summary>
        ''' <returns>The complete Name of the strategy</returns>
        Public Overrides ReadOnly Property Name As String
            Get
                Return "Sansa Strategy"
            End Get
        End Property

        ''' <summary>
        ''' Security filter that ensures the OpenPosition will be closed at the end of the trading session.
        ''' </summary>
        ''' <returns>
        ''' True if the opened position must be closed automatically on session's close, false otherwise
        ''' </returns>
        Public Overrides ReadOnly Property ForceCloseIntradayPosition As Boolean
            Get
                Return False
            End Get
        End Property

        ''' <summary>
        ''' Security filter that sets a maximum open position level, and ensures that the strategy will never exceeds it
        ''' </summary>
        ''' <returns>
        ''' The maximum opened lots allowed (any side)
        ''' </returns>
        Public Overrides ReadOnly Property MaxOpenPosition As UInteger
            Get
                Return 1
            End Get
        End Property

        ''' <summary>
        ''' Flag that indicates if the strategy uses advanced Order management or standard
        ''' </summary>
        ''' <returns>
        ''' True if strategy uses advanced Order management. This means that the strategy uses the advanced methods (InsertOrder/CancelOrder/ModifyOrder) in opposite of the simple ones (Buy/Sell/ExitLong/ExitShort).
        ''' </returns>
        Public Overrides ReadOnly Property UsesAdvancedOrderManagement As Boolean
            Get
                Return False
            End Get
        End Property

        ''' <summary>
        ''' Creates the set of exposed Parameters for the strategy
        ''' </summary>
        ''' <returns>The exposed Parameters collection</returns>
        Public Overrides Function SetInputParameters() As InputParameterList
            Return New InputParameterList() From
                {
                    New InputParameter("Slow Weighted Moving Average Period", 30),
                    New InputParameter("Fast Weighted Moving Average Period", 5),
                    New InputParameter("Ticks Take-Profit", 190),
                    New InputParameter("Ticks Stop-Loss", 60)
                }
        End Function

        ''' <summary>
        ''' Callback executed when the strategy starts executing. This is the right place
        ''' to create the Indicators that the strategy will use.
        ''' </summary>
        Public Overrides Sub OnInitialize()

            log.Debug("Sansa Strategy onInitialize()")
            
            Dim indSlowWMA = New WMAIndicator(Bars.Close, GetInputParameter("Slow Weighted Moving Average Period"))
            AddIndicator("Slow WMA", indSlowWMA) ''' Add indicator to the main chart


            If (ContainsSecondaryChart("FGBL", BarPeriodType.Minute, 120)) Then
                Dim bund120min As Chart = GetSecondaryChart("FGBL", BarPeriodType.Minute, 120)

                Dim indFastWMA = New WMAIndicator(Bars.Close, GetInputParameter("Fast Weighted Moving Average Period"))
                bund120min.AddIndicator("Fast WMA", indFastWMA) ''' Add indicator secondary chart
            Else
                Throw New Exception("Not Found Secondary Chart FGBL 120'")
            End If

        End Sub

        ''' <summary>
        ''' Callback executed for every new Bar. This is the right place
        ''' to check your Indicators/trading rules and place the orders accordingly.
        ''' </summary>
        Public Overrides Sub OnNewBar()

            Dim indFastSma As WMAIndicator = GetSecondaryChart(0).GetIndicator("Fast WMA")
            Dim indSlowSma As WMAIndicator = GetIndicator("Slow WMA")

            If GetOpenPosition() = 0 Then

                If indFastSma.GetWMA()(0) > indSlowSma.GetWMA()(0) And _
                    indFastSma.GetWMA()(1) < indSlowSma.GetWMA()(1) Then
                    'Check if Fast weighted moving average is higher than Slow moving average in current bar, and
                    'Check if Fast weighted moving average was lower than Slow moving average in previous bar

                    'Going Long (Buying 1 Contract at Market price)
                    Buy(OrderType.Market, 1, 0, "Open long position")
                End If

            End If

            'Place Take-Profit and Stop-Loss orders
            PlaceExitOrders()
        End Sub

        ''' <summary>
        ''' Helper method to place money management orders (Take profit and Stop Loss)
        ''' </summary>
        Protected Sub PlaceExitOrders()
            If GetOpenPosition() > 0 Then

                Dim ticksTakeProfit As Integer = GetInputParameter("Ticks Take-Profit")
                Dim ticksStopLoss As Integer = GetInputParameters("Ticks Stop-Loss")

                Dim takeProfitLevel As Double = GetFilledOrders()(0).FillPrice + (ticksTakeProfit * Symbol.TickSize)
                Dim stopLossLevel As Double = GetFilledOrders()(0).FillPrice - (ticksStopLoss * Symbol.TickSize)

                ExitLong(OrderType.Limit, Symbol.RoundToNearestTick(takeProfitLevel), "Take Profit")
                ExitLong(OrderType.Stop, Symbol.RoundToNearestTick(stopLossLevel), "Stop Loss")

            End If
        End Sub

    End Class
End Namespace