﻿Imports CustomSeriesPointDrawingSample.Model
Imports DevExpress.XtraCharts
Imports System
Imports System.Collections.Generic
Imports System.Data
Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.IO
Imports System.Linq
Imports System.Windows.Forms

Namespace CustomSeriesPointDrawingSample
	Partial Public Class Form1
		Inherits Form

		Private trackedPointArgument As Object
		Private photoCache As New Dictionary(Of String, Image)()

		#Region "#Constants"
		Private Const borderSize As Integer = 5
		Private Const scaledPhotoWidth As Integer = 48
		Private Const scaledPhotoHeight As Integer = 51
		' Width and height of scaled photo with border.
		Private Const totalWidth As Integer = 58
		Private Const totalHeight As Integer = 61

		' Rects required to create a custom legend series marker.
		Private Shared ReadOnly photoRect As New Rectangle(borderSize, borderSize, scaledPhotoWidth, scaledPhotoHeight)
		Private Shared ReadOnly totalRect As New Rectangle(0, 0, totalWidth, totalHeight)
		#End Region

		Public Sub New()
			InitializeComponent()
		End Sub

		#Region "#ChartPreparation"
		Private Sub Form1_Load(ByVal sender As Object, ByVal e As EventArgs) Handles Me.Load
			AddHandler chart.CustomDrawSeriesPoint, AddressOf OnCustomDrawSeriesPoint
			AddHandler chart.BoundDataChanged, AddressOf OnBoundDataChanged
			AddHandler chart.ObjectHotTracked, AddressOf OnObjectHotTracked

			Using context = New NwindDbContext()
				chart.DataSource = PrepareDataSource(context.Orders)
				InitPhotoCache(context.Employees)
			End Using
			chart.SeriesDataMember = "Year"
			chart.SeriesTemplate.ArgumentDataMember = "Employee"
			chart.SeriesTemplate.ValueDataMembers.AddRange("Value")

			chart.SeriesTemplate.ToolTipPointPattern = "{S}: {A} ({VP:P})"
			chart.SeriesTemplate.SeriesPointsSorting = SortingMode.Ascending
		End Sub
		#End Region

		#Region "#AutogeneratedSeriesModifying"
		Private Sub OnBoundDataChanged(ByVal sender As Object, ByVal e As EventArgs)
			If chart.Series.Count <= 1 Then
				Return
			End If
			For i As Integer = 1 To chart.Series.Count - 1
				chart.Series(i).ShowInLegend = False
			Next i
		End Sub
		#End Region

		#Region "#CustomPointDrawing"
		Private Sub OnCustomDrawSeriesPoint(ByVal sender As Object, ByVal e As CustomDrawSeriesPointEventArgs)
			' Design a series marker image.
			Dim image As New Bitmap(totalWidth, totalHeight)
			Dim isSelected As Boolean = trackedPointArgument IsNot Nothing AndAlso e.SeriesPoint.Argument.Equals(trackedPointArgument)

			Using graphics As Graphics = System.Drawing.Graphics.FromImage(image)
				Using fillBrush = If(isSelected, CType(New HatchBrush(HatchStyle.DarkDownwardDiagonal, e.LegendDrawOptions.Color, e.LegendDrawOptions.ActualColor2), Brush), CType(New SolidBrush(e.LegendDrawOptions.Color), Brush))
					graphics.FillRectangle(fillBrush, totalRect)
				End Using
				Dim photo As Image = Nothing
				If photoCache.TryGetValue(e.SeriesPoint.Argument, photo) Then
					graphics.DrawImage(photo, photoRect)
				End If
			End Using

			e.LegendMarkerImage = image
			e.DisposeLegendMarkerImage = True

			Dim options As PieDrawOptions = TryCast(e.SeriesDrawOptions, PieDrawOptions)
			If isSelected AndAlso options IsNot Nothing Then
				options.FillStyle.FillMode = DevExpress.XtraCharts.FillMode.Hatch
				CType(options.FillStyle.Options, HatchFillOptions).HatchStyle = HatchStyle.DarkDownwardDiagonal
			End If
		End Sub
		#End Region

		Private Sub OnObjectHotTracked(ByVal sender As Object, ByVal e As HotTrackEventArgs)
			trackedPointArgument = If(e.HitInfo.InSeriesPoint, e.HitInfo.SeriesPoint.Argument, Nothing)
			chart.Invalidate()
		End Sub

		Private Sub InitPhotoCache(ByVal employees As IEnumerable(Of Employee))
			photoCache.Clear()
			For Each employee In employees
				Using stream As New MemoryStream(employee.Photo)
					If Not photoCache.ContainsKey(employee.FullName) Then
						photoCache.Add(employee.FullName, Image.FromStream(stream))
					End If
				End Using
			Next employee
		End Sub

		Private Function PrepareDataSource(ByVal orders As IEnumerable(Of Order)) As List(Of SalesPoint)
			Dim query = From o In orders
				Group o By GroupKey = New With {
					Key .Year = o.OrderDate.Year,
					Key .Employee = o.Employee.FirstName & " " & o.Employee.LastName
				} Into g = Group
				Select New With {
					Key .Employee = GroupKey.Employee,
					Key .Year = GroupKey.Year,
					Key .Values = g.Select(Function(o)If(o.Freight.HasValue, o.Freight.Value, 0))
				}

			Dim points As New List(Of SalesPoint)()
			For Each item In query
				points.Add(New SalesPoint With {
					.Employee = item.Employee,
					.Year = item.Year,
					.Value = item.Values.Aggregate(Function(d1, d2) d1 + d2)
				})
			Next item
			Return points
		End Function
	End Class
End Namespace

Friend Class SalesPoint
	Public Property Employee() As String
	Public Property Year() As Integer
	Public Property Value() As Decimal
End Class
