using DesktopLife.Core;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace DesktopLife.App;

public sealed partial class RoomWindow
{
    public bool IsFoodSurface => Item.Kind is FurnitureKind.CatBowl or FurnitureKind.DiningTable;
    public event Action<FoodKind, bool>? FoodRequested;
    internal Point FoodContactPoint => new(Item.X + (Item.Kind == FurnitureKind.DiningTable ? 75 : 40) * SceneScale,
        Item.Y + (Item.Kind == FurnitureKind.DiningTable ? 20.5 : 11.5) * SceneScale);
    private FoodServing? serving;
    private readonly FoodContentsVisual foodContents = new();
    private MenuItem? foodSummary;

    private void InitializeFoodSurface()
    {
        if (!IsFoodSurface) return;
        var interactionStrip = new Border { Width = Width, Height = Item.Kind == FurnitureKind.CatBowl ? 28 : 35,
            Background = new SolidColorBrush(Color.FromArgb(1, 255, 255, 255)) };
        canvas.Children.Add(interactionStrip);
        canvas.Children.Add(foodContents);
        var menu = canvas.ContextMenu!;
        foodSummary = new MenuItem { Header = "尚未準備食物", IsEnabled = false };
        menu.Items.Insert(0, foodSummary);
        var allowed = Enum.GetValues<FoodKind>().Where(kind => FoodCatalog.CanServe(Item.Kind, kind));
        var index = 1;
        foreach (var kind in allowed)
        {
            var item = new MenuItem { Header = FoodName(kind), Tag = kind };
            item.Click += (_, _) => FoodRequested?.Invoke(kind, serving is {} current && current.Kind != kind);
            menu.Items.Insert(index++, item);
        }
        menu.Items.Insert(index, new Separator());
        canvas.PreviewMouseLeftButtonUp += (_, e) =>
        {
            if (editing) return;
            menu.PlacementTarget = canvas; menu.IsOpen = true; e.Handled = true;
        };
        SetFood(null);
    }

    internal static string FoodName(FoodKind kind) => kind switch
    {
        FoodKind.CatKibble => "補充貓飼料", FoodKind.DogKibble => "補充狗飼料", FoodKind.SharedKibble => "補充共用飼料",
        FoodKind.BurgerMeal => "漢堡套餐", FoodKind.FriedChickenMeal => "炸雞套餐", FoodKind.HotPot => "火鍋",
        FoodKind.Ramen => "拉麵", _ => "雞排"
    };

    public void SetFood(FoodServing? value)
    {
        serving = value;
        if (!IsFoodSurface) return;
        var summary = value is { RemainingPortions: > 0 }
            ? $"{FoodName(value.Kind).Replace("補充", "")} · 剩餘 {value.RemainingPortions} / {value.TotalPortions} 份"
            : Item.Kind == FurnitureKind.CatBowl ? "空飼料碗 · 點一下補充飼料" : "空餐桌 · 點一下準備料理";
        canvas.ToolTip = summary;
        if (foodSummary is not null) foodSummary.Header = summary;
        var table = Item.Kind == FurnitureKind.DiningTable;
        foodContents.Width = table ? 62 : 52; foodContents.Height = table ? 23 : 9;
        Canvas.SetLeft(foodContents, table ? 44 : 14); Canvas.SetTop(foodContents, table ? 9 : 7);
        foodContents.Update(value, table);
    }

    public void ShowFoodNotice(string message) => canvas.ToolTip = message;
    internal void ChooseFoodDiagnostic(FoodKind kind)
    {
        var option = canvas.ContextMenu!.Items.OfType<MenuItem>().Single(item => item.Tag is FoodKind value && value == kind);
        option.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
    }

    // This is an inventory indicator. The meal-specific illustrated props are a
    // separate art milestone; changing this never alters the furniture collider.
    private sealed class FoodContentsVisual : FrameworkElement
    {
        private FoodServing? value;
        private bool table;
        private static readonly Brush empty = Frozen("#ECE1CE"), plate = Frozen("#FAF0E3"), kibble = Frozen("#B68A65"), meal = Frozen("#D69B6E");
        private static Brush Frozen(string color) { var brush = (SolidColorBrush)new BrushConverter().ConvertFrom(color)!; brush.Freeze(); return brush; }
        public FoodContentsVisual() { IsHitTestVisible = false; }
        public void Update(FoodServing? serving, bool onTable) { value = serving; table = onTable; InvalidateVisual(); }
        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            if (table && value is null) return;
            dc.DrawEllipse(table ? plate : empty, null, new(Width / 2, Height / 2), Width / 2, Height / 2);
            if (value is not { RemainingPortions: > 0 }) return;
            var count = (int)Math.Ceiling(12d * value.RemainingPortions / value.Capacity);
            for (var i = 0; i < count; i++)
                dc.DrawEllipse(table ? meal : kibble, null, new(8 + i % 6 * (Width - 16) / 5, Height * (.35 + i / 6 * .3)), table ? 4 : 2.3, table ? 2.5 : 1.3);
        }
    }
}
