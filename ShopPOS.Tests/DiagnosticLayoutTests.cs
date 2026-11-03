using System;
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using ShopPOS.UI;
using Xunit;
using Xunit.Abstractions;

namespace ShopPOS.Tests;

public class DiagnosticLayoutTests
{
    private readonly ITestOutputHelper _output;

    public DiagnosticLayoutTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private static void RunOnSta(Action action)
    {
        if (Application.Current != null && Application.Current.Dispatcher != null)
        {
            if (Application.Current.Dispatcher.CheckAccess())
            {
                action();
            }
            else
            {
                Application.Current.Dispatcher.Invoke(action);
            }
            return;
        }

        Exception? threadEx = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                threadEx = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (threadEx != null)
        {
            ExceptionDispatchInfo.Capture(threadEx).Throw();
        }
    }

    private static T? FindVisualChild<T>(DependencyObject parent, string? name = null) where T : DependencyObject
    {
        int count = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T typedChild)
            {
                if (string.IsNullOrEmpty(name) || (child is FrameworkElement fe && fe.Name == name))
                {
                    return typedChild;
                }
            }

            var nested = FindVisualChild<T>(child, name);
            if (nested != null)
            {
                return nested;
            }
        }
        return null;
    }

    [Fact]
    public void Step1_Diagnose_ImplicitControls_Metrics()
    {
        RunOnSta(() =>
        {
            if (Application.Current == null)
            {
                var app = new App();
                app.InitializeComponent();
            }

            var window = new Window
            {
                Title = "Diagnostic Window",
                Width = 800,
                Height = 600,
                WindowStyle = WindowStyle.None,
                ShowInTaskbar = false
            };

            var stack = new StackPanel { Margin = new Thickness(10) };
            window.Content = stack;

            // 1. Plain TextBox using implicit style
            var textBox = new TextBox { Text = "Ag89$#y" };
            stack.Children.Add(textBox);

            // 2. Plain ComboBox using implicit style
            var comboBox = new ComboBox();
            comboBox.Items.Add("Option 1 (Ag89$#y)");
            comboBox.SelectedIndex = 0;
            stack.Children.Add(comboBox);

            // 3. Plain DatePicker using implicit style
            var datePicker = new DatePicker { SelectedDate = DateTime.Today };
            stack.Children.Add(datePicker);

            // 4. Plain Button using implicit style
            var button = new Button { Content = "Submit (Ag89$#y)" };
            stack.Children.Add(button);

            // 5. Plain CheckBox using implicit style
            var checkBox = new CheckBox { Content = "Check Option (Ag89$#y)", IsChecked = true };
            stack.Children.Add(checkBox);

            // 6. Plain DataGrid and row using implicit style
            var dataGrid = new DataGrid
            {
                AutoGenerateColumns = false,
                ItemsSource = new[] { new { Name = "Row Item (Ag89$#y)" } }
            };
            dataGrid.Columns.Add(new DataGridTextColumn
            {
                Header = "Name",
                Binding = new System.Windows.Data.Binding("Name")
            });
            stack.Children.Add(dataGrid);

            // Show window and force render pass
            window.Show();
            Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.Render);

            _output.WriteLine("==========================================================================================");
            _output.WriteLine("STEP 1: CURRENT STATE CONTROL METRICS DIAGNOSTIC (USING IMPLICIT / TYPE-KEYED STYLES)");
            _output.WriteLine("==========================================================================================");

            // Report TextBox
            {
                var contentHost = FindVisualChild<ScrollViewer>(textBox, "PART_ContentHost");
                double requiredLineHeight = textBox.FontFamily.LineSpacing * textBox.FontSize;
                double contentHostHeight = contentHost != null ? contentHost.ActualHeight : 0;
                double contentHostMarginTop = contentHost != null ? contentHost.Margin.Top : 0;
                double contentHostMarginBottom = contentHost != null ? contentHost.Margin.Bottom : 0;

                _output.WriteLine("--- TextBox ---");
                _output.WriteLine($"  ActualHeight:         {textBox.ActualHeight:F2} px");
                _output.WriteLine($"  Explicit Height:      {(double.IsNaN(textBox.Height) ? "Auto" : textBox.Height.ToString("F2"))}");
                _output.WriteLine($"  MinHeight:            {textBox.MinHeight:F2} px");
                _output.WriteLine($"  Padding:              {textBox.Padding} (Left={textBox.Padding.Left}, Top={textBox.Padding.Top}, Right={textBox.Padding.Right}, Bottom={textBox.Padding.Bottom})");
                _output.WriteLine($"  BorderThickness:      {textBox.BorderThickness} (Top={textBox.BorderThickness.Top}, Bottom={textBox.BorderThickness.Bottom})");
                _output.WriteLine($"  Margin:               {textBox.Margin}");
                _output.WriteLine($"  FontFamily:           {textBox.FontFamily.Source}");
                _output.WriteLine($"  FontSize:             {textBox.FontSize:F2} pt");
                _output.WriteLine($"  Font LineSpacing:     {textBox.FontFamily.LineSpacing:F4}");
                _output.WriteLine($"  Required Line Height: {requiredLineHeight:F2} px");
                _output.WriteLine($"  PART_ContentHost:     ActualHeight={contentHostHeight:F2} px, Margin={contentHost?.Margin}");
                _output.WriteLine($"  Remaining for Text:   {(contentHostHeight - contentHostMarginTop - contentHostMarginBottom):F2} px");
                _output.WriteLine($"  Clipping Risk:        {((contentHostHeight - contentHostMarginTop - contentHostMarginBottom) < requiredLineHeight ? "YES - CLIPPED!" : "NO")}");
                _output.WriteLine("");
            }

            // Report ComboBox
            {
                var contentPresenter = FindVisualChild<ContentPresenter>(comboBox);
                double requiredLineHeight = comboBox.FontFamily.LineSpacing * comboBox.FontSize;
                double contentHeight = contentPresenter != null ? contentPresenter.ActualHeight : 0;

                _output.WriteLine("--- ComboBox ---");
                _output.WriteLine($"  ActualHeight:         {comboBox.ActualHeight:F2} px");
                _output.WriteLine($"  Explicit Height:      {(double.IsNaN(comboBox.Height) ? "Auto" : comboBox.Height.ToString("F2"))}");
                _output.WriteLine($"  MinHeight:            {comboBox.MinHeight:F2} px");
                _output.WriteLine($"  Padding:              {comboBox.Padding}");
                _output.WriteLine($"  BorderThickness:      {comboBox.BorderThickness}");
                _output.WriteLine($"  Margin:               {comboBox.Margin}");
                _output.WriteLine($"  FontFamily:           {comboBox.FontFamily.Source}");
                _output.WriteLine($"  FontSize:             {comboBox.FontSize:F2} pt");
                _output.WriteLine($"  Font LineSpacing:     {comboBox.FontFamily.LineSpacing:F4}");
                _output.WriteLine($"  Required Line Height: {requiredLineHeight:F2} px");
                _output.WriteLine($"  Content Host Height:  {contentHeight:F2} px");
                _output.WriteLine("");
            }

            // Report DatePicker
            {
                var datePickerTextBox = FindVisualChild<DatePickerTextBox>(datePicker);
                double requiredLineHeight = datePicker.FontFamily.LineSpacing * datePicker.FontSize;
                double contentHeight = datePickerTextBox != null ? datePickerTextBox.ActualHeight : 0;

                _output.WriteLine("--- DatePicker ---");
                _output.WriteLine($"  ActualHeight:         {datePicker.ActualHeight:F2} px");
                _output.WriteLine($"  Explicit Height:      {(double.IsNaN(datePicker.Height) ? "Auto" : datePicker.Height.ToString("F2"))}");
                _output.WriteLine($"  MinHeight:            {datePicker.MinHeight:F2} px");
                _output.WriteLine($"  Padding:              {datePicker.Padding}");
                _output.WriteLine($"  BorderThickness:      {datePicker.BorderThickness}");
                _output.WriteLine($"  Margin:               {datePicker.Margin}");
                _output.WriteLine($"  FontFamily:           {datePicker.FontFamily.Source}");
                _output.WriteLine($"  FontSize:             {datePicker.FontSize:F2} pt");
                _output.WriteLine($"  Font LineSpacing:     {datePicker.FontFamily.LineSpacing:F4}");
                _output.WriteLine($"  Required Line Height: {requiredLineHeight:F2} px");
                _output.WriteLine($"  Inner TextBox Height: {contentHeight:F2} px");
                _output.WriteLine("");
            }

            // Report Button
            {
                var contentPresenter = FindVisualChild<ContentPresenter>(button);
                double requiredLineHeight = button.FontFamily.LineSpacing * button.FontSize;
                double contentHeight = contentPresenter != null ? contentPresenter.ActualHeight : 0;

                _output.WriteLine("--- Button ---");
                _output.WriteLine($"  ActualHeight:         {button.ActualHeight:F2} px");
                _output.WriteLine($"  Explicit Height:      {(double.IsNaN(button.Height) ? "Auto" : button.Height.ToString("F2"))}");
                _output.WriteLine($"  MinHeight:            {button.MinHeight:F2} px");
                _output.WriteLine($"  Padding:              {button.Padding}");
                _output.WriteLine($"  BorderThickness:      {button.BorderThickness}");
                _output.WriteLine($"  Margin:               {button.Margin}");
                _output.WriteLine($"  FontFamily:           {button.FontFamily.Source}");
                _output.WriteLine($"  FontSize:             {button.FontSize:F2} pt");
                _output.WriteLine($"  Font LineSpacing:     {button.FontFamily.LineSpacing:F4}");
                _output.WriteLine($"  Required Line Height: {requiredLineHeight:F2} px");
                _output.WriteLine($"  ContentPresenter H:   {contentHeight:F2} px");
                _output.WriteLine("");
            }

            // Report CheckBox
            {
                var contentPresenter = FindVisualChild<ContentPresenter>(checkBox);
                double requiredLineHeight = checkBox.FontFamily.LineSpacing * checkBox.FontSize;
                double contentHeight = contentPresenter != null ? contentPresenter.ActualHeight : 0;

                _output.WriteLine("--- CheckBox ---");
                _output.WriteLine($"  ActualHeight:         {checkBox.ActualHeight:F2} px");
                _output.WriteLine($"  Explicit Height:      {(double.IsNaN(checkBox.Height) ? "Auto" : checkBox.Height.ToString("F2"))}");
                _output.WriteLine($"  MinHeight:            {checkBox.MinHeight:F2} px");
                _output.WriteLine($"  Padding:              {checkBox.Padding}");
                _output.WriteLine($"  BorderThickness:      {checkBox.BorderThickness}");
                _output.WriteLine($"  Margin:               {checkBox.Margin}");
                _output.WriteLine($"  FontFamily:           {checkBox.FontFamily.Source}");
                _output.WriteLine($"  FontSize:             {checkBox.FontSize:F2} pt");
                _output.WriteLine($"  Font LineSpacing:     {checkBox.FontFamily.LineSpacing:F4}");
                _output.WriteLine($"  Required Line Height: {requiredLineHeight:F2} px");
                _output.WriteLine($"  ContentPresenter H:   {contentHeight:F2} px");
                _output.WriteLine("");
            }

            // Report DataGrid Row & Cell
            {
                var row = FindVisualChild<DataGridRow>(dataGrid);
                var cell = FindVisualChild<DataGridCell>(dataGrid);
                double requiredLineHeight = dataGrid.FontFamily.LineSpacing * dataGrid.FontSize;
                double rowHeight = row != null ? row.ActualHeight : 0;
                double cellHeight = cell != null ? cell.ActualHeight : 0;

                _output.WriteLine("--- DataGrid Row / Cell ---");
                _output.WriteLine($"  Row ActualHeight:     {rowHeight:F2} px");
                _output.WriteLine($"  Cell ActualHeight:    {cellHeight:F2} px");
                _output.WriteLine($"  MinHeight:            {(row != null ? row.MinHeight.ToString("F2") : "N/A")} px");
                _output.WriteLine($"  Padding:              {(cell != null ? cell.Padding.ToString() : "N/A")}");
                _output.WriteLine($"  BorderThickness:      {(cell != null ? cell.BorderThickness.ToString() : "N/A")}");
                _output.WriteLine($"  FontFamily:           {dataGrid.FontFamily.Source}");
                _output.WriteLine($"  FontSize:             {dataGrid.FontSize:F2} pt");
                _output.WriteLine($"  Font LineSpacing:     {dataGrid.FontFamily.LineSpacing:F4}");
                _output.WriteLine($"  Required Line Height: {requiredLineHeight:F2} px");
                _output.WriteLine("==========================================================================================");
            }

            window.Close();
        });
    }
}

